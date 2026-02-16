namespace OperationsOneCentre.Models;

/// <summary>
/// Represents a processed and sanitized solution extracted from a Jira ticket.
/// This is the "clean" knowledge snippet, not the raw ticket data.
/// </summary>
public class JiraSolution
{
    /// <summary>
    /// Unique identifier (typically the Jira ticket key like "IT-5420")
    /// </summary>
    public string TicketId { get; set; } = string.Empty;
    
    /// <summary>
    /// Original ticket summary/title
    /// </summary>
    public string TicketTitle { get; set; } = string.Empty;
    
    /// <summary>
    /// Concise problem description (1-2 sentences, LLM-generated)
    /// </summary>
    public string Problem { get; set; } = string.Empty;
    
    /// <summary>
    /// Root cause identified by the LLM analysis
    /// </summary>
    public string RootCause { get; set; } = string.Empty;
    
    /// <summary>
    /// Solution applied to resolve the issue
    /// </summary>
    public string Solution { get; set; } = string.Empty;
    
    /// <summary>
    /// Step-by-step resolution instructions (max 5-7 steps)
    /// </summary>
    public List<string> Steps { get; set; } = new();
    
    /// <summary>
    /// System/Application affected (e.g., "SAP", "Network", "SharePoint", "Email")
    /// </summary>
    public string System { get; set; } = string.Empty;
    
    /// <summary>
    /// Category for grouping (e.g., "Hardware", "Software", "Access", "Configuration")
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Keywords for search optimization
    /// </summary>
    public List<string> Keywords { get; set; } = new();
    
    /// <summary>
    /// Priority of the original ticket (for context)
    /// </summary>
    public string Priority { get; set; } = string.Empty;
    
    /// <summary>
    /// Date when the ticket was resolved
    /// </summary>
    public DateTime ResolvedDate { get; set; }
    
    /// <summary>
    /// Date when this solution was harvested/processed
    /// </summary>
    public DateTime HarvestedDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Number of times users validated this solution as helpful
    /// Used for boosting in search results
    /// </summary>
    public int ValidationCount { get; set; } = 0;
    
    /// <summary>
    /// Whether this solution has been promoted to official documentation
    /// </summary>
    public bool IsPromoted { get; set; } = false;
    
    /// <summary>
    /// URL to the original Jira ticket (for reference)
    /// </summary>
    public string JiraUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Vector embedding for semantic search
    /// Generated from Problem + Solution + Keywords
    /// </summary>
    public ReadOnlyMemory<float> Embedding { get; set; }
    
    /// <summary>
    /// Returns the searchable text used for embedding generation
    /// </summary>
    public string GetSearchableText()
    {
        var parts = new List<string>
        {
            Problem,
            Solution,
            RootCause,
            string.Join(" ", Keywords),
            System,
            Category
        };
        
        if (Steps.Count > 0)
        {
            parts.Add(string.Join(" ", Steps));
        }
        
        return string.Join(". ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
    
    /// <summary>
    /// Returns a formatted summary for display in chat responses
    /// </summary>
    public string GetFormattedSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"**Ticket**: {TicketId}");
        sb.AppendLine($"**Sistema**: {System}");
        sb.AppendLine($"**Problema**: {Problem}");
        
        if (!string.IsNullOrWhiteSpace(RootCause))
        {
            sb.AppendLine($"**Causa Raíz**: {RootCause}");
        }
        
        sb.AppendLine($"**Solución**: {Solution}");
        
        if (Steps.Count > 0)
        {
            sb.AppendLine("**Pasos**:");
            for (int i = 0; i < Steps.Count; i++)
            {
                sb.AppendLine($"  {i + 1}. {Steps[i]}");
            }
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Storage model for JSON serialization (with float array instead of ReadOnlyMemory)
/// </summary>
public class JiraSolutionStorageModel
{
    public string TicketId { get; set; } = string.Empty;
    public string TicketTitle { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public string System { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public string Priority { get; set; } = string.Empty;
    public DateTime ResolvedDate { get; set; }
    public DateTime HarvestedDate { get; set; }
    public int ValidationCount { get; set; }
    public bool IsPromoted { get; set; }
    public string JiraUrl { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

/// <summary>
/// Raw Jira ticket data before processing
/// </summary>
public class JiraTicket
{
    public string Key { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string Reporter { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime? Resolved { get; set; }
    public List<JiraComment> Comments { get; set; } = new();
    public Dictionary<string, string> CustomFields { get; set; } = new();
}

/// <summary>
/// Jira ticket comment
/// </summary>
public class JiraComment
{
    public string Author { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime Created { get; set; }
}
