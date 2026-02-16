using Microsoft.AspNetCore.Mvc;
using OperationsOneCentre.Services;

namespace OperationsOneCentre.Controllers;

/// <summary>
/// Health check endpoint for Azure App Service and monitoring.
/// Returns service status and readiness information.
/// </summary>
[ApiController]
[Route("api")]
public class HealthController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IServiceProvider serviceProvider, ILogger<HealthController> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        var checks = new Dictionary<string, object>();
        var overallHealthy = true;

        // Check core services
        try
        {
            var knowledgeSearch = _serviceProvider.GetRequiredService<KnowledgeSearchService>();
            checks["KnowledgeSearch"] = new { Status = "Healthy", ArticleCount = knowledgeSearch.GetAllArticles()?.Count ?? 0 };
        }
        catch (Exception ex)
        {
            checks["KnowledgeSearch"] = new { Status = "Unhealthy", Error = ex.Message };
            overallHealthy = false;
        }

        try
        {
            var contextSearch = _serviceProvider.GetRequiredService<ContextSearchService>();
            checks["ContextSearch"] = new { Status = "Healthy" };
        }
        catch (Exception ex)
        {
            checks["ContextSearch"] = new { Status = "Unhealthy", Error = ex.Message };
            overallHealthy = false;
        }

        try
        {
            var confluence = _serviceProvider.GetRequiredService<ConfluenceKnowledgeService>();
            checks["Confluence"] = new
            {
                Status = confluence.IsConfigured ? "Healthy" : "NotConfigured",
                PageCount = confluence.GetCachedPageCount()
            };
        }
        catch (Exception ex)
        {
            checks["Confluence"] = new { Status = "Unhealthy", Error = ex.Message };
        }

        try
        {
            var cache = _serviceProvider.GetRequiredService<QueryCacheService>();
            var stats = cache.GetStatistics();
            checks["Cache"] = new
            {
                Status = "Healthy",
                HitRate = $"{stats.HitRate:F1}%",
                SemanticCacheSize = stats.SemanticCacheSize
            };
        }
        catch (Exception ex)
        {
            checks["Cache"] = new { Status = "Unhealthy", Error = ex.Message };
        }

        return Ok(new
        {
            Status = overallHealthy ? "Healthy" : "Degraded",
            Timestamp = DateTime.UtcNow,
            Version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            Checks = checks
        });
    }
}
