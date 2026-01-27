using Azure.Storage.Blobs;
using OpenAI.Embeddings;
using RecipeSearchWeb.Interfaces;
using RecipeSearchWeb.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RecipeSearchWeb.Services;

/// <summary>
/// Service for managing chat feedback and auto-learning
/// Stores feedback in Azure Blob Storage for analysis and improvement
/// </summary>
public class FeedbackService : IFeedbackService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<FeedbackService> _logger;
    private readonly ContextSearchService _contextService;
    private readonly ContextStorageService _contextStorage;
    private readonly EmbeddingClient? _embeddingClient;
    
    private const string FeedbackBlobName = "chat-feedback.json";
    private const string SuccessfulResponsesBlobName = "successful-responses.json";
    private const string FailurePatternsBlobName = "failure-patterns.json";
    private const string AutoLearningLogBlobName = "auto-learning-log.json";
    private const string ContainerName = "agent-context";
    
    // Auto-learning configuration
    private readonly AutoLearningConfig _config = new();
    
    private List<ChatFeedback> _feedbackCache = new();
    private List<SuccessfulResponse> _successfulResponses = new();
    private List<FailurePattern> _failurePatterns = new();
    private List<string> _autoLearningLog = new();
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public FeedbackService(
        IConfiguration configuration,
        ContextSearchService contextService,
        ContextStorageService contextStorage,
        ILogger<FeedbackService> logger,
        EmbeddingClient? embeddingClient = null)
    {
        _contextService = contextService;
        _contextStorage = contextStorage;
        _logger = logger;
        _embeddingClient = embeddingClient;
        
        // Try multiple configuration keys for connection string (consistency with other services)
        var connectionString = configuration["AzureStorage:ConnectionString"]
            ?? configuration["AzureBlobStorage:ConnectionString"] 
            ?? configuration["AZURE_BLOB_STORAGE_CONNECTION_STRING"]
            ?? configuration.GetConnectionString("AzureBlobStorage");
        
        _logger.LogInformation("FeedbackService: Checking Azure Blob Storage configuration...");
        
        if (!string.IsNullOrEmpty(connectionString) && 
            connectionString != "SET_IN_AZURE_APP_SERVICE_CONFIGURATION")
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(connectionString);
                _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
                _logger.LogInformation("FeedbackService: Azure Blob Storage configured successfully for container '{Container}'", ContainerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FeedbackService: Failed to create BlobServiceClient");
                _containerClient = null!;
            }
        }
        else
        {
            _logger.LogWarning("FeedbackService: Azure Blob Storage NOT configured. Feedback will NOT persist!");
            _containerClient = null!;
        }
    }

    /// <summary>
    /// Initialize service and load existing feedback from Azure Blob Storage
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;
            
            if (_containerClient == null)
            {
                _logger.LogWarning("FeedbackService: Cannot initialize - no container client configured");
                _isInitialized = true; // Mark as initialized to avoid repeated attempts
                return;
            }
            
            try
            {
                // Ensure container exists
                await _containerClient.CreateIfNotExistsAsync();
                _logger.LogInformation("FeedbackService: Container '{Container}' ready", ContainerName);
                
                // Load all data from blob storage
                _feedbackCache = await LoadFeedbackAsync();
                _successfulResponses = await LoadSuccessfulResponsesAsync();
                _failurePatterns = await LoadFailurePatternsAsync();
                _autoLearningLog = await LoadAutoLearningLogAsync();
                
                _logger.LogInformation(
                    "FeedbackService INITIALIZED: {Feedback} feedback entries, {Success} cached responses, {Failures} failure patterns, {Log} log entries", 
                    _feedbackCache.Count, _successfulResponses.Count, _failurePatterns.Count, _autoLearningLog.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FeedbackService: Error during initialization - starting with empty caches");
                _feedbackCache = new List<ChatFeedback>();
                _successfulResponses = new List<SuccessfulResponse>();
                _failurePatterns = new List<FailurePattern>();
                _autoLearningLog = new List<string>();
            }
            
            _isInitialized = true;
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
        
        if (isHelpful)
        {
            // POSITIVE FEEDBACK: Cache successful response for future use
            await CacheSuccessfulResponseAsync(query, response, agentType);
        }
        else
        {
            // NEGATIVE FEEDBACK: Analyze, suggest improvements, track patterns
            feedback.SuggestedKeywords = await AnalyzeAndSuggestKeywordsAsync(query, response);
            await TrackFailurePatternAsync(query, feedback.SuggestedKeywords);
            
            // Try auto-enrichment if threshold reached
            await TryAutoEnrichKeywordsAsync();
        }
        
        _feedbackCache.Add(feedback);
        await SaveFeedbackAsync();
        
        _logger.LogInformation("Feedback submitted: {Rating} for query '{Query}' (Agent: {Agent}, Total: {Total})", 
            isHelpful ? "👍" : "👎", 
            query.Length > 50 ? query.Substring(0, 50) + "..." : query,
            agentType,
            _feedbackCache.Count);
        
        return feedback;
    }
    
    /// <summary>
    /// Submit feedback with user correction (for negative feedback)
    /// Automatically enriches context documents with the correction
    /// </summary>
    public async Task<ChatFeedback> SubmitFeedbackWithCorrectionAsync(
        string query,
        string response,
        string userCorrection,
        List<string> sourcesUsed,
        string? userId = null,
        string agentType = "General",
        double bestScore = 0,
        bool wasLowConfidence = false)
    {
        await InitializeAsync();
        
        _logger.LogInformation("Feedback with correction submitted for query: '{Query}' (Length: {Length} chars)", 
            query.Length > 50 ? query.Substring(0, 50) + "..." : query,
            userCorrection.Length);
        
        try
        {
            var feedback = new ChatFeedback
            {
                Query = query,
                Response = response,
                IsHelpful = false, // Always false when correction is provided
                UserCorrection = userCorrection,
                SourcesUsed = sourcesUsed,
                UserId = userId,
                AgentType = agentType,
                BestSearchScore = bestScore,
                WasLowConfidence = wasLowConfidence,
                ExtractedKeywords = ExtractKeywords(query),
                Timestamp = DateTime.UtcNow
            };
            
            // CRITICAL: Enrich context documents with user correction
            await EnrichContextFromCorrectionAsync(feedback);
            
            // Track failure pattern for analysis
            feedback.SuggestedKeywords = await AnalyzeAndSuggestKeywordsAsync(query, response);
            await TrackFailurePatternAsync(query, feedback.SuggestedKeywords);
            
            // Save feedback
            _feedbackCache.Add(feedback);
            await SaveFeedbackAsync();
            
            _logger.LogInformation(
                "✅ Feedback with correction saved and context enriched. Total feedback: {Total}", 
                _feedbackCache.Count);
            
            return feedback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing feedback with correction for query: '{Query}'", query);
            throw;
        }
    }
    
    /// <summary>
    /// Enrich context documents from user correction
    /// Creates a new context document with the user's correct answer
    /// </summary>
    private async Task EnrichContextFromCorrectionAsync(ChatFeedback feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback.UserCorrection))
        {
            _logger.LogWarning("Cannot enrich context: UserCorrection is empty");
            return;
        }
        
        try
        {
            _logger.LogInformation("📝 Enriching context from user correction...");
            
            // Create a new context document from the correction
            var contextDoc = new ContextDocument
            {
                SourceFile = "user-corrections",
                Name = $"User Correction - {feedback.Query.Substring(0, Math.Min(50, feedback.Query.Length))}",
                Description = feedback.UserCorrection,
                Keywords = string.Join(", ", feedback.ExtractedKeywords),
                Category = feedback.AgentType
            };
            
            // Add source context if available in AdditionalData
            if (feedback.SourcesUsed.Any())
            {
                var tickets = feedback.SourcesUsed
                    .Where(s => s.Contains("-")) // Ticket format: MT-12345
                    .ToList();
                if (tickets.Any())
                {
                    contextDoc.AdditionalData["RelatedTickets"] = string.Join(", ", tickets);
                }
            }
            
            // Generate embeddings for the document
            var embeddingText = $"{contextDoc.Name} {contextDoc.Description} {contextDoc.Keywords}";
            if (_embeddingClient != null)
            {
                var embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(embeddingText);
                contextDoc.Embedding = embeddingResult.Value.ToFloats().ToArray();
            }
            else
            {
                _logger.LogWarning("EmbeddingClient not available, document saved without embeddings");
            }
            
            // Load existing documents, add new one, and save
            var allDocs = await _contextStorage.LoadDocumentsAsync();
            allDocs.Add(contextDoc);
            await _contextStorage.SaveDocumentsAsync(allDocs);
            
            _logger.LogInformation(
                "✅ Context document created from user correction: '{DocName}' (Keywords: {Keywords})",
                contextDoc.Name,
                contextDoc.Keywords);
            
            // Log the auto-learning event
            var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Auto-enriched context from user correction - Query: \"{feedback.Query}\" → Doc: \"{contextDoc.Name}\"";
            _autoLearningLog.Add(logEntry);
            await SaveAutoLearningLogAsync();
            
            // Force Hot Reload: Refresh context service to include the new document
            _logger.LogInformation("🔄 Forcing Context Service refresh for Hot Reload...");
            await _contextService.InitializeAsync();
            _logger.LogInformation("✅ Hot Reload completed - New knowledge is now available in memory");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error enriching context from user correction");
            throw;
        }
    }
    
    /// <summary>
    /// Check if the service is properly configured and can persist data
    /// </summary>
    public async Task<(bool IsOperational, string Message)> CheckHealthAsync()
    {
        if (_containerClient == null)
        {
            return (false, "Azure Blob Storage not configured - feedback will NOT persist!");
        }
        
        try
        {
            // Test write capability
            var testBlobName = $"health-check-{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
            var blobClient = _containerClient.GetBlobClient(testBlobName);
            
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Health check OK"));
            await blobClient.UploadAsync(stream, overwrite: true);
            await blobClient.DeleteAsync();
            
            await InitializeAsync();
            
            return (true, $"Operational - {_feedbackCache.Count} feedback, {_successfulResponses.Count} cached responses, {_failurePatterns.Count} patterns");
        }
        catch (Exception ex)
        {
            return (false, $"Azure Blob Storage error: {ex.Message}");
        }
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
        
        // Count auto-enriched keywords from the log
        var autoEnriched = _autoLearningLog.Count(l => l.Contains("Auto-enriched"));
        
        return new FeedbackStats
        {
            TotalFeedback = total,
            PositiveFeedback = positive,
            NegativeFeedback = negative,
            SatisfactionRate = total > 0 ? (double)positive / total * 100 : 0,
            UnreviewedCount = unreviewed,
            LowConfidenceCount = lowConfidence,
            TopSuggestions = keywordGroups,
            FailurePatterns = _failurePatterns.Where(p => p.FailureCount >= _config.FailureAlertThreshold).ToList(),
            AutoEnrichedKeywords = autoEnriched,
            CachedSuccessfulResponses = _successfulResponses.Count
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
    /// Dismiss feedback (not relevant for training, skip it)
    /// </summary>
    public async Task DismissFeedbackAsync(string feedbackId)
    {
        await InitializeAsync();
        
        var feedback = _feedbackCache.FirstOrDefault(f => f.Id == feedbackId);
        if (feedback != null)
        {
            feedback.IsDismissed = true;
            feedback.IsReviewed = true; // Mark as reviewed so it doesn't appear in pending
            await SaveFeedbackAsync();
            _logger.LogInformation("Feedback {Id} dismissed", feedbackId);
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

    #region Auto-Enrichment (Feature 1)

    /// <summary>
    /// Try to auto-enrich keywords when threshold is reached
    /// </summary>
    private async Task TryAutoEnrichKeywordsAsync()
    {
        try
        {
            // Get keyword frequencies from negative feedback
            var keywordFrequencies = _feedbackCache
                .Where(f => !f.IsHelpful && f.SuggestedKeywords.Any())
                .SelectMany(f => f.SuggestedKeywords)
                .GroupBy(k => k.ToLowerInvariant())
                .Where(g => g.Count() >= _config.KeywordEnrichmentThreshold)
                .Select(g => new { Keyword = g.Key, Count = g.Count() })
                .ToList();

            if (!keywordFrequencies.Any()) return;

            await _contextService.InitializeAsync();
            await _contextStorage.InitializeAsync();

            var allDocuments = await _contextStorage.LoadDocumentsAsync();
            var documentsModified = false;

            foreach (var kw in keywordFrequencies)
            {
                // Check if already auto-applied
                if (_autoLearningLog.Any(l => l.Contains($"Auto-enriched: {kw.Keyword}")))
                    continue;

                // Find the best context document to add this keyword
                var searchResults = await _contextService.SearchAsync(kw.Keyword, topResults: 3);
                var bestMatch = searchResults.FirstOrDefault();

                if (bestMatch != null && bestMatch.SearchScore > 0.3)
                {
                    // Find the actual document in storage
                    var document = allDocuments.FirstOrDefault(d => d.Id == bestMatch.Id);
                    if (document == null) continue;

                    // Check if keyword already exists
                    var existingKeywords = document.Keywords?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim().ToLowerInvariant())
                        .ToHashSet() ?? new HashSet<string>();

                    if (!existingKeywords.Contains(kw.Keyword))
                    {
                        // Update the document's keywords
                        document.Keywords = string.IsNullOrEmpty(document.Keywords)
                            ? kw.Keyword
                            : $"{document.Keywords}, {kw.Keyword}";
                        
                        documentsModified = true;

                        // Log the auto-enrichment
                        var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] Auto-enriched: {kw.Keyword} -> Document: {document.Name} (occurrences: {kw.Count})";
                        _autoLearningLog.Add(logEntry);

                        _logger.LogInformation("Auto-enriched keyword '{Keyword}' to document '{Document}'", kw.Keyword, document.Name);
                    }
                }
            }

            // Save all modified documents at once
            if (documentsModified)
            {
                await _contextStorage.SaveDocumentsAsync(allDocuments);
                // Note: Embeddings will be recalculated on next service restart or can be triggered manually
                await SaveAutoLearningLogAsync();
                
                _logger.LogInformation("Auto-enrichment complete. Documents saved. Restart app or re-import to update embeddings.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-enrichment");
        }
    }

    /// <summary>
    /// Get auto-learning activity log
    /// </summary>
    public async Task<List<string>> GetAutoLearningLogAsync()
    {
        await InitializeAsync();
        return _autoLearningLog.OrderByDescending(l => l).ToList();
    }

    #endregion

    #region Successful Response Cache (Feature 2)

    /// <summary>
    /// Cache a successful query-response pair for future semantic matching
    /// </summary>
    private async Task CacheSuccessfulResponseAsync(string query, string response, string agentType)
    {
        try
        {
            // Check if similar query already cached
            var existing = _successfulResponses.FirstOrDefault(r => 
                r.Query.Equals(query, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.UseCount++;
                existing.LastUsedAt = DateTime.UtcNow;
            }
            else
            {
                var cached = new SuccessfulResponse
                {
                    Query = query,
                    Response = response,
                    AgentType = agentType,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                };

                // Generate embedding for semantic matching
                if (_embeddingClient != null)
                {
                    try
                    {
                        var embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(query);
                        cached.QueryEmbedding = embeddingResult.Value.ToFloats().ToArray();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate embedding for cached response");
                    }
                }

                _successfulResponses.Add(cached);

                // Limit cache size
                if (_successfulResponses.Count > _config.MaxCachedResponses)
                {
                    // Remove least used/oldest entries
                    var toRemove = _successfulResponses
                        .OrderBy(r => r.UseCount)
                        .ThenBy(r => r.LastUsedAt)
                        .Take(_successfulResponses.Count - _config.MaxCachedResponses)
                        .ToList();
                    
                    foreach (var r in toRemove)
                        _successfulResponses.Remove(r);
                }
            }

            await SaveSuccessfulResponsesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching successful response");
        }
    }

    /// <summary>
    /// Try to find a cached response that semantically matches the query
    /// </summary>
    public async Task<SuccessfulResponse?> GetCachedResponseAsync(string query)
    {
        await InitializeAsync();

        if (_embeddingClient == null || !_successfulResponses.Any(r => r.QueryEmbedding.Length > 0))
            return null;

        try
        {
            var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.Value.ToFloats().ToArray();

            SuccessfulResponse? bestMatch = null;
            double bestSimilarity = 0;

            foreach (var cached in _successfulResponses.Where(r => r.QueryEmbedding.Length > 0))
            {
                var similarity = CosineSimilarity(queryVector, cached.QueryEmbedding);
                if (similarity > bestSimilarity && similarity >= _config.CachedResponseSimilarityThreshold)
                {
                    bestSimilarity = similarity;
                    bestMatch = cached;
                }
            }

            if (bestMatch != null)
            {
                bestMatch.UseCount++;
                bestMatch.LastUsedAt = DateTime.UtcNow;
                await SaveSuccessfulResponsesAsync();
                
                _logger.LogInformation("Found cached response (similarity: {Sim:F3}) for query: {Query}", 
                    bestSimilarity, query.Length > 50 ? query.Substring(0, 50) + "..." : query);
            }

            return bestMatch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching cached responses");
            return null;
        }
    }

    /// <summary>
    /// Get all cached successful responses
    /// </summary>
    public async Task<List<SuccessfulResponse>> GetCachedResponsesAsync()
    {
        await InitializeAsync();
        return _successfulResponses.OrderByDescending(r => r.UseCount).ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        
        double dotProduct = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        return normA > 0 && normB > 0 ? dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB)) : 0;
    }

    #endregion

    #region Failure Pattern Tracking (Feature 3)

    /// <summary>
    /// Track failure patterns for alerting
    /// </summary>
    private async Task TrackFailurePatternAsync(string query, List<string> keywords)
    {
        try
        {
            // Create a pattern signature from keywords
            var patternKey = string.Join("+", keywords.OrderBy(k => k).Take(3));
            if (string.IsNullOrEmpty(patternKey)) 
                patternKey = "general-failure";

            var existingPattern = _failurePatterns.FirstOrDefault(p => 
                p.PatternDescription == patternKey);

            if (existingPattern != null)
            {
                existingPattern.FailureCount++;
                existingPattern.LastOccurrence = DateTime.UtcNow;
                if (!existingPattern.SampleQueries.Contains(query) && existingPattern.SampleQueries.Count < 10)
                    existingPattern.SampleQueries.Add(query);
                
                // Check if we need to create an alert
                if (existingPattern.FailureCount >= _config.FailureAlertThreshold && !existingPattern.IsAlerted)
                {
                    existingPattern.IsAlerted = true;
                    existingPattern.SuggestedAction = SuggestActionForPattern(existingPattern);
                    
                    var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] ALERT: Failure pattern '{patternKey}' reached {existingPattern.FailureCount} occurrences";
                    _autoLearningLog.Add(logEntry);
                    
                    _logger.LogWarning("Failure pattern alert: '{Pattern}' with {Count} failures", 
                        patternKey, existingPattern.FailureCount);
                }
            }
            else
            {
                _failurePatterns.Add(new FailurePattern
                {
                    PatternDescription = patternKey,
                    SampleQueries = new List<string> { query },
                    FailureCount = 1,
                    FirstOccurrence = DateTime.UtcNow,
                    LastOccurrence = DateTime.UtcNow
                });
            }

            await SaveFailurePatternsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking failure pattern");
        }
    }

    /// <summary>
    /// Suggest an action for a failure pattern
    /// </summary>
    private string SuggestActionForPattern(FailurePattern pattern)
    {
        var keywords = pattern.PatternDescription.Split('+');
        
        if (keywords.Any(k => k.Contains("sap") || k.Contains("transaccion")))
            return "Consider adding SAP documentation for these terms or updating the SAP agent routing.";
        
        if (keywords.Any(k => k.Contains("vpn") || k.Contains("red") || k.Contains("network")))
            return "Consider adding Network documentation or updating the Network agent context.";
        
        if (pattern.SampleQueries.Any(q => q.Length < 20))
            return "Users are asking very short questions. Consider improving disambiguation prompts.";
        
        return $"Consider creating documentation covering: {string.Join(", ", keywords)}";
    }

    /// <summary>
    /// Get all failure patterns
    /// </summary>
    public async Task<List<FailurePattern>> GetFailurePatternsAsync()
    {
        await InitializeAsync();
        return _failurePatterns.OrderByDescending(p => p.FailureCount).ToList();
    }

    /// <summary>
    /// Dismiss a failure pattern alert
    /// </summary>
    public async Task DismissFailurePatternAsync(string patternId)
    {
        var pattern = _failurePatterns.FirstOrDefault(p => p.Id == patternId);
        if (pattern != null)
        {
            pattern.IsAlerted = false;
            pattern.FailureCount = 0; // Reset count
            await SaveFailurePatternsAsync();
        }
    }

    #endregion

    #region Storage

    private async Task<List<ChatFeedback>> LoadFeedbackAsync()
    {
        return await LoadJsonAsync<List<ChatFeedback>>(FeedbackBlobName) ?? new List<ChatFeedback>();
    }

    private async Task<List<SuccessfulResponse>> LoadSuccessfulResponsesAsync()
    {
        return await LoadJsonAsync<List<SuccessfulResponse>>(SuccessfulResponsesBlobName) ?? new List<SuccessfulResponse>();
    }

    private async Task<List<FailurePattern>> LoadFailurePatternsAsync()
    {
        return await LoadJsonAsync<List<FailurePattern>>(FailurePatternsBlobName) ?? new List<FailurePattern>();
    }

    private async Task<List<string>> LoadAutoLearningLogAsync()
    {
        return await LoadJsonAsync<List<string>>(AutoLearningLogBlobName) ?? new List<string>();
    }

    private async Task<T?> LoadJsonAsync<T>(string blobName) where T : class
    {
        if (_containerClient == null)
        {
            _logger.LogWarning("FeedbackService: Cannot load {BlobName} - no container client", blobName);
            return null;
        }
        
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogInformation("FeedbackService: Blob {BlobName} does not exist yet (will be created on first save)", blobName);
                return null;
            }
            
            var response = await blobClient.DownloadContentAsync();
            var json = response.Value.Content.ToString();
            
            var result = JsonSerializer.Deserialize<T>(json);
            _logger.LogDebug("FeedbackService: Loaded {BlobName} ({Bytes} bytes)", blobName, json.Length);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FeedbackService: Error loading {BlobName} from storage", blobName);
            return null;
        }
    }

    private async Task SaveFeedbackAsync()
    {
        await SaveJsonAsync(FeedbackBlobName, _feedbackCache, "feedback");
    }

    private async Task SaveSuccessfulResponsesAsync()
    {
        await SaveJsonAsync(SuccessfulResponsesBlobName, _successfulResponses, "successful responses");
    }

    private async Task SaveFailurePatternsAsync()
    {
        await SaveJsonAsync(FailurePatternsBlobName, _failurePatterns, "failure patterns");
    }

    private async Task SaveAutoLearningLogAsync()
    {
        await SaveJsonAsync(AutoLearningLogBlobName, _autoLearningLog, "auto-learning log");
    }

    private async Task SaveJsonAsync<T>(string blobName, T data, string description = "data")
    {
        if (_containerClient == null)
        {
            _logger.LogWarning("FeedbackService: Cannot save {Description} - no container client", description);
            return;
        }
        
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(stream, overwrite: true);
            
            _logger.LogDebug("FeedbackService: Saved {Description} to {BlobName} ({Bytes} bytes)", 
                description, blobName, json.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FeedbackService: ERROR saving {Description} to {BlobName}", description, blobName);
            throw; // Re-throw to notify caller of failure
        }
    }

    #endregion
}
