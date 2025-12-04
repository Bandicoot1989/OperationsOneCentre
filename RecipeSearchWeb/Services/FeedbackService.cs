using Azure.Storage.Blobs;
using RecipeSearchWeb.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RecipeSearchWeb.Services;

/// <summary>
/// Service for managing chat feedback and auto-learning
/// Stores feedback in Azure Blob Storage for analysis and improvement
/// </summary>
public class FeedbackService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<FeedbackService> _logger;
    private readonly ContextSearchService _contextService;
    
    private const string FeedbackBlobName = "chat-feedback.json";
    private const string ContainerName = "agent-context";
    
    private List<ChatFeedback> _feedbackCache = new();
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public FeedbackService(
        IConfiguration configuration,
        ContextSearchService contextService,
        ILogger<FeedbackService> logger)
    {
        _contextService = contextService;
        _logger = logger;
        
        var connectionString = configuration["AzureBlobStorage:ConnectionString"] 
            ?? configuration["AZURE_BLOB_STORAGE_CONNECTION_STRING"];
        
        if (!string.IsNullOrEmpty(connectionString))
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        }
        else
        {
            _logger.LogWarning("Azure Blob Storage not configured for feedback service");
            _containerClient = null!;
        }
    }

    /// <summary>
    /// Initialize service and load existing feedback
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;
            
            if (_containerClient != null)
            {
                await _containerClient.CreateIfNotExistsAsync();
                _feedbackCache = await LoadFeedbackAsync();
            }
            
            _isInitialized = true;
            _logger.LogInformation("FeedbackService initialized with {Count} feedback entries", _feedbackCache.Count);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Submit feedback for a chat response
    /// </summary>
    public async Task<ChatFeedback> SubmitFeedbackAsync(
        string query, 
        string response, 
        bool isHelpful, 
        string? comment = null,
        string? userId = null,
        string agentType = "General",
        double bestScore = 0,
        bool wasLowConfidence = false)
    {
        await InitializeAsync();
        
        var feedback = new ChatFeedback
        {
            Query = query,
            Response = response,
            IsHelpful = isHelpful,
            Comment = comment,
            UserId = userId,
            AgentType = agentType,
            BestSearchScore = bestScore,
            WasLowConfidence = wasLowConfidence,
            ExtractedKeywords = ExtractKeywords(query),
            Timestamp = DateTime.UtcNow
        };
        
        // If negative feedback, analyze and suggest improvements
        if (!isHelpful)
        {
            feedback.SuggestedKeywords = await AnalyzeAndSuggestKeywordsAsync(query, response);
        }
        
        _feedbackCache.Add(feedback);
        await SaveFeedbackAsync();
        
        _logger.LogInformation("Feedback submitted: {Rating} for query '{Query}' (Agent: {Agent})", 
            isHelpful ? "👍" : "👎", 
            query.Length > 50 ? query.Substring(0, 50) + "..." : query,
            agentType);
        
        return feedback;
    }

    /// <summary>
    /// Get all feedback entries
    /// </summary>
    public async Task<List<ChatFeedback>> GetAllFeedbackAsync()
    {
        await InitializeAsync();
        return _feedbackCache.OrderByDescending(f => f.Timestamp).ToList();
    }

    /// <summary>
    /// Get only negative feedback (for improvement analysis)
    /// </summary>
    public async Task<List<ChatFeedback>> GetNegativeFeedbackAsync()
    {
        await InitializeAsync();
        return _feedbackCache
            .Where(f => !f.IsHelpful)
            .OrderByDescending(f => f.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Get unreviewed feedback
    /// </summary>
    public async Task<List<ChatFeedback>> GetUnreviewedFeedbackAsync()
    {
        await InitializeAsync();
        return _feedbackCache
            .Where(f => !f.IsReviewed)
            .OrderByDescending(f => f.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Get feedback statistics
    /// </summary>
    public async Task<FeedbackStats> GetStatsAsync()
    {
        await InitializeAsync();
        
        var total = _feedbackCache.Count;
        var positive = _feedbackCache.Count(f => f.IsHelpful);
        var negative = _feedbackCache.Count(f => !f.IsHelpful);
        var unreviewed = _feedbackCache.Count(f => !f.IsReviewed);
        var lowConfidence = _feedbackCache.Count(f => f.WasLowConfidence);
        
        // Aggregate keyword suggestions from negative feedback
        var keywordGroups = _feedbackCache
            .Where(f => !f.IsHelpful && f.SuggestedKeywords.Any())
            .SelectMany(f => f.SuggestedKeywords.Select(k => new { Keyword = k, Query = f.Query }))
            .GroupBy(x => x.Keyword.ToLowerInvariant())
            .Select(g => new KeywordSuggestion
            {
                Keyword = g.Key,
                Frequency = g.Count(),
                RelatedQueries = g.Select(x => x.Query).Distinct().Take(5).ToList()
            })
            .OrderByDescending(k => k.Frequency)
            .Take(20)
            .ToList();
        
        return new FeedbackStats
        {
            TotalFeedback = total,
            PositiveFeedback = positive,
            NegativeFeedback = negative,
            SatisfactionRate = total > 0 ? (double)positive / total * 100 : 0,
            UnreviewedCount = unreviewed,
            LowConfidenceCount = lowConfidence,
            TopSuggestions = keywordGroups
        };
    }

    /// <summary>
    /// Mark feedback as reviewed
    /// </summary>
    public async Task MarkAsReviewedAsync(string feedbackId)
    {
        await InitializeAsync();
        
        var feedback = _feedbackCache.FirstOrDefault(f => f.Id == feedbackId);
        if (feedback != null)
        {
            feedback.IsReviewed = true;
            await SaveFeedbackAsync();
        }
    }

    /// <summary>
    /// Mark feedback improvement as applied
    /// </summary>
    public async Task MarkAsAppliedAsync(string feedbackId)
    {
        await InitializeAsync();
        
        var feedback = _feedbackCache.FirstOrDefault(f => f.Id == feedbackId);
        if (feedback != null)
        {
            feedback.IsApplied = true;
            feedback.IsReviewed = true;
            await SaveFeedbackAsync();
        }
    }

    /// <summary>
    /// Delete old feedback (cleanup)
    /// </summary>
    public async Task CleanupOldFeedbackAsync(int daysToKeep = 90)
    {
        await InitializeAsync();
        
        var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
        var removed = _feedbackCache.RemoveAll(f => f.Timestamp < cutoff && f.IsReviewed);
        
        if (removed > 0)
        {
            await SaveFeedbackAsync();
            _logger.LogInformation("Cleaned up {Count} old feedback entries", removed);
        }
    }

    #region Auto-Learning

    /// <summary>
    /// Extract keywords from a query for analysis
    /// </summary>
    private List<string> ExtractKeywords(string query)
    {
        // Stop words to ignore
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "que", "es", "el", "la", "los", "las", "un", "una", "de", "del", "en", "por", "para",
            "como", "cómo", "cual", "cuál", "donde", "dónde", "cuando", "cuándo", "quien", "quién",
            "what", "is", "the", "a", "an", "of", "in", "for", "to", "how", "which", "where", "when", "who",
            "me", "te", "se", "nos", "mi", "tu", "su", "este", "esta", "ese", "esa",
            "con", "sin", "sobre", "entre", "hasta", "desde", "hacia",
            "tengo", "necesito", "quiero", "puedo", "problema", "ayuda", "ticket"
        };
        
        var words = Regex.Split(query.ToLowerInvariant(), @"[\s\?\!\.\,\;\:\(\)]+")
            .Where(w => w.Length >= 3)
            .Where(w => !stopWords.Contains(w))
            .Distinct()
            .ToList();
        
        return words;
    }

    /// <summary>
    /// Analyze a failed query and suggest keywords to add to context documents
    /// </summary>
    private async Task<List<string>> AnalyzeAndSuggestKeywordsAsync(string query, string response)
    {
        var suggestions = new List<string>();
        var keywords = ExtractKeywords(query);
        
        try
        {
            await _contextService.InitializeAsync();
            
            // Search for the query to see what was found (or not found)
            var searchResults = await _contextService.SearchAsync(query, topResults: 5);
            
            // If we got low scores or no results, the keywords might be missing
            if (!searchResults.Any() || searchResults.All(r => r.SearchScore < 0.5))
            {
                // These keywords should be added to improve future matching
                suggestions.AddRange(keywords);
                
                // Also add common variations
                foreach (var kw in keywords)
                {
                    // Add with/without accents
                    var withoutAccents = RemoveAccents(kw);
                    if (withoutAccents != kw)
                        suggestions.Add(withoutAccents);
                }
            }
            else
            {
                // We found something but user said it wasn't helpful
                // Maybe the keywords need to be more specific
                var foundKeywords = searchResults
                    .SelectMany(r => r.Keywords?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
                    .Select(k => k.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToHashSet();
                
                // Suggest keywords that weren't in the found documents
                suggestions.AddRange(keywords.Where(k => !foundKeywords.Contains(k)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing query for keyword suggestions");
        }
        
        return suggestions.Distinct().ToList();
    }

    /// <summary>
    /// Remove accents from text
    /// </summary>
    private string RemoveAccents(string text)
    {
        return text
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
            .Replace("ü", "u");
    }

    #endregion

    #region Storage

    private async Task<List<ChatFeedback>> LoadFeedbackAsync()
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(FeedbackBlobName);
            
            if (!await blobClient.ExistsAsync())
                return new List<ChatFeedback>();
            
            var response = await blobClient.DownloadContentAsync();
            var json = response.Value.Content.ToString();
            
            return JsonSerializer.Deserialize<List<ChatFeedback>>(json) ?? new List<ChatFeedback>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading feedback from storage");
            return new List<ChatFeedback>();
        }
    }

    private async Task SaveFeedbackAsync()
    {
        if (_containerClient == null) return;
        
        try
        {
            var blobClient = _containerClient.GetBlobClient(FeedbackBlobName);
            var json = JsonSerializer.Serialize(_feedbackCache, new JsonSerializerOptions { WriteIndented = true });
            
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(stream, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving feedback to storage");
        }
    }

    #endregion
}
