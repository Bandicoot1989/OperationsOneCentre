using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RecipeSearchWeb.Services;

/// <summary>
/// Service for caching query results to improve response times
/// Tier 2 Optimization: Intelligent Caching
/// </summary>
public class QueryCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<QueryCacheService> _logger;
    
    // Cache configuration
    private readonly TimeSpan _queryResultExpiration = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _embeddingExpiration = TimeSpan.FromHours(24);
    private readonly TimeSpan _searchResultExpiration = TimeSpan.FromMinutes(15);
    
    // Cache statistics
    private int _cacheHits = 0;
    private int _cacheMisses = 0;

    public QueryCacheService(IMemoryCache cache, ILogger<QueryCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
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
            HitRate = _cacheHits + _cacheMisses > 0 
                ? (double)_cacheHits / (_cacheHits + _cacheMisses) * 100 
                : 0
        };
    }
    
    /// <summary>
    /// Clear all caches
    /// </summary>
    public void ClearCache()
    {
        // MemoryCache doesn't have a Clear method, but entries will expire
        // In production, you might want to use a distributed cache with Clear capability
        _cacheHits = 0;
        _cacheMisses = 0;
        _logger.LogInformation("Cache statistics reset");
    }
    
    #endregion
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
    public double HitRate { get; set; }
}
