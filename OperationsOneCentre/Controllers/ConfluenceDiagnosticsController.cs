using Microsoft.AspNetCore.Mvc;
using OperationsOneCentre.Services;

namespace OperationsOneCentre.Controllers;

/// <summary>
/// Diagnostic endpoints for Confluence integration.
/// Moved from Program.cs to follow clean architecture and keep startup minimal.
/// </summary>
[ApiController]
[Route("api/confluence")]
public class ConfluenceDiagnosticsController : ControllerBase
{
    private readonly ConfluenceKnowledgeService _confluenceService;
    private readonly IConfiguration _config;
    private readonly ILogger<ConfluenceDiagnosticsController> _logger;

    public ConfluenceDiagnosticsController(
        ConfluenceKnowledgeService confluenceService,
        IConfiguration config,
        ILogger<ConfluenceDiagnosticsController> logger)
    {
        _confluenceService = confluenceService;
        _config = config;
        _logger = logger;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var baseUrl = _config["Confluence:BaseUrl"];
        var email = _config["Confluence:Email"];
        var spaceKeys = _config["Confluence:SpaceKeys"];
        var tokenBase64 = _config["Confluence:ApiTokenBase64"];
        var tokenLegacy = _config["Confluence:ApiToken"];

        return Ok(new
        {
            IsConfigured = _confluenceService.IsConfigured,
            PageCount = _confluenceService.GetCachedPageCount(),
            Config = new
            {
                BaseUrl = string.IsNullOrEmpty(baseUrl) ? "NOT SET" : baseUrl,
                Email = string.IsNullOrEmpty(email) ? "NOT SET" : email,
                SpaceKeys = string.IsNullOrEmpty(spaceKeys) ? "NOT SET" : spaceKeys,
                ApiTokenBase64 = string.IsNullOrEmpty(tokenBase64) ? "NOT SET" : $"SET ({tokenBase64.Length} chars)",
                ApiTokenLegacy = string.IsNullOrEmpty(tokenLegacy) ? "NOT SET" : $"SET ({tokenLegacy.Length} chars)"
            }
        });
    }

    [HttpGet("sync")]
    public async Task<IActionResult> SyncAll(CancellationToken cancellationToken)
    {
        if (!_confluenceService.IsConfigured)
            return Ok(new { Success = false, Error = "Confluence is not configured" });

        try
        {
            var syncedCount = await _confluenceService.SyncPagesAsync();
            return Ok(new
            {
                Success = true,
                PageCount = _confluenceService.GetCachedPageCount(),
                Message = $"Sync completed successfully - {syncedCount} pages synced with content"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Confluence sync failed");
            return Ok(new
            {
                Success = false,
                Error = ex.Message,
                InnerError = ex.InnerException?.Message
            });
        }
    }

    [HttpGet("sync/{spaceKey}")]
    public async Task<IActionResult> SyncSpace(string spaceKey, CancellationToken cancellationToken)
    {
        if (!_confluenceService.IsConfigured)
            return Ok(new { Success = false, Error = "Confluence is not configured" });

        try
        {
            var (count, message) = await _confluenceService.SyncSingleSpaceAsync(spaceKey.ToUpperInvariant());
            return Ok(new
            {
                Success = count > 0,
                SpaceKey = spaceKey.ToUpperInvariant(),
                PageCount = count,
                TotalCached = _confluenceService.GetCachedPageCount(),
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Confluence space sync failed for {SpaceKey}", spaceKey);
            return Ok(new { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("spaces")]
    public IActionResult GetSpaces()
    {
        var spaces = _confluenceService.GetConfiguredSpaceKeys();
        var cachedPages = _confluenceService.GetAllCachedPages();

        return Ok(new
        {
            ConfiguredSpaces = spaces,
            SpaceStats = spaces.Select(s => new
            {
                SpaceKey = s,
                CachedPageCount = cachedPages.Count(p => p.SpaceKey.Equals(s, StringComparison.OrdinalIgnoreCase))
            }),
            TotalCachedPages = cachedPages.Count
        });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        try
        {
            var results = await _confluenceService.SearchWithScoresAsync(q, topResults: 10);
            return Ok(new
            {
                Query = q,
                TotalPages = _confluenceService.GetCachedPageCount(),
                Results = results.Select(r => new
                {
                    Title = r.Page.Title,
                    SpaceKey = r.Page.SpaceKey,
                    Score = r.Similarity,
                    ContentPreview = r.Page.Content?.Substring(0, Math.Min(200, r.Page.Content?.Length ?? 0)) + "..."
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Confluence search failed for query: {Query}", q);
            return Ok(new { Error = ex.Message });
        }
    }

    [HttpGet("list")]
    public IActionResult ListPages([FromQuery] string? search)
    {
        var pages = _confluenceService.GetAllCachedPages();

        if (!string.IsNullOrEmpty(search))
        {
            pages = pages.Where(p => p.Title.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Ok(new
        {
            TotalCount = _confluenceService.GetCachedPageCount(),
            FilteredCount = pages.Count,
            Pages = pages.Take(50).Select(p => new
            {
                p.Id,
                p.Title,
                ContentLength = p.Content?.Length ?? 0,
                HasEmbedding = p.Embedding.Length > 0
            })
        });
    }
}
