using RecipeSearchWeb.Interfaces;
using RecipeSearchWeb.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecipeSearchWeb.Services;

/// <summary>
/// Client for Jira Cloud API (atlassian.net)
/// Uses REST API v3 with Basic Auth (email + API token)
/// </summary>
public class JiraClient : IJiraClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraClient> _logger;
    private readonly string? _baseUrl;
    private readonly bool _isConfigured;

    public JiraClient(IConfiguration configuration, ILogger<JiraClient> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        
        // Configuration from appsettings or environment variables
        _baseUrl = configuration["Jira:BaseUrl"] ?? configuration["JIRA_BASE_URL"];
        var email = configuration["Jira:Email"] ?? configuration["JIRA_EMAIL"];
        var apiToken = configuration["Jira:ApiToken"] ?? configuration["JIRA_API_TOKEN"];
        
        if (string.IsNullOrEmpty(_baseUrl) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(apiToken))
        {
            _logger.LogWarning("Jira configuration incomplete. Required: BaseUrl, Email, ApiToken");
            _isConfigured = false;
            return;
        }
        
        // Normalize base URL
        _baseUrl = _baseUrl.TrimEnd('/');
        
        // Configure HTTP client with Basic Auth
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.BaseAddress = new Uri(_baseUrl);
        
        _isConfigured = true;
        _logger.LogInformation("JiraClient configured for {BaseUrl}", _baseUrl);
    }

    public bool IsConfigured => _isConfigured;

    /// <summary>
    /// Test the connection to Jira
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        if (!_isConfigured) return false;
        
        try
        {
            var response = await _httpClient.GetAsync("/rest/api/3/myself");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<JiraUser>(content, _jsonOptions);
                _logger.LogInformation("Jira connection successful. Authenticated as: {DisplayName} ({Email})", 
                    user?.DisplayName, user?.EmailAddress);
                return true;
            }
            
            _logger.LogError("Jira connection failed: {Status} - {Reason}", 
                response.StatusCode, response.ReasonPhrase);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Jira");
            return false;
        }
    }

    /// <summary>
    /// Get resolved/closed tickets from the last N days
    /// </summary>
    public async Task<List<JiraTicket>> GetResolvedTicketsAsync(int days = 30, List<string>? projectKeys = null, int maxResults = 100)
    {
        if (!_isConfigured) return new List<JiraTicket>();
        
        try
        {
            // Build JQL query
            var jql = $"status in (Resolved, Closed, Done) AND resolved >= -{days}d";
            
            if (projectKeys?.Any() == true)
            {
                jql += $" AND project in ({string.Join(",", projectKeys)})";
            }
            
            jql += " ORDER BY resolved DESC";
            
            return await SearchTicketsAsync(jql, maxResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resolved tickets");
            return new List<JiraTicket>();
        }
    }

    /// <summary>
    /// Get a single ticket by its key
    /// </summary>
    public async Task<JiraTicket?> GetTicketAsync(string ticketKey)
    {
        if (!_isConfigured) return null;
        
        try
        {
            var url = $"/rest/api/3/issue/{ticketKey}?expand=changelog";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get ticket {Key}: {Status}", ticketKey, response.StatusCode);
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var jiraIssue = JsonSerializer.Deserialize<JiraIssueResponse>(content, _jsonOptions);
            
            if (jiraIssue == null) return null;
            
            var ticket = MapToTicket(jiraIssue);
            
            // Get comments separately for full detail
            ticket.Comments = await GetTicketCommentsAsync(ticketKey);
            
            return ticket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ticket {Key}", ticketKey);
            return null;
        }
    }

    /// <summary>
    /// Get comments for a specific ticket
    /// </summary>
    public async Task<List<JiraComment>> GetTicketCommentsAsync(string ticketKey)
    {
        if (!_isConfigured) return new List<JiraComment>();
        
        try
        {
            var url = $"/rest/api/3/issue/{ticketKey}/comment";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                return new List<JiraComment>();
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var commentsResponse = JsonSerializer.Deserialize<JiraCommentsResponse>(content, _jsonOptions);
            
            return commentsResponse?.Comments?.Select(c => new JiraComment
            {
                Author = c.Author?.DisplayName ?? "Unknown",
                Body = ExtractTextFromAdf(c.Body),
                Created = c.Created
            }).ToList() ?? new List<JiraComment>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments for ticket {Key}", ticketKey);
            return new List<JiraComment>();
        }
    }

    /// <summary>
    /// Search tickets using JQL (Jira Query Language)
    /// Uses new POST /rest/api/3/search/jql endpoint (required since 2024)
    /// </summary>
    public async Task<List<JiraTicket>> SearchTicketsAsync(string jql, int maxResults = 50)
    {
        if (!_isConfigured) return new List<JiraTicket>();
        
        var tickets = new List<JiraTicket>();
        var startAt = 0;
        
        try
        {
            while (tickets.Count < maxResults)
            {
                var batchSize = Math.Min(50, maxResults - tickets.Count); // Jira max is 50 per request
                
                // Use new POST API (GET /search was deprecated)
                var requestBody = new
                {
                    jql = jql,
                    startAt = startAt,
                    maxResults = batchSize,
                    fields = new[] { "key", "summary", "description", "status", "resolution", "priority", "project", "assignee", "reporter", "created", "resolutiondate", "comment" }
                };
                
                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody, _jsonOptions),
                    Encoding.UTF8,
                    "application/json"
                );
                
                var response = await _httpClient.PostAsync("/rest/api/3/search/jql", jsonContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Jira search failed: {Status} - JQL: {Jql} - Error: {Error}", response.StatusCode, jql, errorContent);
                    break;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<JiraSearchResponse>(content, _jsonOptions);
                
                if (searchResult?.Issues == null || !searchResult.Issues.Any())
                    break;
                
                foreach (var issue in searchResult.Issues)
                {
                    var ticket = MapToTicket(issue);
                    tickets.Add(ticket);
                }
                
                if (searchResult.Issues.Count < batchSize)
                    break; // No more results
                    
                startAt += batchSize;
            }
            
            _logger.LogInformation("Jira search returned {Count} tickets for JQL: {Jql}", tickets.Count, jql);
            return tickets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Jira with JQL: {Jql}", jql);
            return tickets;
        }
    }

    /// <summary>
    /// Check if a ticket has already been harvested
    /// </summary>
    public async Task<bool> IsTicketAlreadyHarvestedAsync(string ticketKey)
    {
        // This is implemented in JiraSolutionStorageService
        await Task.CompletedTask;
        return false;
    }

    /// <summary>
    /// Map Jira API response to our internal model
    /// </summary>
    private JiraTicket MapToTicket(JiraIssueResponse issue)
    {
        return new JiraTicket
        {
            Key = issue.Key ?? "",
            Summary = issue.Fields?.Summary ?? "",
            Description = ExtractTextFromAdf(issue.Fields?.Description),
            Status = issue.Fields?.Status?.Name ?? "",
            Resolution = issue.Fields?.Resolution?.Name ?? "",
            Priority = issue.Fields?.Priority?.Name ?? "",
            Project = issue.Fields?.Project?.Key ?? "",
            Assignee = issue.Fields?.Assignee?.DisplayName ?? "",
            Reporter = issue.Fields?.Reporter?.DisplayName ?? "",
            Created = issue.Fields?.Created ?? DateTime.MinValue,
            Resolved = issue.Fields?.ResolutionDate,
            Comments = issue.Fields?.Comment?.Comments?.Select(c => new JiraComment
            {
                Author = c.Author?.DisplayName ?? "Unknown",
                Body = ExtractTextFromAdf(c.Body),
                Created = c.Created
            }).ToList() ?? new List<JiraComment>()
        };
    }

    /// <summary>
    /// Extract plain text from Atlassian Document Format (ADF)
    /// </summary>
    private string ExtractTextFromAdf(object? adfContent)
    {
        if (adfContent == null) return "";
        
        try
        {
            // ADF is a complex JSON structure, we need to extract text nodes
            var json = adfContent is JsonElement element 
                ? element.GetRawText() 
                : JsonSerializer.Serialize(adfContent);
            
            var doc = JsonSerializer.Deserialize<AdfDocument>(json, _jsonOptions);
            if (doc?.Content == null) return json; // Fallback to raw if not ADF
            
            var sb = new StringBuilder();
            ExtractTextRecursive(doc.Content, sb);
            return sb.ToString().Trim();
        }
        catch
        {
            // If it's plain text, return as-is
            return adfContent?.ToString() ?? "";
        }
    }

    private void ExtractTextRecursive(List<AdfNode>? nodes, StringBuilder sb)
    {
        if (nodes == null) return;
        
        foreach (var node in nodes)
        {
            if (node.Type == "text" && !string.IsNullOrEmpty(node.Text))
            {
                sb.Append(node.Text);
            }
            else if (node.Type == "hardBreak" || node.Type == "paragraph")
            {
                sb.AppendLine();
            }
            
            ExtractTextRecursive(node.Content, sb);
        }
    }

    // JSON serialization options
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #region Jira API Response Models
    
    private class JiraUser
    {
        public string? DisplayName { get; set; }
        public string? EmailAddress { get; set; }
        public string? AccountId { get; set; }
    }
    
    private class JiraSearchResponse
    {
        public int StartAt { get; set; }
        public int MaxResults { get; set; }
        public int Total { get; set; }
        public List<JiraIssueResponse>? Issues { get; set; }
    }
    
    private class JiraIssueResponse
    {
        public string? Key { get; set; }
        public JiraFieldsResponse? Fields { get; set; }
    }
    
    private class JiraFieldsResponse
    {
        public string? Summary { get; set; }
        public object? Description { get; set; } // Can be ADF or string
        public JiraStatusResponse? Status { get; set; }
        public JiraResolutionResponse? Resolution { get; set; }
        public JiraPriorityResponse? Priority { get; set; }
        public JiraProjectResponse? Project { get; set; }
        public JiraUserResponse? Assignee { get; set; }
        public JiraUserResponse? Reporter { get; set; }
        public DateTime Created { get; set; }
        [JsonPropertyName("resolutiondate")]
        public DateTime? ResolutionDate { get; set; }
        public JiraCommentsContainer? Comment { get; set; }
    }
    
    private class JiraStatusResponse
    {
        public string? Name { get; set; }
    }
    
    private class JiraResolutionResponse
    {
        public string? Name { get; set; }
    }
    
    private class JiraPriorityResponse
    {
        public string? Name { get; set; }
    }
    
    private class JiraProjectResponse
    {
        public string? Key { get; set; }
        public string? Name { get; set; }
    }
    
    private class JiraUserResponse
    {
        public string? DisplayName { get; set; }
        public string? EmailAddress { get; set; }
    }
    
    private class JiraCommentsContainer
    {
        public List<JiraCommentResponse>? Comments { get; set; }
    }
    
    private class JiraCommentsResponse
    {
        public List<JiraCommentResponse>? Comments { get; set; }
    }
    
    private class JiraCommentResponse
    {
        public JiraUserResponse? Author { get; set; }
        public object? Body { get; set; } // Can be ADF or string
        public DateTime Created { get; set; }
    }
    
    // Atlassian Document Format (ADF) models
    private class AdfDocument
    {
        public string? Type { get; set; }
        public List<AdfNode>? Content { get; set; }
    }
    
    private class AdfNode
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
        public List<AdfNode>? Content { get; set; }
    }
    
    #endregion
}
