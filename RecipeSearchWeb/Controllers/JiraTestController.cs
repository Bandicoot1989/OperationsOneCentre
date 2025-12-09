using Microsoft.AspNetCore.Mvc;
using RecipeSearchWeb.Interfaces;
using RecipeSearchWeb.Services;

namespace RecipeSearchWeb.Controllers;

/// <summary>
/// Temporary controller for testing Jira API connection
/// Can be removed after verification
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class JiraTestController : ControllerBase
{
    private readonly JiraClient _jiraClient;
    private readonly ILogger<JiraTestController> _logger;

    public JiraTestController(JiraClient jiraClient, ILogger<JiraTestController> logger)
    {
        _jiraClient = jiraClient;
        _logger = logger;
    }

    /// <summary>
    /// Test connection to Jira API
    /// GET /api/jiratest/connection
    /// </summary>
    [HttpGet("connection")]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            _logger.LogInformation("Testing Jira connection...");
            
            var isConnected = await _jiraClient.TestConnectionAsync();
            
            if (isConnected)
            {
                return Ok(new 
                { 
                    success = true, 
                    message = "Successfully connected to Jira API",
                    timestamp = DateTime.UtcNow
                });
            }
            else
            {
                return Ok(new 
                { 
                    success = false, 
                    message = "Jira connection failed - check credentials or configuration",
                    hint = "Ensure Jira:BaseUrl, Jira:Username, and Jira:ApiToken are configured",
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Jira connection");
            return StatusCode(500, new 
            { 
                success = false, 
                message = $"Error: {ex.Message}",
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get recent resolved tickets (test data fetch)
    /// GET /api/jiratest/tickets?days=7&maxResults=5
    /// </summary>
    [HttpGet("tickets")]
    public async Task<IActionResult> GetResolvedTickets([FromQuery] int days = 7, [FromQuery] int maxResults = 5)
    {
        try
        {
            _logger.LogInformation("Fetching resolved tickets from last {Days} days (max {Max})", days, maxResults);
            
            var tickets = await _jiraClient.GetResolvedTicketsAsync(days, null, maxResults);
            
            if (tickets == null || !tickets.Any())
            {
                return Ok(new
                {
                    success = true,
                    message = "No resolved tickets found in the specified period",
                    count = 0,
                    tickets = Array.Empty<object>()
                });
            }

            // Return simplified view of tickets
            var summary = tickets.Select(t => new
            {
                key = t.Key,
                summary = t.Summary,
                status = t.Status,
                resolution = t.Resolution,
                resolved = t.Resolved,
                assignee = t.Assignee,
                hasDescription = !string.IsNullOrEmpty(t.Description),
                commentCount = t.Comments?.Count ?? 0
            });

            return Ok(new
            {
                success = true,
                message = $"Found {tickets.Count} resolved tickets",
                count = tickets.Count,
                tickets = summary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Jira tickets");
            return StatusCode(500, new
            {
                success = false,
                message = $"Error: {ex.Message}",
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get a specific ticket by key
    /// GET /api/jiratest/ticket/PROJ-123
    /// </summary>
    [HttpGet("ticket/{ticketKey}")]
    public async Task<IActionResult> GetTicket(string ticketKey)
    {
        try
        {
            _logger.LogInformation("Fetching ticket {Key}", ticketKey);
            
            var ticket = await _jiraClient.GetTicketAsync(ticketKey);
            
            if (ticket == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Ticket {ticketKey} not found"
                });
            }

            return Ok(new
            {
                success = true,
                ticket = new
                {
                    key = ticket.Key,
                    summary = ticket.Summary,
                    status = ticket.Status,
                    resolution = ticket.Resolution,
                    resolved = ticket.Resolved,
                    created = ticket.Created,
                    assignee = ticket.Assignee,
                    reporter = ticket.Reporter,
                    description = ticket.Description?.Substring(0, Math.Min(ticket.Description.Length, 500)) + 
                                 (ticket.Description?.Length > 500 ? "..." : ""),
                    comments = ticket.Comments?.Select(c => new
                    {
                        author = c.Author,
                        created = c.Created,
                        bodyPreview = c.Body?.Substring(0, Math.Min(c.Body.Length, 200)) + 
                                     (c.Body?.Length > 200 ? "..." : "")
                    })
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching ticket {Key}", ticketKey);
            return StatusCode(500, new
            {
                success = false,
                message = $"Error: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Check current Jira configuration status
    /// GET /api/jiratest/config
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfigStatus()
    {
        var isConfigured = _jiraClient.IsConfigured;
        
        return Ok(new
        {
            isConfigured,
            message = isConfigured 
                ? "Jira is configured. Use /api/jiratest/connection to test the connection."
                : "Jira is NOT configured. Please set Jira:BaseUrl, Jira:Username, and Jira:ApiToken in app settings.",
            requiredSettings = new[]
            {
                "Jira:BaseUrl - e.g., https://yourcompany.atlassian.net",
                "Jira:Username - Your Jira email/username",
                "Jira:ApiToken - API token from https://id.atlassian.com/manage/api-tokens"
            }
        });
    }
}
