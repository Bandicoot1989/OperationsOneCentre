using Azure.AI.OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using RecipeSearchWeb.Interfaces;
using RecipeSearchWeb.Models;
using System.Text.Json;

namespace RecipeSearchWeb.Services;

/// <summary>
/// Service that processes resolved Jira tickets and extracts clean knowledge snippets.
/// Uses LLM to summarize and sanitize ticket content into structured solutions.
/// </summary>
public class JiraHarvesterService : IJiraSolutionHarvester
{
    private readonly ChatClient _chatClient;
    private readonly EmbeddingClient _embeddingClient;
    private readonly JiraSolutionStorageService _storageService;
    private readonly JiraSolutionSearchService _searchService;
    private readonly ILogger<JiraHarvesterService> _logger;

    // Prompt for extracting solutions from tickets
    private const string ExtractionPrompt = @"Eres un Analista Técnico Senior experto en IT Service Management.

Analiza el siguiente ticket de soporte técnico y extrae la información clave.

## REGLAS CRÍTICAS:
1. **NO incluyas información personal** (nombres de personas, correos electrónicos, IPs internas específicas)
2. **Ignora saludos, agradecimientos y ruido conversacional** en los comentarios
3. **Si no hay una solución clara documentada**, devuelve null
4. **Identifica el sistema/aplicación afectada** (SAP, Network, Email, SharePoint, etc.)
5. **Resume en español** manteniendo términos técnicos en inglés cuando sea apropiado

## FORMATO DE RESPUESTA (JSON):
```json
{
  ""problem"": ""Descripción concisa del problema en 1-2 oraciones"",
  ""rootCause"": ""Causa raíz identificada (o null si no se identificó)"",
  ""solution"": ""Descripción de la solución aplicada"",
  ""steps"": [""Paso 1"", ""Paso 2"", ""Paso 3""],
  ""system"": ""Sistema afectado (SAP, Network, Email, SharePoint, VPN, Hardware, etc.)"",
  ""category"": ""Categoría (Access, Configuration, Error, Installation, Performance, Security)"",
  ""keywords"": [""keyword1"", ""keyword2"", ""keyword3""]
}
```

Si el ticket NO tiene una solución clara o aplicable, responde SOLO con: null";

    public JiraHarvesterService(
        AzureOpenAIClient azureClient,
        IConfiguration configuration,
        EmbeddingClient embeddingClient,
        JiraSolutionStorageService storageService,
        JiraSolutionSearchService searchService,
        ILogger<JiraHarvesterService> logger)
    {
        var chatModel = configuration["AZURE_OPENAI_CHAT_NAME"] ?? "gpt-4o-mini";
        _chatClient = azureClient.GetChatClient(chatModel);
        _embeddingClient = embeddingClient;
        _storageService = storageService;
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Process a single ticket and extract a clean solution snippet
    /// </summary>
    public async Task<JiraSolution?> HarvestSolutionAsync(JiraTicket ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.Resolution) && !ticket.Comments.Any())
        {
            _logger.LogDebug("Skipping ticket {Key}: No resolution or comments", ticket.Key);
            return null;
        }

        try
        {
            // Build the ticket content for LLM analysis
            var ticketContent = BuildTicketContent(ticket);
            
            // Call LLM to extract solution
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(ExtractionPrompt),
                new UserChatMessage($"## TICKET: {ticket.Key}\n\n{ticketContent}")
            };

            var response = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
            {
                Temperature = 0.1f, // Low temperature for consistent extraction
                MaxOutputTokenCount = 1000
            });

            var responseText = response.Value.Content[0].Text?.Trim() ?? "";
            
            // Check if LLM returned null (no valid solution)
            if (responseText == "null" || string.IsNullOrWhiteSpace(responseText))
            {
                _logger.LogDebug("Ticket {Key}: No valid solution extracted", ticket.Key);
                return null;
            }

            // Parse the JSON response
            var extractedData = ParseExtractionResponse(responseText);
            if (extractedData == null)
            {
                _logger.LogWarning("Ticket {Key}: Failed to parse LLM response", ticket.Key);
                return null;
            }

            // Build the JiraSolution object
            var solution = new JiraSolution
            {
                TicketId = ticket.Key,
                TicketTitle = ticket.Summary,
                Problem = extractedData.Problem ?? "",
                RootCause = extractedData.RootCause ?? "",
                Solution = extractedData.Solution ?? "",
                Steps = extractedData.Steps ?? new List<string>(),
                System = extractedData.System ?? "General",
                Category = extractedData.Category ?? "Other",
                Keywords = extractedData.Keywords ?? new List<string>(),
                Priority = ticket.Priority,
                ResolvedDate = ticket.Resolved ?? DateTime.UtcNow,
                HarvestedDate = DateTime.UtcNow,
                JiraUrl = $"https://antolin.atlassian.net/browse/{ticket.Key}" // Adjust base URL as needed
            };

            // Generate embedding for semantic search
            var searchText = solution.GetSearchableText();
            var embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(searchText);
            solution.Embedding = embeddingResult.Value.ToFloats();

            _logger.LogInformation("Harvested solution from ticket {Key}: System={System}, Keywords={Keywords}",
                ticket.Key, solution.System, string.Join(",", solution.Keywords));

            return solution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error harvesting solution from ticket {Key}", ticket.Key);
            return null;
        }
    }

    /// <summary>
    /// Process multiple tickets in batch
    /// </summary>
    public async Task<List<JiraSolution>> HarvestSolutionsAsync(List<JiraTicket> tickets, IProgress<int>? progress = null)
    {
        var solutions = new List<JiraSolution>();
        var harvestedIds = await _storageService.GetHarvestedTicketIdsAsync();
        var processedCount = 0;

        foreach (var ticket in tickets)
        {
            // Skip already harvested tickets
            if (harvestedIds.Contains(ticket.Key))
            {
                _logger.LogDebug("Skipping already harvested ticket {Key}", ticket.Key);
                processedCount++;
                progress?.Report(processedCount);
                continue;
            }

            var solution = await HarvestSolutionAsync(ticket);
            if (solution != null)
            {
                solutions.Add(solution);
                await _storageService.MarkTicketAsHarvestedAsync(ticket.Key);
            }

            processedCount++;
            progress?.Report(processedCount);

            // Small delay to avoid rate limiting
            await Task.Delay(100);
        }

        // Save all new solutions
        if (solutions.Any())
        {
            var existingSolutions = await _storageService.LoadSolutionsAsync();
            existingSolutions.AddRange(solutions);
            await _storageService.SaveSolutionsAsync(existingSolutions);
            
            // Reload search service cache
            await _searchService.ReloadAsync();
        }

        return solutions;
    }

    /// <summary>
    /// Run a full harvest cycle (requires IJiraClient to be implemented)
    /// </summary>
    public async Task<HarvestResult> RunHarvestCycleAsync(int days = 30)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new HarvestResult();

        _logger.LogInformation("Starting Jira harvest cycle for last {Days} days", days);

        // NOTE: This requires IJiraClient to be implemented
        // For MVP, we'll use manual import via ImportFromJsonAsync
        
        _logger.LogWarning("Full harvest cycle requires IJiraClient implementation. Use ImportFromJsonAsync for manual import.");
        
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    /// <summary>
    /// Import solutions from a JSON file (manual MVP approach)
    /// </summary>
    public async Task<HarvestResult> ImportFromJsonAsync(string jsonContent)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new HarvestResult();

        try
        {
            var tickets = JsonSerializer.Deserialize<List<JiraTicket>>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tickets == null || !tickets.Any())
            {
                result.Errors.Add("No tickets found in JSON");
                return result;
            }

            result.TicketsProcessed = tickets.Count;
            _logger.LogInformation("Importing {Count} tickets from JSON", tickets.Count);

            var progress = new Progress<int>(count => 
            {
                if (count % 10 == 0)
                    _logger.LogInformation("Processed {Count}/{Total} tickets", count, tickets.Count);
            });

            var solutions = await HarvestSolutionsAsync(tickets, progress);
            
            result.SolutionsExtracted = solutions.Count;
            result.FailedExtractions = tickets.Count - solutions.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing from JSON");
            result.Errors.Add(ex.Message);
        }

        result.Duration = stopwatch.Elapsed;
        return result;
    }

    /// <summary>
    /// Build ticket content string for LLM analysis
    /// </summary>
    private string BuildTicketContent(JiraTicket ticket)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"**Título**: {ticket.Summary}");
        sb.AppendLine($"**Estado**: {ticket.Status}");
        sb.AppendLine($"**Prioridad**: {ticket.Priority}");
        sb.AppendLine($"**Resolución**: {ticket.Resolution}");
        sb.AppendLine();
        
        if (!string.IsNullOrWhiteSpace(ticket.Description))
        {
            sb.AppendLine("**Descripción**:");
            sb.AppendLine(TruncateText(ticket.Description, 1000));
            sb.AppendLine();
        }

        if (ticket.Comments.Any())
        {
            sb.AppendLine("**Comentarios** (más recientes primero):");
            var recentComments = ticket.Comments
                .OrderByDescending(c => c.Created)
                .Take(10); // Limit to 10 most recent comments
            
            foreach (var comment in recentComments)
            {
                sb.AppendLine($"[{comment.Created:yyyy-MM-dd}] {TruncateText(comment.Body, 500)}");
                sb.AppendLine("---");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parse the LLM extraction response
    /// </summary>
    private ExtractionResult? ParseExtractionResponse(string response)
    {
        try
        {
            // Remove markdown code blocks if present
            response = response.Replace("```json", "").Replace("```", "").Trim();
            
            return JsonSerializer.Deserialize<ExtractionResult>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse extraction response: {Response}", response);
            return null;
        }
    }

    /// <summary>
    /// Truncate text to a maximum length
    /// </summary>
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Internal class for parsing LLM response
    /// </summary>
    private class ExtractionResult
    {
        public string? Problem { get; set; }
        public string? RootCause { get; set; }
        public string? Solution { get; set; }
        public List<string>? Steps { get; set; }
        public string? System { get; set; }
        public string? Category { get; set; }
        public List<string>? Keywords { get; set; }
    }
}
