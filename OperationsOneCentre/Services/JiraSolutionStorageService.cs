using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using OperationsOneCentre.Models;
using System.Text.Json;

namespace OperationsOneCentre.Services;

/// <summary>
/// Service for storing and retrieving Jira solutions from Azure Blob Storage
/// </summary>
public class JiraSolutionStorageService
{
    private readonly BlobContainerClient? _containerClient;
    private readonly ILogger<JiraSolutionStorageService> _logger;
    private const string SolutionsBlob = "jira-solutions-with-embeddings.json";
    private const string HarvestedTicketsBlob = "harvested-tickets.json";
    private bool _isAvailable = false;

    public JiraSolutionStorageService(IConfiguration configuration, ILogger<JiraSolutionStorageService> logger)
    {
        _logger = logger;
        try
        {
            var connectionString = configuration["AzureStorage:ConnectionString"] 
                ?? configuration["AZURE_STORAGE_CONNECTION_STRING"];
            
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Azure Storage connection string not configured - Jira solution service disabled");
                return;
            }
            
            // Use the same container as the harvester service
            var containerName = configuration["AzureStorage:HarvestedSolutionsContainer"] ?? "harvested-solutions";
            var blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            _isAvailable = true;
            _logger.LogInformation("JiraSolutionStorageService initialized with container: {Container}", containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize JiraSolutionStorageService");
        }
    }

    public bool IsAvailable => _isAvailable;

    /// <summary>
    /// Initialize the container
    /// </summary>
    public async Task InitializeAsync()
    {
        if (!_isAvailable || _containerClient == null) return;
        
        try
        {
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            _logger.LogInformation("Jira solutions storage container initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Jira solutions container");
            _isAvailable = false;
        }
    }

    /// <summary>
    /// Save all Jira solutions
    /// </summary>
    public async Task SaveSolutionsAsync(List<JiraSolution> solutions)
    {
        if (!_isAvailable || _containerClient == null) return;
        
        var storageModels = solutions.Select(s => new JiraSolutionStorageModel
        {
            TicketId = s.TicketId,
            TicketTitle = s.TicketTitle,
            Problem = s.Problem,
            RootCause = s.RootCause,
            Solution = s.Solution,
            Steps = s.Steps,
            System = s.System,
            Category = s.Category,
            Keywords = s.Keywords,
            Priority = s.Priority,
            ResolvedDate = s.ResolvedDate,
            HarvestedDate = s.HarvestedDate,
            ValidationCount = s.ValidationCount,
            IsPromoted = s.IsPromoted,
            JiraUrl = s.JiraUrl,
            Embedding = s.Embedding.ToArray()
        }).ToList();

        var json = JsonSerializer.Serialize(storageModels, new JsonSerializerOptions { WriteIndented = true });
        var blobClient = _containerClient.GetBlobClient(SolutionsBlob);
        
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, overwrite: true);
        
        _logger.LogInformation("Saved {Count} Jira solutions to storage", solutions.Count);
    }

    /// <summary>
    /// Load all Jira solutions
    /// </summary>
    public async Task<List<JiraSolution>> LoadSolutionsAsync()
    {
        if (!_isAvailable || _containerClient == null) 
            return new List<JiraSolution>();
        
        var blobClient = _containerClient.GetBlobClient(SolutionsBlob);
        
        if (!await blobClient.ExistsAsync())
        {
            _logger.LogInformation("No existing Jira solutions found in storage");
            return new List<JiraSolution>();
        }

        var response = await blobClient.DownloadContentAsync();
        var json = response.Value.Content.ToString();
        
        var storageModels = JsonSerializer.Deserialize<List<JiraSolutionStorageModel>>(json) 
            ?? new List<JiraSolutionStorageModel>();

        var solutions = storageModels.Select(s => new JiraSolution
        {
            TicketId = s.TicketId,
            TicketTitle = s.TicketTitle,
            Problem = s.Problem,
            RootCause = s.RootCause,
            Solution = s.Solution,
            Steps = s.Steps ?? new(),
            System = s.System,
            Category = s.Category,
            Keywords = s.Keywords ?? new(),
            Priority = s.Priority,
            ResolvedDate = s.ResolvedDate,
            HarvestedDate = s.HarvestedDate,
            ValidationCount = s.ValidationCount,
            IsPromoted = s.IsPromoted,
            JiraUrl = s.JiraUrl,
            Embedding = s.Embedding ?? Array.Empty<float>()
        }).ToList();

        _logger.LogInformation("Loaded {Count} Jira solutions from storage", solutions.Count);
        return solutions;
    }

    /// <summary>
    /// Add or update a single solution
    /// </summary>
    public async Task UpsertSolutionAsync(JiraSolution solution, List<JiraSolution> allSolutions)
    {
        var existing = allSolutions.FindIndex(s => s.TicketId == solution.TicketId);
        if (existing >= 0)
        {
            allSolutions[existing] = solution;
        }
        else
        {
            allSolutions.Add(solution);
        }
        
        await SaveSolutionsAsync(allSolutions);
    }

    /// <summary>
    /// Get the set of already harvested ticket IDs (to avoid duplicates)
    /// </summary>
    public async Task<HashSet<string>> GetHarvestedTicketIdsAsync()
    {
        if (!_isAvailable || _containerClient == null) 
            return new HashSet<string>();
        
        var blobClient = _containerClient.GetBlobClient(HarvestedTicketsBlob);
        
        if (!await blobClient.ExistsAsync())
        {
            return new HashSet<string>();
        }

        var response = await blobClient.DownloadContentAsync();
        var json = response.Value.Content.ToString();
        
        return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
    }

    /// <summary>
    /// Save the set of harvested ticket IDs
    /// </summary>
    public async Task SaveHarvestedTicketIdsAsync(HashSet<string> ticketIds)
    {
        if (!_isAvailable || _containerClient == null) return;
        
        var json = JsonSerializer.Serialize(ticketIds, new JsonSerializerOptions { WriteIndented = true });
        var blobClient = _containerClient.GetBlobClient(HarvestedTicketsBlob);
        
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, overwrite: true);
    }

    /// <summary>
    /// Add a ticket ID to the harvested set
    /// </summary>
    public async Task MarkTicketAsHarvestedAsync(string ticketId)
    {
        var harvestedIds = await GetHarvestedTicketIdsAsync();
        harvestedIds.Add(ticketId);
        await SaveHarvestedTicketIdsAsync(harvestedIds);
    }

    /// <summary>
    /// Update validation count for a solution
    /// </summary>
    public async Task IncrementValidationCountAsync(string ticketId)
    {
        var solutions = await LoadSolutionsAsync();
        var solution = solutions.FirstOrDefault(s => s.TicketId == ticketId);
        
        if (solution != null)
        {
            solution.ValidationCount++;
            await SaveSolutionsAsync(solutions);
            _logger.LogInformation("Incremented validation count for ticket {TicketId} to {Count}", 
                ticketId, solution.ValidationCount);
        }
    }

    /// <summary>
    /// Mark a solution as promoted
    /// </summary>
    public async Task MarkAsPromotedAsync(string ticketId)
    {
        var solutions = await LoadSolutionsAsync();
        var solution = solutions.FirstOrDefault(s => s.TicketId == ticketId);
        
        if (solution != null)
        {
            solution.IsPromoted = true;
            await SaveSolutionsAsync(solutions);
            _logger.LogInformation("Marked ticket {TicketId} as promoted", ticketId);
        }
    }

    /// <summary>
    /// Delete a solution
    /// </summary>
    public async Task DeleteSolutionAsync(string ticketId)
    {
        var solutions = await LoadSolutionsAsync();
        var removed = solutions.RemoveAll(s => s.TicketId == ticketId);
        
        if (removed > 0)
        {
            await SaveSolutionsAsync(solutions);
            _logger.LogInformation("Deleted solution for ticket {TicketId}", ticketId);
        }
    }

    /// <summary>
    /// Get storage statistics
    /// </summary>
    public async Task<(long sizeBytes, int solutionCount)> GetStorageStatsAsync()
    {
        if (!_isAvailable || _containerClient == null) 
            return (0, 0);
        
        try
        {
            var blobClient = _containerClient.GetBlobClient(SolutionsBlob);
            if (!await blobClient.ExistsAsync())
                return (0, 0);
            
            var properties = await blobClient.GetPropertiesAsync();
            var solutions = await LoadSolutionsAsync();
            
            return (properties.Value.ContentLength, solutions.Count);
        }
        catch
        {
            return (0, 0);
        }
    }
}
