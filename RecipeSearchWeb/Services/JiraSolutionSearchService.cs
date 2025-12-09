using OpenAI.Embeddings;
using RecipeSearchWeb.Interfaces;
using RecipeSearchWeb.Models;

namespace RecipeSearchWeb.Services;

/// <summary>
/// Service for searching Jira solutions using semantic search with RRF
/// Weight: 0.65 (lower than documentation but useful for troubleshooting hints)
/// </summary>
public class JiraSolutionSearchService : IJiraSolutionService
{
    private readonly EmbeddingClient _embeddingClient;
    private readonly JiraSolutionStorageService _storageService;
    private readonly ILogger<JiraSolutionSearchService> _logger;
    
    private List<JiraSolution> _solutions = new();
    private bool _isInitialized = false;
    
    // RRF constant
    private const int RRF_K = 60;
    
    // Weight for Jira solutions in combined search (lower than docs/confluence)
    public const float JIRA_SOLUTION_WEIGHT = 0.65f;

    public JiraSolutionSearchService(
        EmbeddingClient embeddingClient,
        JiraSolutionStorageService storageService,
        ILogger<JiraSolutionSearchService> logger)
    {
        _embeddingClient = embeddingClient;
        _storageService = storageService;
        _logger = logger;
    }

    public bool IsAvailable => _storageService.IsAvailable;

    /// <summary>
    /// Initialize the service by loading solutions from storage
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            await _storageService.InitializeAsync();
            _solutions = await _storageService.LoadSolutionsAsync();
            _isInitialized = true;
            
            // Log statistics by system
            var systemCounts = _solutions.GroupBy(s => s.System)
                .Select(g => $"{g.Key}: {g.Count()}");
            
            _logger.LogInformation(
                "Jira Solution search initialized with {Count} solutions. Systems: [{Systems}]", 
                _solutions.Count, 
                string.Join(", ", systemCounts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Jira solution search service");
            _solutions = new List<JiraSolution>();
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Search for similar solutions using hybrid search with RRF
    /// </summary>
    public async Task<List<JiraSolutionSearchResult>> SearchSolutionsAsync(string query, int topK = 5)
    {
        await InitializeAsync();

        if (!_solutions.Any())
        {
            _logger.LogDebug("No Jira solutions loaded for search");
            return new List<JiraSolutionSearchResult>();
        }

        try
        {
            const int retrieveCount = 15;
            
            // STEP 1: Keyword search with ranking
            var keywordRanked = SearchByKeywordRanked(query, retrieveCount);
            
            // STEP 2: Semantic search
            var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.Value.ToFloats();

            var semanticRanked = _solutions
                .Where(s => s.Embedding.Length > 0)
                .Select(solution => new
                {
                    Solution = solution,
                    Score = CosineSimilarity(queryVector, solution.Embedding)
                })
                .Where(x => x.Score > 0.20) // Threshold for relevance
                .OrderByDescending(x => x.Score)
                .Take(retrieveCount)
                .Select((x, rank) => new RankedSolution { Solution = x.Solution, Rank = rank + 1, Score = x.Score })
                .ToList();

            // STEP 3: Calculate RRF scores
            var rrfScores = new Dictionary<string, (JiraSolution Sol, double RrfScore, double KeywordScore, double SemanticScore)>();
            
            foreach (var item in keywordRanked)
            {
                var rrfScore = 1.0 / (RRF_K + item.Rank);
                if (!rrfScores.ContainsKey(item.Solution.TicketId))
                {
                    rrfScores[item.Solution.TicketId] = (item.Solution, rrfScore, item.Score, 0.0);
                }
                else
                {
                    var existing = rrfScores[item.Solution.TicketId];
                    rrfScores[item.Solution.TicketId] = (existing.Sol, existing.RrfScore + rrfScore, item.Score, existing.SemanticScore);
                }
            }
            
            foreach (var item in semanticRanked)
            {
                var rrfScore = 1.0 / (RRF_K + item.Rank);
                if (!rrfScores.ContainsKey(item.Solution.TicketId))
                {
                    rrfScores[item.Solution.TicketId] = (item.Solution, rrfScore, 0.0, item.Score);
                }
                else
                {
                    var existing = rrfScores[item.Solution.TicketId];
                    rrfScores[item.Solution.TicketId] = (existing.Sol, existing.RrfScore + rrfScore, existing.KeywordScore, item.Score);
                }
            }
            
            // STEP 4: Build results with boosted scores
            var maxRrf = 2.0 / (RRF_K + 1);
            var results = rrfScores.Values
                .OrderByDescending(x => x.RrfScore)
                .Take(topK)
                .Select(x => new JiraSolutionSearchResult
                {
                    Solution = x.Sol,
                    RelevanceScore = (float)(x.RrfScore / maxRrf) * JIRA_SOLUTION_WEIGHT,
                    SimilarityScore = (float)x.SemanticScore
                })
                .ToList();

            _logger.LogInformation(
                "Jira solution search for '{Query}': Found {Count} results. Top: {Top}", 
                query, 
                results.Count,
                results.FirstOrDefault()?.Solution.TicketId ?? "none");

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Jira solutions");
            return new List<JiraSolutionSearchResult>();
        }
    }

    /// <summary>
    /// Search formatted for agent context (returns string like other search services)
    /// </summary>
    public async Task<string> SearchForAgentAsync(string query, int topK = 3)
    {
        var results = await SearchSolutionsAsync(query, topK);
        
        if (!results.Any())
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("### Soluciones de Tickets Históricos (Experiencia Previa):");
        sb.AppendLine("_Nota: Estas son sugerencias basadas en incidencias anteriores. Prioriza siempre la documentación oficial si hay contradicción._");
        sb.AppendLine();

        foreach (var result in results)
        {
            var solution = result.Solution;
            sb.AppendLine($"**[{solution.TicketId}]** Sistema: {solution.System}");
            sb.AppendLine($"- **Problema**: {solution.Problem}");
            sb.AppendLine($"- **Solución**: {solution.Solution}");
            
            if (solution.Steps.Any())
            {
                sb.AppendLine("- **Pasos**:");
                foreach (var step in solution.Steps.Take(3))
                {
                    sb.AppendLine($"  - {step}");
                }
            }
            
            if (solution.ValidationCount > 0)
            {
                sb.AppendLine($"- _Validado {solution.ValidationCount} veces por usuarios_");
            }
            
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Keyword search with ranking
    /// </summary>
    private List<RankedSolution> SearchByKeywordRanked(string query, int maxResults)
    {
        var stopWords = new HashSet<string> { 
            "que", "es", "el", "la", "los", "las", "un", "una", "de", "del", "en", "por", "para",
            "como", "cual", "donde", "cuando", "quien", "qué", "cuál", "dónde", "cuándo", "quién",
            "what", "is", "the", "a", "an", "of", "in", "for", "to", "how", "which", "where", "when", "who",
            "me", "te", "se", "nos", "mi", "tu", "su", "error", "problema", "issue"
        };
        
        var terms = query.Split(new[] { ' ', '?', '¿', '!', '¡', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .Select(t => t.ToLowerInvariant())
            .Where(t => !stopWords.Contains(t))
            .ToList();

        if (!terms.Any()) return new List<RankedSolution>();

        var scoredMatches = _solutions.Select(sol =>
        {
            var searchableText = $"{sol.Problem} {sol.Solution} {sol.RootCause} {string.Join(" ", sol.Keywords)}".ToLowerInvariant();
            var systemText = sol.System.ToLowerInvariant();
            
            double score = 0;
            int matchedTerms = 0;
            
            foreach (var term in terms)
            {
                // System match gets highest score
                if (systemText.Contains(term))
                {
                    score += 2.5;
                    matchedTerms++;
                }
                // Keyword match
                else if (sol.Keywords.Any(k => k.ToLowerInvariant().Contains(term)))
                {
                    score += 2.0;
                    matchedTerms++;
                }
                // Problem/Solution text match
                else if (searchableText.Contains(term))
                {
                    score += 1.0;
                    matchedTerms++;
                }
            }
            
            // Boost by validation count (proven solutions rank higher)
            if (sol.ValidationCount > 0)
            {
                score *= (1.0 + Math.Min(sol.ValidationCount * 0.1, 0.5)); // Max 50% boost
            }
            
            if (terms.Count > 0 && matchedTerms > 0)
            {
                score *= (1.0 + (double)matchedTerms / terms.Count);
            }
            
            return new { Solution = sol, Score = score };
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(maxResults)
        .Select((x, rank) => new RankedSolution { Solution = x.Solution, Rank = rank + 1, Score = x.Score })
        .ToList();
        
        return scoredMatches;
    }

    /// <summary>
    /// Get a solution by ticket ID
    /// </summary>
    public async Task<JiraSolution?> GetSolutionByTicketIdAsync(string ticketId)
    {
        await InitializeAsync();
        return _solutions.FirstOrDefault(s => 
            s.TicketId.Equals(ticketId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validate a solution (user found it helpful)
    /// </summary>
    public async Task ValidateSolutionAsync(string ticketId)
    {
        await _storageService.IncrementValidationCountAsync(ticketId);
        
        // Update local cache
        var solution = _solutions.FirstOrDefault(s => s.TicketId == ticketId);
        if (solution != null)
        {
            solution.ValidationCount++;
        }
    }

    /// <summary>
    /// Get solutions with high validation counts (candidates for promotion)
    /// </summary>
    public async Task<List<JiraSolution>> GetPromotionCandidatesAsync(int minValidations = 5)
    {
        await InitializeAsync();
        return _solutions
            .Where(s => s.ValidationCount >= minValidations && !s.IsPromoted)
            .OrderByDescending(s => s.ValidationCount)
            .ToList();
    }

    /// <summary>
    /// Mark a solution as promoted
    /// </summary>
    public async Task MarkAsPromotedAsync(string ticketId)
    {
        await _storageService.MarkAsPromotedAsync(ticketId);
        
        var solution = _solutions.FirstOrDefault(s => s.TicketId == ticketId);
        if (solution != null)
        {
            solution.IsPromoted = true;
        }
    }

    /// <summary>
    /// Get statistics about the solution database
    /// </summary>
    public async Task<JiraSolutionStats> GetStatsAsync()
    {
        await InitializeAsync();
        
        return new JiraSolutionStats
        {
            TotalSolutions = _solutions.Count,
            ValidatedSolutions = _solutions.Count(s => s.ValidationCount > 0),
            PromotedSolutions = _solutions.Count(s => s.IsPromoted),
            SolutionsBySystem = _solutions
                .GroupBy(s => s.System)
                .ToDictionary(g => g.Key, g => g.Count()),
            SolutionsByCategory = _solutions
                .GroupBy(s => s.Category)
                .ToDictionary(g => g.Key, g => g.Count()),
            LastHarvestDate = _solutions.Any() 
                ? _solutions.Max(s => s.HarvestedDate) 
                : null,
            OldestSolution = _solutions.Any() 
                ? _solutions.Min(s => s.ResolvedDate) 
                : null,
            NewestSolution = _solutions.Any() 
                ? _solutions.Max(s => s.ResolvedDate) 
                : null
        };
    }

    /// <summary>
    /// Reload solutions from storage
    /// </summary>
    public async Task ReloadAsync()
    {
        _solutions = await _storageService.LoadSolutionsAsync();
        _logger.LogInformation("Reloaded {Count} Jira solutions", _solutions.Count);
    }

    /// <summary>
    /// Add a new solution (used by harvester)
    /// </summary>
    public async Task AddSolutionAsync(JiraSolution solution)
    {
        await _storageService.UpsertSolutionAsync(solution, _solutions);
        
        // Update local cache if not already present
        if (!_solutions.Any(s => s.TicketId == solution.TicketId))
        {
            _solutions.Add(solution);
        }
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private float CosineSimilarity(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
    {
        var spanA = a.Span;
        var spanB = b.Span;
        
        if (spanA.Length != spanB.Length || spanA.Length == 0)
            return 0;

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < spanA.Length; i++)
        {
            dotProduct += spanA[i] * spanB[i];
            normA += spanA[i] * spanA[i];
            normB += spanB[i] * spanB[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    /// <summary>
    /// Helper class for ranked results
    /// </summary>
    private class RankedSolution
    {
        public JiraSolution Solution { get; set; } = null!;
        public int Rank { get; set; }
        public double Score { get; set; }
    }
}
