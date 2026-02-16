using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using OpenAI.Embeddings;
using OperationsOneCentre.Interfaces;
using OperationsOneCentre.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OperationsOneCentre.Services;

/// <summary>
/// Service for accessing Atlassian Confluence pages as knowledge base
/// Uses Confluence REST API with Basic Auth or API Token
/// </summary>
public class ConfluenceKnowledgeService : IConfluenceService
{
    private readonly IConfiguration _configuration;
    private readonly EmbeddingClient _embeddingClient;
    private readonly BlobContainerClient? _containerClient;
    private readonly ILogger<ConfluenceKnowledgeService> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private List<ConfluencePage> _pages = new();
    
    private const string CacheBlobName = "confluence-kb-cache.json";
    
    // Confluence configuration
    private readonly string? _baseUrl;
    private readonly string? _username;
    private readonly string? _apiToken;
    private readonly string[]? _spaceKeys;

    public bool IsConfigured => !string.IsNullOrEmpty(_baseUrl) && 
                                 !string.IsNullOrEmpty(_username) && 
                                 !string.IsNullOrEmpty(_apiToken);

    public int GetCachedPageCount() => _pages.Count;
    
    public List<ConfluencePage> GetAllCachedPages() => _pages;
    
    /// <summary>
    /// Get raw API response for a specific page ID (for debugging)
    /// </summary>
    public async Task<string> GetRawPageContentAsync(string pageId)
    {
        if (!IsConfigured) return "Not configured";
        
        try
        {
            var url = $"/wiki/rest/api/content/{pageId}?expand=body.storage,body.view";
            var response = await _httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Get page ID by title
    /// </summary>
    public string? GetPageIdByTitle(string title)
    {
        return _pages.FirstOrDefault(p => p.Title.Contains(title, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    public ConfluenceKnowledgeService(
        IConfiguration configuration,
        EmbeddingClient embeddingClient,
        IHttpClientFactory httpClientFactory,
        ILogger<ConfluenceKnowledgeService> logger)
    {
        _configuration = configuration;
        _embeddingClient = embeddingClient;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Confluence");

        // Confluence configuration
        _baseUrl = configuration["Confluence:BaseUrl"]?.TrimEnd('/');
        _username = configuration["Confluence:Email"]; // Email is the username for Atlassian Cloud
        
        // Try Base64 encoded token first (to handle special characters like '=')
        var base64Token = configuration["Confluence:ApiTokenBase64"];
        if (!string.IsNullOrEmpty(base64Token))
        {
            try
            {
                _apiToken = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Token));
                _logger.LogInformation("Confluence API token loaded from Base64 encoding");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decode Base64 API token");
                _apiToken = null;
            }
        }
        
        // Fallback to Connection String or direct App Setting
        _apiToken ??= configuration.GetConnectionString("ConfluenceApiToken") 
                      ?? configuration["Confluence:ApiToken"];
        
        // Log configuration status
        _logger.LogInformation("Confluence configuration - BaseUrl: {BaseUrl}, Email: {Email}, Token: {HasToken}, IsConfigured: {IsConfigured}",
            _baseUrl ?? "NOT SET",
            _username ?? "NOT SET", 
            !string.IsNullOrEmpty(_apiToken) ? "SET" : "NOT SET",
            IsConfigured);
        
        // Space keys to sync (comma-separated in config)
        var spaceKeysConfig = configuration["Confluence:SpaceKeys"];
        _spaceKeys = string.IsNullOrEmpty(spaceKeysConfig) 
            ? null 
            : spaceKeysConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        _logger.LogInformation("Confluence space keys: {SpaceKeys}", spaceKeysConfig ?? "NOT SET");

        // Azure Blob Storage for caching
        var connectionString = configuration["AzureStorage:ConnectionString"];
        var containerName = configuration["AzureStorage:ConfluenceCacheContainer"] ?? "confluence-cache";

        if (!string.IsNullOrEmpty(connectionString) && connectionString != "SET_IN_AZURE_APP_SERVICE_CONFIGURATION")
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Configure HTTP client for Confluence API
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        if (!IsConfigured) return;

        if (_baseUrl == "SET_IN_AZURE_APP_SERVICE_CONFIGURATION" ||
            _username == "SET_IN_AZURE_APP_SERVICE_CONFIGURATION" ||
            _apiToken == "SET_IN_AZURE_APP_SERVICE_CONFIGURATION")
        {
            return;
        }

        // Basic Auth with API Token (email:api_token for Cloud, username:password for Server)
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_apiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.BaseAddress = new Uri(_baseUrl!);
    }

    public async Task InitializeAsync()
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Confluence service not configured - skipping initialization. Set Confluence:BaseUrl, Username, and ApiToken");
            return;
        }

        try
        {
            // Create container if not exists
            if (_containerClient != null)
            {
                await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            }

            // Load cached pages
            await LoadCachedPagesAsync();

            _logger.LogInformation("ConfluenceKnowledgeService initialized with {Count} cached pages", _pages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ConfluenceKnowledgeService");
        }
    }

    public async Task<List<ConfluencePage>> GetAllPagesAsync()
    {
        if (_pages.Any())
        {
            return _pages;
        }

        await LoadCachedPagesAsync();
        return _pages;
    }

    public async Task<List<ConfluencePage>> GetPagesInSpaceAsync(string spaceKey)
    {
        var allPages = await GetAllPagesAsync();
        return allPages.Where(p => p.SpaceKey.Equals(spaceKey, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<int> SyncPagesAsync()
    {
        if (!IsConfigured || _httpClient.BaseAddress == null)
        {
            _logger.LogWarning("Confluence client not configured - cannot sync pages");
            return 0;
        }

        try
        {
            var newPages = new List<ConfluencePage>();
            
            if (_spaceKeys == null || !_spaceKeys.Any())
            {
                _logger.LogWarning("No Confluence space keys configured");
                return 0;
            }

            foreach (var spaceKey in _spaceKeys)
            {
                _logger.LogInformation("Syncing space: {SpaceKey}", spaceKey);
                var spacePages = await FetchPagesFromSpaceAsync(spaceKey);
                newPages.AddRange(spacePages);
                _logger.LogInformation("Space {SpaceKey} returned {Count} pages", spaceKey, spacePages.Count);
            }

            // Generate embeddings for new pages
            await GenerateEmbeddingsAsync(newPages);

            _pages = newPages;

            // Cache to blob storage
            await SaveCachedPagesAsync();

            _logger.LogInformation("Synced {Count} pages from Confluence", newPages.Count);
            return newPages.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Confluence pages");
            return 0;
        }
    }
    
    /// <summary>
    /// Sync a single space (useful to avoid timeout when syncing many spaces)
    /// </summary>
    public async Task<(int count, string message)> SyncSingleSpaceAsync(string spaceKey)
    {
        if (!IsConfigured || _httpClient.BaseAddress == null)
        {
            return (0, "Confluence client not configured");
        }

        try
        {
            _logger.LogInformation("Syncing single space: {SpaceKey}", spaceKey);
            
            // Fetch pages from the specific space
            var spacePages = await FetchPagesFromSpaceAsync(spaceKey);
            
            if (!spacePages.Any())
            {
                return (0, $"No pages found in space {spaceKey}");
            }
            
            // Generate embeddings for these pages
            await GenerateEmbeddingsAsync(spacePages);
            
            // Add to existing pages (remove old pages from same space first)
            _pages = _pages.Where(p => !p.SpaceKey.Equals(spaceKey, StringComparison.OrdinalIgnoreCase)).ToList();
            _pages.AddRange(spacePages);
            
            // Save to cache
            await SaveCachedPagesAsync();
            
            var message = $"Synced {spacePages.Count} pages from space {spaceKey}. Total cached: {_pages.Count}";
            _logger.LogInformation(message);
            return (spacePages.Count, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing space {SpaceKey}", spaceKey);
            return (0, $"Error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Get configured space keys
    /// </summary>
    public string[] GetConfiguredSpaceKeys() => _spaceKeys ?? Array.Empty<string>();

    private async Task<List<ConfluencePage>> FetchPagesFromSpaceAsync(string spaceKey)
    {
        var pages = new List<ConfluencePage>();
        var pageIds = new List<(string Id, string Title)>();
        var start = 0;
        var limit = 50;
        var hasMore = true;

        try
        {
            // Step 1: Get list of all page IDs (without body to avoid size limits)
            while (hasMore)
            {
                var url = $"/wiki/rest/api/content?spaceKey={spaceKey}&type=page&status=current&expand=version,metadata.labels&start={start}&limit={limit}";
                
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch pages from space {SpaceKey}: {StatusCode}", spaceKey, response.StatusCode);
                    break;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonDocument.Parse(json);
                var root = result.RootElement;

                if (root.TryGetProperty("results", out var results))
                {
                    foreach (var item in results.EnumerateArray())
                    {
                        var id = item.GetProperty("id").GetString() ?? "";
                        var title = item.GetProperty("title").GetString() ?? "";
                        if (!string.IsNullOrEmpty(id))
                        {
                            pageIds.Add((id, title));
                        }
                    }
                }

                if (root.TryGetProperty("size", out var size) && size.GetInt32() < limit)
                {
                    hasMore = false;
                }
                else
                {
                    start += limit;
                }

                await Task.Delay(50);
            }

            _logger.LogInformation("Found {Count} pages in space {SpaceKey}, fetching content...", pageIds.Count, spaceKey);

            // Step 2: Fetch each page individually to get full content
            var counter = 0;
            foreach (var (pageId, pageTitle) in pageIds)
            {
                try
                {
                    var page = await FetchSinglePageAsync(pageId, spaceKey);
                    if (page != null)
                    {
                        pages.Add(page);
                    }
                    
                    counter++;
                    if (counter % 20 == 0)
                    {
                        _logger.LogInformation("Fetched {Count}/{Total} pages from {SpaceKey}", counter, pageIds.Count, spaceKey);
                    }

                    // Rate limiting - be gentle with the API
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch page {PageId} ({Title})", pageId, pageTitle);
                }
            }

            _logger.LogInformation("Fetched {Count} pages with content from space {SpaceKey}", pages.Count, spaceKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pages from space {SpaceKey}", spaceKey);
        }

        return pages;
    }

    private async Task<ConfluencePage?> FetchSinglePageAsync(string pageId, string spaceKey)
    {
        var url = $"/wiki/rest/api/content/{pageId}?expand=body.storage,version,ancestors,metadata.labels";
        
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var item = JsonDocument.Parse(json).RootElement;
        
        return ParseConfluencePage(item, spaceKey);
    }

    private ConfluencePage? ParseConfluencePage(JsonElement item, string spaceKey)
    {
        try
        {
            var page = new ConfluencePage
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                Title = item.GetProperty("title").GetString() ?? string.Empty,
                SpaceKey = spaceKey,
                Status = item.TryGetProperty("status", out var status) ? status.GetString() ?? "current" : "current"
            };

            // Get content body - try multiple formats
            string htmlContent = "";
            
            if (item.TryGetProperty("body", out var body))
            {
                // Try storage format first (most common)
                if (body.TryGetProperty("storage", out var storage) && 
                    storage.TryGetProperty("value", out var storageValue))
                {
                    htmlContent = storageValue.GetString() ?? "";
                }
                // Try view format (rendered HTML)
                else if (body.TryGetProperty("view", out var view) && 
                         view.TryGetProperty("value", out var viewValue))
                {
                    htmlContent = viewValue.GetString() ?? "";
                }
                // Try export_view format
                else if (body.TryGetProperty("export_view", out var exportView) && 
                         exportView.TryGetProperty("value", out var exportValue))
                {
                    htmlContent = exportValue.GetString() ?? "";
                }
            }
            
            page.Content = StripHtmlTags(htmlContent);
            
            // Log if content is empty for debugging
            if (string.IsNullOrEmpty(page.Content))
            {
                _logger.LogWarning("Empty content for page: {Title} (ID: {Id})", page.Title, page.Id);
            }

            // Get version info
            if (item.TryGetProperty("version", out var version))
            {
                page.Version = version.TryGetProperty("number", out var num) ? num.GetInt32() : 1;
                
                if (version.TryGetProperty("when", out var when))
                {
                    page.Modified = DateTime.Parse(when.GetString() ?? DateTime.UtcNow.ToString());
                }
                
                if (version.TryGetProperty("by", out var by) && by.TryGetProperty("displayName", out var displayName))
                {
                    page.ModifiedBy = displayName.GetString() ?? "Unknown";
                }
            }

            // Get labels
            if (item.TryGetProperty("metadata", out var metadata) && 
                metadata.TryGetProperty("labels", out var labels) &&
                labels.TryGetProperty("results", out var labelResults))
            {
                foreach (var label in labelResults.EnumerateArray())
                {
                    if (label.TryGetProperty("name", out var labelName))
                    {
                        page.Labels.Add(labelName.GetString() ?? "");
                    }
                }
            }

            // Get ancestors (parent pages)
            if (item.TryGetProperty("ancestors", out var ancestors))
            {
                foreach (var ancestor in ancestors.EnumerateArray())
                {
                    if (ancestor.TryGetProperty("title", out var ancestorTitle))
                    {
                        page.Ancestors.Add(ancestorTitle.GetString() ?? "");
                    }
                }
            }

            // Build web URL
            if (item.TryGetProperty("_links", out var links) && links.TryGetProperty("webui", out var webui))
            {
                page.WebUrl = $"{_baseUrl}/wiki{webui.GetString()}";
            }

            return page;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing Confluence page");
            return null;
        }
    }

    private string StripHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // Remove script and style elements
        html = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        // Remove all HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ");
        
        // Decode HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);
        
        // Normalize whitespace
        html = Regex.Replace(html, @"\s+", " ").Trim();

        return html;
    }

    public async Task<string> GetPageContentAsync(string pageId)
    {
        var page = _pages.FirstOrDefault(p => p.Id == pageId);
        return page?.Content ?? string.Empty;
    }

    public async Task<List<ConfluencePage>> SearchAsync(string query, int topResults = 5)
    {
        if (!_pages.Any())
        {
            await LoadCachedPagesAsync();
        }

        if (!_pages.Any() || string.IsNullOrWhiteSpace(query))
        {
            return new List<ConfluencePage>();
        }

        try
        {
            // Generate embedding for the query
            var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.Value.ToFloats();

            // Calculate cosine similarity for each page
            var results = _pages
                .Where(p => p.Embedding.Length > 0)
                .Select(p => new
                {
                    Page = p,
                    Similarity = CosineSimilarity(queryVector.Span, p.Embedding.Span)
                })
                .Where(x => x.Similarity > 0.3f) // Minimum similarity threshold
                .OrderByDescending(x => x.Similarity)
                .Take(topResults)
                .Select(x => x.Page)
                .ToList();

            _logger.LogInformation("Confluence search for '{Query}' returned {Count} results", 
                query.Substring(0, Math.Min(30, query.Length)), results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Confluence pages");
            return new List<ConfluencePage>();
        }
    }

    /// <summary>
    /// Search with scores for diagnostics
    /// </summary>
    public async Task<List<(ConfluencePage Page, float Similarity)>> SearchWithScoresAsync(string query, int topResults = 10)
    {
        if (!_pages.Any())
        {
            await LoadCachedPagesAsync();
        }

        if (!_pages.Any() || string.IsNullOrWhiteSpace(query))
        {
            return new List<(ConfluencePage, float)>();
        }

        try
        {
            var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.Value.ToFloats();

            var results = _pages
                .Where(p => p.Embedding.Length > 0)
                .Select(p => (Page: p, Similarity: CosineSimilarity(queryVector.Span, p.Embedding.Span)))
                .OrderByDescending(x => x.Similarity)
                .Take(topResults)
                .ToList();

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Confluence pages with scores");
            return new List<(ConfluencePage, float)>();
        }
    }

    private float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length) return 0;

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }

    private async Task GenerateEmbeddingsAsync(List<ConfluencePage> pages)
    {
        foreach (var page in pages)
        {
            try
            {
                var text = page.GetSearchableText();
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Truncate to avoid token limits
                if (text.Length > 8000)
                {
                    text = text.Substring(0, 8000);
                }

                var embedding = await _embeddingClient.GenerateEmbeddingAsync(text);
                page.Embedding = embedding.Value.ToFloats();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding for page {PageId}", page.Id);
            }
        }
    }

    private async Task LoadCachedPagesAsync()
    {
        if (_containerClient == null) return;

        try
        {
            var blobClient = _containerClient.GetBlobClient(CacheBlobName);
            
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogInformation("No cached Confluence pages found");
                return;
            }

            var response = await blobClient.DownloadContentAsync();
            var storageModels = JsonSerializer.Deserialize<List<ConfluencePageStorageModel>>(
                response.Value.Content.ToString(), _jsonOptions);

            if (storageModels == null) return;

            _pages = storageModels.Select(s => new ConfluencePage
            {
                Id = s.Id,
                Title = s.Title,
                SpaceKey = s.SpaceKey,
                SpaceName = s.SpaceName,
                Content = s.Content,
                Excerpt = s.Excerpt,
                WebUrl = s.WebUrl,
                CreatedBy = s.CreatedBy,
                ModifiedBy = s.ModifiedBy,
                Created = s.Created,
                Modified = s.Modified,
                Version = s.Version,
                Status = s.Status,
                Labels = s.Labels,
                Ancestors = s.Ancestors,
                Embedding = s.Embedding != null && s.Embedding.Length > 0 
                    ? new ReadOnlyMemory<float>(s.Embedding) 
                    : default
            }).ToList();

            // Check if any pages are missing embeddings and regenerate only those
            var pagesWithoutEmbeddings = _pages.Where(p => p.Embedding.Length == 0).ToList();
            if (pagesWithoutEmbeddings.Any())
            {
                _logger.LogInformation("Regenerating embeddings for {Count} pages without embeddings", pagesWithoutEmbeddings.Count);
                await GenerateEmbeddingsAsync(pagesWithoutEmbeddings);
                await SaveCachedPagesAsync(); // Save updated embeddings
            }

            _logger.LogInformation("Loaded {Count} Confluence pages from cache", _pages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading cached Confluence pages");
        }
    }

    private async Task SaveCachedPagesAsync()
    {
        if (_containerClient == null) return;

        try
        {
            var blobClient = _containerClient.GetBlobClient(CacheBlobName);

            var storageModels = _pages.Select(p => new ConfluencePageStorageModel
            {
                Id = p.Id,
                Title = p.Title,
                SpaceKey = p.SpaceKey,
                SpaceName = p.SpaceName,
                Content = p.Content,
                Excerpt = p.Excerpt,
                WebUrl = p.WebUrl,
                CreatedBy = p.CreatedBy,
                ModifiedBy = p.ModifiedBy,
                Created = p.Created,
                Modified = p.Modified,
                Version = p.Version,
                Status = p.Status,
                Labels = p.Labels,
                Ancestors = p.Ancestors,
                Embedding = p.Embedding.Length > 0 ? p.Embedding.ToArray() : null
            }).ToList();

            var json = JsonSerializer.Serialize(storageModels, _jsonOptions);
            
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(stream, overwrite: true);

            _logger.LogInformation("Cached {Count} Confluence pages to blob storage", _pages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving Confluence pages cache");
        }
    }
}
