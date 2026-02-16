using Microsoft.AspNetCore.Mvc;
using OperationsOneCentre.Interfaces;
using OperationsOneCentre.Services;

namespace OperationsOneCentre.Controllers;

/// <summary>
/// Temporary controller for testing Jira API connection
/// Can be removed after verification
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class JiraTestController : ControllerBase
{
    private readonly JiraClient _jiraClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JiraTestController> _logger;

    public JiraTestController(
        JiraClient jiraClient, 
        IServiceProvider serviceProvider,
        ILogger<JiraTestController> logger)
    {
        _jiraClient = jiraClient;
        _serviceProvider = serviceProvider;
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
                project = t.Project,
                priority = t.Priority,
                hasDescription = !string.IsNullOrEmpty(t.Description),
                descriptionPreview = !string.IsNullOrEmpty(t.Description) 
                    ? (t.Description.Length > 200 ? t.Description.Substring(0, 200) + "..." : t.Description) 
                    : null,
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

    /// <summary>
    /// Raw diagnostic endpoint - calls Jira API directly and returns raw response
    /// GET /api/jiratest/raw?jql=resolved >= -7d
    /// </summary>
    [HttpGet("raw")]
    public async Task<IActionResult> RawJiraQuery([FromQuery] string? jql = "resolved >= -7d ORDER BY resolved DESC", [FromQuery] int maxResults = 3)
    {
        try
        {
            var diagnostics = new List<string>();
            diagnostics.Add($"Starting raw Jira query at {DateTime.UtcNow}");
            diagnostics.Add($"JQL: {jql}");
            diagnostics.Add($"MaxResults: {maxResults}");
            diagnostics.Add($"IsConfigured: {_jiraClient.IsConfigured}");
            
            if (!_jiraClient.IsConfigured)
            {
                return Ok(new { success = false, diagnostics, error = "Jira client not configured" });
            }

            // Call the raw search method
            var result = await _jiraClient.RawSearchAsync(jql!, maxResults);
            diagnostics.Add($"Raw search completed");
            
            return Ok(new 
            { 
                success = true, 
                diagnostics,
                rawResponse = result
            });
        }
        catch (Exception ex)
        {
            return Ok(new 
            { 
                success = false, 
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// Debug the full search pipeline
    /// GET /api/jiratest/debug
    /// </summary>
    [HttpGet("debug")]
    public async Task<IActionResult> DebugSearch([FromQuery] int days = 7, [FromQuery] int maxResults = 3)
    {
        var diagnostics = new List<string>();
        
        try
        {
            diagnostics.Add($"IsConfigured: {_jiraClient.IsConfigured}");
            
            // Step 1: Get raw response
            var jql = $"resolved >= -{days}d ORDER BY resolved DESC";
            diagnostics.Add($"JQL: {jql}");
            
            var rawResult = await _jiraClient.RawSearchAsync(jql, maxResults);
            diagnostics.Add($"Raw search completed");
            
            // Step 2: Try to deserialize manually
            var rawJson = System.Text.Json.JsonSerializer.Serialize(rawResult);
            diagnostics.Add($"Raw result type: {rawResult.GetType().Name}");
            
            // Step 3: Call actual method
            var tickets = await _jiraClient.GetResolvedTicketsAsync(days, null, maxResults);
            diagnostics.Add($"GetResolvedTicketsAsync returned {tickets.Count} tickets");
            
            return Ok(new
            {
                success = true,
                diagnostics,
                ticketCount = tickets.Count,
                tickets = tickets.Select(t => new 
                {
                    key = t.Key,
                    summary = t.Summary,
                    status = t.Status,
                    resolved = t.Resolved
                }),
                rawResultPreview = rawJson.Length > 500 ? rawJson.Substring(0, 500) : rawJson
            });
        }
        catch (Exception ex)
        {
            diagnostics.Add($"ERROR: {ex.Message}");
            return Ok(new
            {
                success = false,
                diagnostics,
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// Direct deserialize test - skip all intermediate methods
    /// GET /api/jiratest/deserialize-test
    /// </summary>
    [HttpGet("deserialize-test")]
    public async Task<IActionResult> DeserializeTest()
    {
        var diagnostics = new List<string>();
        
        try
        {
            // Call raw API directly and try to deserialize
            var result = await _jiraClient.TestDeserializationAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// Diagnose a specific ticket - shows raw API response
    /// GET /api/jiratest/diagnose/MT-802141
    /// </summary>
    [HttpGet("diagnose/{ticketKey}")]
    public async Task<IActionResult> DiagnoseTicket(string ticketKey)
    {
        var diagnostics = new List<string>();
        
        try
        {
            diagnostics.Add($"Testing ticket: {ticketKey}");
            diagnostics.Add($"JiraClient.IsConfigured: {_jiraClient.IsConfigured}");
            
            if (!_jiraClient.IsConfigured)
            {
                diagnostics.Add("ERROR: Jira client not configured");
                return Ok(new { success = false, diagnostics });
            }

            // Test 1: Try GetTicketAsync
            diagnostics.Add("Calling GetTicketAsync...");
            var ticket = await _jiraClient.GetTicketAsync(ticketKey);
            
            if (ticket != null)
            {
                diagnostics.Add($"SUCCESS: Got ticket from GetTicketAsync");
                return Ok(new
                {
                    success = true,
                    diagnostics,
                    ticket = new
                    {
                        key = ticket.Key,
                        summary = ticket.Summary,
                        status = ticket.Status,
                        priority = ticket.Priority,
                        reporter = ticket.Reporter,
                        assignee = ticket.Assignee,
                        created = ticket.Created,
                        descriptionLength = ticket.Description?.Length ?? 0,
                        commentCount = ticket.Comments?.Count ?? 0
                    }
                });
            }
            
            diagnostics.Add("GetTicketAsync returned null - checking raw API...");
            
            // Test 2: Try raw search by key
            diagnostics.Add($"Trying raw search for key={ticketKey}...");
            var rawResult = await _jiraClient.RawSearchAsync($"key={ticketKey}", maxResults: 1);
            diagnostics.Add($"Raw search result: {System.Text.Json.JsonSerializer.Serialize(rawResult)}");
            
            return Ok(new
            {
                success = false,
                diagnostics,
                rawSearchResult = rawResult,
                message = $"Could not fetch ticket {ticketKey} via GetTicketAsync"
            });
        }
        catch (Exception ex)
        {
            diagnostics.Add($"EXCEPTION: {ex.Message}");
            return Ok(new
            {
                success = false,
                diagnostics,
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// Check all DI registrations for ticket lookup
    /// GET /api/jiratest/di-check
    /// </summary>
    [HttpGet("di-check")]
    public IActionResult CheckDependencyInjection()
    {
        var results = new Dictionary<string, object>();
        
        // Check IJiraClient
        try
        {
            var jiraClient = _serviceProvider.GetService<IJiraClient>();
            results["IJiraClient"] = jiraClient != null ? $"OK - {jiraClient.GetType().Name}" : "NOT REGISTERED";
        }
        catch (Exception ex)
        {
            results["IJiraClient"] = $"ERROR: {ex.Message}";
        }
        
        // Check JiraClient (concrete)
        try
        {
            var jiraClient = _serviceProvider.GetService<JiraClient>();
            results["JiraClient (concrete)"] = jiraClient != null ? "OK" : "NOT REGISTERED";
        }
        catch (Exception ex)
        {
            results["JiraClient (concrete)"] = $"ERROR: {ex.Message}";
        }
        
        // Check IJiraSolutionService
        try
        {
            var solutionService = _serviceProvider.GetService<IJiraSolutionService>();
            results["IJiraSolutionService"] = solutionService != null ? $"OK - {solutionService.GetType().Name}" : "NOT REGISTERED";
        }
        catch (Exception ex)
        {
            results["IJiraSolutionService"] = $"ERROR: {ex.Message}";
        }
        
        // Check ITicketLookupService
        try
        {
            var ticketLookup = _serviceProvider.GetService<ITicketLookupService>();
            results["ITicketLookupService"] = ticketLookup != null ? $"OK - {ticketLookup.GetType().Name}" : "NOT REGISTERED";
        }
        catch (Exception ex)
        {
            results["ITicketLookupService"] = $"ERROR: {ex.Message}";
        }
        
        // Try to manually create TicketLookupService to see what fails
        try
        {
            var jiraClient = _serviceProvider.GetRequiredService<IJiraClient>();
            var solutionService = _serviceProvider.GetService<IJiraSolutionService>();
            var logger = _serviceProvider.GetRequiredService<ILogger<TicketLookupService>>();
            
            var manualService = new TicketLookupService(jiraClient, solutionService, _serviceProvider.GetService<IConfiguration>(), logger);
            results["Manual Creation Test"] = "SUCCESS - TicketLookupService can be created manually!";
        }
        catch (Exception ex)
        {
            results["Manual Creation Test"] = $"FAILED: {ex.Message}";
        }
        
        return Ok(results);
    }

    /// <summary>
    /// Simulate what the chatbot does - test the full TicketLookupService flow
    /// GET /api/jiratest/chatbot-test/MT-802141
    /// </summary>
    [HttpGet("chatbot-test/{ticketKey}")]
    public async Task<IActionResult> ChatbotFlowTest(string ticketKey)
    {
        var diagnostics = new List<string>();
        
        try
        {
            diagnostics.Add($"=== CHATBOT FLOW TEST for {ticketKey} ===");
            
            // Step 1: Try to get TicketLookupService from DI
            var ticketLookupService = _serviceProvider.GetService<ITicketLookupService>();
            diagnostics.Add($"Step 1: ITicketLookupService from DI: {(ticketLookupService != null ? "YES" : "NO - NOT REGISTERED!")}");
            
            if (ticketLookupService == null)
            {
                // Try to get the concrete type
                var concreteService = _serviceProvider.GetService<TicketLookupService>();
                diagnostics.Add($"Step 1b: TicketLookupService (concrete): {(concreteService != null ? "YES" : "NO")}");
                
                return Ok(new
                {
                    success = false,
                    problem = "ITicketLookupService is not registered in DI!",
                    diagnostics
                });
            }
            
            // Step 2: Test ContainsTicketReference
            var query = $"Me puedes ayudar con el ticket {ticketKey}?";
            var containsRef = ticketLookupService.ContainsTicketReference(query);
            diagnostics.Add($"Step 2: ContainsTicketReference('{query}'): {containsRef}");
            
            // Step 3: Test ExtractTicketIds
            var ticketIds = ticketLookupService.ExtractTicketIds(query);
            diagnostics.Add($"Step 3: ExtractTicketIds: [{string.Join(", ", ticketIds)}]");
            
            // Step 4: Test LookupTicketsAsync
            diagnostics.Add($"Step 4: Calling LookupTicketsAsync...");
            var result = await ticketLookupService.LookupTicketsAsync(ticketIds);
            
            diagnostics.Add($"Step 4 Result: Success={result.Success}, TicketCount={result.Tickets?.Count ?? 0}, Error={result.ErrorMessage ?? "none"}");
            
            if (result.Success && result.Tickets?.Any() == true)
            {
                var ticketInfo = result.Tickets.First();
                return Ok(new
                {
                    success = true,
                    diagnostics,
                    ticketInfo = new
                    {
                        ticketId = ticketInfo.TicketId,
                        summary = ticketInfo.Summary,
                        status = ticketInfo.Status,
                        priority = ticketInfo.Priority,
                        reporter = ticketInfo.Reporter,
                        assignee = ticketInfo.Assignee,
                        jiraUrl = ticketInfo.JiraUrl,
                        detectedSystem = ticketInfo.DetectedSystem
                    },
                    contextForAgent = result.ContextForAgent?.Substring(0, Math.Min(500, result.ContextForAgent?.Length ?? 0))
                });
            }
            
            return Ok(new
            {
                success = false,
                diagnostics,
                errorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            diagnostics.Add($"EXCEPTION: {ex.Message}");
            return Ok(new
            {
                success = false,
                diagnostics,
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }
}
