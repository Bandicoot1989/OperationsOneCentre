using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsOneCentre.Services;
using System.Security.Claims;

namespace OperationsOneCentre.Controllers;

/// <summary>
/// Diagnostic endpoints for context search and authentication.
/// Moved from Program.cs to follow clean architecture.
/// </summary>
[ApiController]
[Route("api")]
public class DiagnosticsController : ControllerBase
{
    private readonly ContextSearchService _contextService;
    private readonly AzureAuthService _authService;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        ContextSearchService contextService,
        AzureAuthService authService,
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<DiagnosticsController> logger)
    {
        _contextService = contextService;
        _authService = authService;
        _env = env;
        _config = config;
        _logger = logger;
    }

    [HttpGet("context-debug")]
    [AllowAnonymous]
    public async Task<IActionResult> ContextDebug([FromQuery] string q, CancellationToken cancellationToken)
    {
        try
        {
            var results = await _contextService.SearchAsync(q, topResults: 10);
            return Ok(new
            {
                Query = q,
                ResultCount = results.Count,
                Results = results.Select(r => new
                {
                    r.Name,
                    r.Category,
                    r.Description,
                    r.Keywords,
                    Link = r.Link ?? "NO LINK",
                    AdditionalDataCount = r.AdditionalData?.Count ?? 0,
                    AdditionalData = r.AdditionalData
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Context debug search failed for query: {Query}", q);
            return Ok(new { Error = ex.Message });
        }
    }

    [HttpGet("context-all")]
    [AllowAnonymous]
    public async Task<IActionResult> ContextAll(CancellationToken cancellationToken)
    {
        try
        {
            var files = await _contextService.GetFilesAsync();
            var allDocs = new List<object>();

            foreach (var file in files)
            {
                var docs = await _contextService.GetDocumentsByFileAsync(file.FileName);
                allDocs.AddRange(docs.Select(r => new
                {
                    r.Name,
                    r.Category,
                    Link = string.IsNullOrEmpty(r.Link) ? "EMPTY" : r.Link,
                    r.Description
                }));
            }

            return Ok(new
            {
                TotalDocuments = allDocs.Count,
                Documents = allDocs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Context all search failed");
            return Ok(new { Error = ex.Message });
        }
    }

    [HttpGet("auth-status")]
    [AllowAnonymous]
    public IActionResult AuthStatus()
    {
        var user = _authService.GetCurrentUser();
        var easyAuthName = HttpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault();
        var easyAuthId = HttpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"].FirstOrDefault();
        var claimsPrincipal = HttpContext.User;

        var adminEmails = _config.GetSection("Authorization:AdminEmails").Get<string[]>() ?? Array.Empty<string>();

        return Ok(new
        {
            Environment = _env.EnvironmentName,
            IsDevelopment = _env.IsDevelopment(),
            EasyAuth = new
            {
                PrincipalName = easyAuthName ?? "NOT SET",
                PrincipalId = easyAuthId ?? "NOT SET"
            },
            ClaimsPrincipal = new
            {
                IsAuthenticated = claimsPrincipal?.Identity?.IsAuthenticated ?? false,
                Name = claimsPrincipal?.Identity?.Name ?? "null",
                AuthenticationType = claimsPrincipal?.Identity?.AuthenticationType ?? "null",
                Claims = claimsPrincipal?.Claims?.Select(c => new { c.Type, c.Value }).ToList()
            },
            CurrentUser = user == null ? null : new
            {
                user.Username,
                user.FullName,
                user.Role,
                user.IsAdmin
            },
            ConfiguredAdmins = adminEmails,
            DevelopmentConfig = new
            {
                SimulatedEmail = _config["Development:SimulatedUserEmail"] ?? "NOT SET",
                SimulatedName = _config["Development:SimulatedUserName"] ?? "NOT SET"
            }
        });
    }
}
