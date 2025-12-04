namespace RecipeSearchWeb.Models;

/// <summary>
/// Represents user feedback on a chat response
/// Used for auto-learning and improving the bot
/// </summary>
public class ChatFeedback
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The user's original question
    /// </summary>
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// The bot's response
    /// </summary>
    public string Response { get; set; } = string.Empty;
    
    /// <summary>
    /// User rating: true = helpful (👍), false = not helpful (👎)
    /// </summary>
    public bool IsHelpful { get; set; }
    
    /// <summary>
    /// Optional user comment explaining the feedback
    /// </summary>
    public string? Comment { get; set; }
    
    /// <summary>
    /// Which agent handled this query (SAP, Network, General)
    /// </summary>
    public string AgentType { get; set; } = "General";
    
    /// <summary>
    /// Search scores from the query (for analysis)
    /// </summary>
    public double BestSearchScore { get; set; }
    
    /// <summary>
    /// Was this a low-confidence response?
    /// </summary>
    public bool WasLowConfidence { get; set; }
    
    /// <summary>
    /// Keywords extracted from the query (for auto-learning)
    /// </summary>
    public List<string> ExtractedKeywords { get; set; } = new();
    
    /// <summary>
    /// Suggested keywords to add (populated by auto-learning)
    /// </summary>
    public List<string> SuggestedKeywords { get; set; } = new();
    
    /// <summary>
    /// Context document IDs that were used (if any)
    /// </summary>
    public List<string> ContextDocumentIds { get; set; } = new();
    
    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// User who provided feedback (if available)
    /// </summary>
    public string? UserId { get; set; }
    
    /// <summary>
    /// Has this feedback been reviewed by admin?
    /// </summary>
    public bool IsReviewed { get; set; } = false;
    
    /// <summary>
    /// Has the suggested improvement been applied?
    /// </summary>
    public bool IsApplied { get; set; } = false;
}

/// <summary>
/// Summary statistics for feedback
/// </summary>
public class FeedbackStats
{
    public int TotalFeedback { get; set; }
    public int PositiveFeedback { get; set; }
    public int NegativeFeedback { get; set; }
    public double SatisfactionRate { get; set; }
    public int UnreviewedCount { get; set; }
    public int LowConfidenceCount { get; set; }
    public List<KeywordSuggestion> TopSuggestions { get; set; } = new();
}

/// <summary>
/// A keyword suggestion from auto-learning
/// </summary>
public class KeywordSuggestion
{
    public string Keyword { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public List<string> RelatedQueries { get; set; } = new();
    public string? SuggestedForDocument { get; set; }
}
