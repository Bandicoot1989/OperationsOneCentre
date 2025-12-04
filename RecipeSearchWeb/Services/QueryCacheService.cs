using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenAI.Embeddings;

namespace RecipeSearchWeb.Services;

/// <summary>
/// Service for caching query results to improve response times
/// Tier 2 Optimization: Intelligent Caching with Semantic Similarity
/// </summary>
public class QueryCacheService
{
    private readonly IMemoryCache _cache;
    private readonly EmbeddingClient? _embeddingClient;
    private readonly ILogger<QueryCacheService> _logger;
    
    // Cache configuration
    private readonly TimeSpan _queryResultExpiration = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _embeddingExpiration = TimeSpan.FromHours(24);
    private readonly TimeSpan _searchResultExpiration = TimeSpan.FromMinutes(15);
    
    // Semantic cache configuration
    private const double SemanticSimilarityThreshold = 0.95;
    private readonly List<SemanticCacheEntry> _semanticCache = new();
    private readonly object _semanticCacheLock = new();
    private const int MaxSemanticCacheEntries = 500;
    
    // Cache statistics
    private int _cacheHits = 0;
    private int _cacheMisses = 0;
    private int _semanticCacheHits = 0;

    public QueryCacheService(IMemoryCache cache, ILogger<QueryCacheService> logger, EmbeddingClient? embeddingClient = null)
    {
        _cache = cache;
        _logger = logger;
        _embeddingClient = embeddingClient;
        
        _logger.LogInformation("QueryCacheService initialized (Semantic caching: {Enabled})", 
            _embeddingClient != null ? "Enabled" : "Disabled");
    }

    #region Query Result Cache
    
    /// <summary>
    /// Get cached response for a similar query
    /// </summary>
    public CachedQueryResult? GetCachedResponse(string query)
    {
        var normalizedQuery = NormalizeQuery(query);
        var cacheKey = $"query_result:{GetQueryHash(normalizedQuery)}";
        
        if (_cache.TryGetValue(cacheKey, out CachedQueryResult? cached))
        {
            _cacheHits++;
            _logger.LogInformation("Cache HIT for query: '{Query}' (Hash: {Hash})", 
                query.Length > 50 ? query.Substring(0, 50) + "..." : query, 
                cacheKey.Substring(cacheKey.Length - 8));
            return cached;
        }
        
        _cacheMisses++;
        return null;
    }
    
    /// <summary>
    /// Cache a query response
    /// </summary>
    public void CacheResponse(string query, string response, List<string> sources)
    {
        var normalizedQuery = NormalizeQuery(query);
        var cacheKey = $"query_result:{GetQueryHash(normalizedQuery)}";
        
        var cached = new CachedQueryResult
        {
            Query = query,
            NormalizedQuery = normalizedQuery,
            Response = response,
            Sources = sources,
            CachedAt = DateTime.UtcNow
        };
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _queryResultExpiration,
            SlidingExpiration = TimeSpan.FromMinutes(10),
            Priority = CacheItemPriority.Normal
        };
        
        _cache.Set(cacheKey, cached, options);
        _logger.LogInformation("Cached response for query (Hash: {Hash}), expires in {Minutes} min", 
            cacheKey.Substring(cacheKey.Length - 8), _queryResultExpiration.TotalMinutes);
    }
    
    #endregion
    
    #region Embedding Cache
    
    /// <summary>
    /// Get cached embedding for text
    /// </summary>
    public float[]? GetCachedEmbedding(string text)
    {
        var cacheKey = $"embedding:{GetQueryHash(text)}";
        
        if (_cache.TryGetValue(cacheKey, out float[]? embedding))
        {
            return embedding;
        }
        
        return null;
    }
    
    /// <summary>
    /// Cache an embedding
    /// </summary>
    public void CacheEmbedding(string text, float[] embedding)
    {
        var cacheKey = $"embedding:{GetQueryHash(text)}";
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _embeddingExpiration,
            Priority = CacheItemPriority.Low // Embeddings can be recalculated
        };
        
        _cache.Set(cacheKey, embedding, options);
    }
    
    #endregion
    
    #region Search Result Cache
    
    /// <summary>
    /// Get cached search results
    /// </summary>
    public List<T>? GetCachedSearchResults<T>(string searchType, string query) where T : class
    {
        var cacheKey = $"search:{searchType}:{GetQueryHash(query)}";
        
        if (_cache.TryGetValue(cacheKey, out List<T>? results))
        {
            _logger.LogDebug("Search cache HIT for {Type}: '{Query}'", searchType, 
                query.Length > 30 ? query.Substring(0, 30) + "..." : query);
            return results;
        }
        
        return null;
    }
    
    /// <summary>
    /// Cache search results
    /// </summary>
    public void CacheSearchResults<T>(string searchType, string query, List<T> results) where T : class
    {
        var cacheKey = $"search:{searchType}:{GetQueryHash(query)}";
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _searchResultExpiration,
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Priority = CacheItemPriority.Normal
        };
        
        _cache.Set(cacheKey, results, options);
    }
    
    #endregion
    
    #region Utilities
    
    /// <summary>
    /// Normalize query for better cache hits
    /// </summary>
    private string NormalizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return string.Empty;
        
        // Lowercase
        var normalized = query.ToLowerInvariant().Trim();
        
        // Remove punctuation except essential ones
        normalized = new string(normalized
            .Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '?' || c == '¿')
            .ToArray());
        
        // Collapse multiple spaces
        while (normalized.Contains("  "))
            normalized = normalized.Replace("  ", " ");
        
        // Remove common filler words (Spanish & English)
        var fillers = new[] { " por favor ", " please ", " gracias ", " thanks ", " porfavor " };
        foreach (var filler in fillers)
        {
            normalized = normalized.Replace(filler, " ");
        }
        
        return normalized.Trim();
    }
    
    /// <summary>
    /// Generate consistent hash for cache key
    /// </summary>
    private string GetQueryHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).Substring(0, 16); // First 16 chars for brevity
    }
    
    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            Hits = _cacheHits,
            Misses = _cacheMisses,
            SemanticHits = _semanticCacheHits,
            HitRate = _cacheHits + _cacheMisses > 0 
                ? (double)_cacheHits / (_cacheHits + _cacheMisses) * 100 
                : 0,
            SemanticCacheSize = _semanticCache.Count
        };
    }
    
    /// <summary>
    /// Clear all caches
    /// </summary>
    public void ClearCache()
    {
        lock (_semanticCacheLock)
        {
            _semanticCache.Clear();
        }
        _cacheHits = 0;
        _cacheMisses = 0;
        _semanticCacheHits = 0;
        _logger.LogInformation("Cache statistics and semantic cache reset");
    }
    
    #endregion
    
    #region Semantic Cache
    
    /// <summary>
    /// Try to find a semantically similar cached response
    /// Returns null if no match found above threshold
    /// </summary>
    public async Task<CachedQueryResult?> GetSemanticallyCachedResponseAsync(string query)
    {
        if (_embeddingClient == null)
        {
            return null;
        }
        
        try
        {
            // Generate embedding for the query
            var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.Value.ToFloats();
            
            lock (_semanticCacheLock)
            {
                // Find the most similar cached query
                SemanticCacheEntry? bestMatch = null;
                double bestSimilarity = 0;
                
                foreach (var entry in _semanticCache)
                {
                    // Skip expired entries
                    if (DateTime.UtcNow - entry.CachedAt > _queryResultExpiration)
                        continue;
                    
                    var similarity = CosineSimilarity(queryVector, entry.QueryEmbedding);
                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestMatch = entry;
                    }
                }
                
                if (bestMatch != null && bestSimilarity >= SemanticSimilarityThreshold)
                {
                    _semanticCacheHits++;
                    _cacheHits++;
                    _logger.LogInformation("Semantic cache HIT: '{Query}' matched '{CachedQuery}' (similarity: {Similarity:F4})",
                        query.Length > 40 ? query.Substring(0, 40) + "..." : query,
                        bestMatch.Query.Length > 40 ? bestMatch.Query.Substring(0, 40) + "..." : bestMatch.Query,
                        bestSimilarity);
                    
                    return new CachedQueryResult
                    {
                        Query = bestMatch.Query,
                        NormalizedQuery = bestMatch.Query.ToLowerInvariant(),
                        Response = bestMatch.Response,
                        Sources = bestMatch.Sources,
                        CachedAt = bestMatch.CachedAt
                    };
                }
            }
            
            _cacheMisses++;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in semantic cache lookup");
            return null;
        }
    }
    
    /// <summary>
    /// Add a response to the semantic cache
    /// </summary>
    public async Task AddToSemanticCacheAsync(string query, string response, List<string> sources)
    {
        if (_embeddingClient == null)
        {
            return;
        }
        
        try
        {
            // Generate embedding for the query
            var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.Value.ToFloats();
            
            lock (_semanticCacheLock)
            {
                // Remove expired entries and maintain max size
                var now = DateTime.UtcNow;
                _semanticCache.RemoveAll(e => now - e.CachedAt > _queryResultExpiration);
                
                if (_semanticCache.Count >= MaxSemanticCacheEntries)
                {
                    // Remove oldest entries (FIFO)
                    var toRemove = _semanticCache.Count - MaxSemanticCacheEntries + 1;
                    _semanticCache.RemoveRange(0, toRemove);
                }
                
                // Check if similar query already cached
                var existingSimilar = _semanticCache
                    .Any(e => CosineSimilarity(queryVector, e.QueryEmbedding) > SemanticSimilarityThreshold);
                
                if (!existingSimilar)
                {
                    _semanticCache.Add(new SemanticCacheEntry
                    {
                        Query = query,
                        QueryEmbedding = queryVector,
                        Response = response,
                        Sources = sources,
                        CachedAt = DateTime.UtcNow
                    });
                    
                    _logger.LogInformation("Added to semantic cache: '{Query}' (cache size: {Size})",
                        query.Length > 40 ? query.Substring(0, 40) + "..." : query,
                        _semanticCache.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error adding to semantic cache");
        }
    }
    
    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private double CosineSimilarity(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
    {
        var spanA = a.Span;
        var spanB = b.Span;
        
        if (spanA.Length != spanB.Length || spanA.Length == 0)
            return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < spanA.Length; i++)
        {
            dotProduct += spanA[i] * spanB[i];
            normA += spanA[i] * spanA[i];
            normB += spanB[i] * spanB[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
    
    #endregion
}

/// <summary>
/// Entry in the semantic cache
/// </summary>
public class SemanticCacheEntry
{
    public string Query { get; set; } = string.Empty;
    public ReadOnlyMemory<float> QueryEmbedding { get; set; }
    public string Response { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new();
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// Cached query result
/// </summary>
public class CachedQueryResult
{
    public string Query { get; set; } = string.Empty;
    public string NormalizedQuery { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new();
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    public int Hits { get; set; }
    public int Misses { get; set; }
    public int SemanticHits { get; set; }
    public double HitRate { get; set; }
    public int SemanticCacheSize { get; set; }
}
