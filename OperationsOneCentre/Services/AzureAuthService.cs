using OperationsOneCentre.Interfaces;
using OperationsOneCentre.Models;
using System.Security.Claims;
using System.Text.Json;

namespace OperationsOneCentre.Services;

/// <summary>
/// Authentication service using Azure App Service Easy Auth
/// Reads user identity from Azure-provided headers
/// In Development mode, uses a simulated user for testing
/// </summary>
public class AzureAuthService : IAuthService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AzureAuthService> _logger;

    // List of admin email addresses - can be configured in appsettings.json
    private readonly HashSet<string> _adminEmails;

    public AzureAuthService(
        IHttpContextAccessor httpContextAccessor, 
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<AzureAuthService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        
        // Load admin emails from configuration
        var adminEmailsConfig = configuration.GetSection("Authorization:AdminEmails").Get<string[]>() ?? Array.Empty<string>();
        _adminEmails = new HashSet<string>(adminEmailsConfig, StringComparer.OrdinalIgnoreCase);
        
        // SECURITY: No default admins — admin list MUST come from configuration
        if (_adminEmails.Count == 0)
        {
            _logger.LogWarning("AzureAuthService: No admin emails configured in 'Authorization:AdminEmails'. No users will have admin access until configured.");
        }
        
        _logger.LogInformation("AzureAuthService initialized. Admin emails: {Admins}", string.Join(", ", _adminEmails));
    }

    /// <summary>
    /// Get the current authenticated user from Azure Easy Auth headers
    /// In Development mode, returns a simulated admin user
    /// </summary>
    public User? GetCurrentUser()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) 
        {
            _logger.LogWarning("GetCurrentUser: HttpContext is null");
            return null;
        }

        // Try to get user info from Easy Auth headers first
        var principalName = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault();
        var principalId = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"].FirstOrDefault();
        
        _logger.LogDebug("Easy Auth headers - Name: {Name}, Id: {Id}", principalName ?? "null", principalId ?? "null");
        
        // Also check ClaimsPrincipal (works in some configurations)
        if (string.IsNullOrEmpty(principalName) && context.User?.Identity?.IsAuthenticated == true)
        {
            principalName = context.User.Identity.Name 
                ?? context.User.FindFirst(ClaimTypes.Email)?.Value
                ?? context.User.FindFirst("preferred_username")?.Value
                ?? context.User.FindFirst("email")?.Value;
            principalId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            _logger.LogDebug("ClaimsPrincipal - Name: {Name}, Id: {Id}", principalName ?? "null", principalId ?? "null");
        }

        // DEVELOPMENT MODE: Simulate authenticated admin user for local testing
        if (string.IsNullOrEmpty(principalName) && _environment.IsDevelopment())
        {
            var devEmail = _configuration["Development:SimulatedUserEmail"] ?? "osmany.fajardo@antolin.com";
            var devName = _configuration["Development:SimulatedUserName"] ?? "Dev Admin";
            
            _logger.LogInformation("Development mode: Simulating user {Email}", devEmail);
            
            return new User
            {
                Id = devEmail.GetHashCode(),
                Username = devEmail,
                FullName = devName,
                Role = _adminEmails.Contains(devEmail) ? UserRole.Admin : UserRole.Tecnico,
                LastLogin = DateTime.Now
            };
        }

        if (string.IsNullOrEmpty(principalName))
        {
            _logger.LogWarning("GetCurrentUser: No user identity found");
            return null;
        }

        // Determine role based on admin list
        var isAdmin = _adminEmails.Contains(principalName);
        _logger.LogInformation("User {Email} authenticated. IsAdmin: {IsAdmin}", principalName, isAdmin);

        return new User
        {
            Id = principalId?.GetHashCode() ?? principalName.GetHashCode(),
            Username = principalName,
            FullName = GetDisplayName(context) ?? principalName,
            Role = isAdmin ? UserRole.Admin : UserRole.Tecnico,
            LastLogin = DateTime.Now
        };
    }

    private string? GetDisplayName(HttpContext context)
    {
        // Try to get display name from headers or claims
        var displayName = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault();
        
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            displayName = context.User.FindFirst(ClaimTypes.GivenName)?.Value
                ?? context.User.FindFirst("name")?.Value
                ?? displayName;
        }

        return displayName;
    }

    /// <summary>
    /// Check if user is authenticated
    /// </summary>
    public bool IsAuthenticated => GetCurrentUser() != null;

    /// <summary>
    /// Check if current user is admin
    /// </summary>
    public bool IsAdmin => GetCurrentUser()?.IsAdmin ?? false;

    /// <summary>
    /// Get logout URL for Azure Easy Auth
    /// </summary>
    public string GetLogoutUrl()
    {
        return "/.auth/logout?post_logout_redirect_uri=/";
    }

    /// <summary>
    /// Get login URL for Azure Easy Auth
    /// </summary>
    public string GetLoginUrl()
    {
        return "/.auth/login/aad";
    }
}
