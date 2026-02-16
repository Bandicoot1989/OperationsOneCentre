namespace OperationsOneCentre.Models;

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
    /// User's correction when feedback is negative (👎)
    /// Contains the correct answer or additional information that was missing
    /// Used for auto-learning and enriching context documents
    /// </summary>
    public string? UserCorrection { get; set; }
    
    /// <summary>
    /// Sources (KB articles, Confluence pages, tickets) used to generate the response
    /// Format: "KB-001", "Confluence:12345", "MT-67890"
    /// </summary>
    public List<string> SourcesUsed { get; set; } = new();
    
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

    /// <summary>
    /// Has this feedback been dismissed (not relevant/useful for training)?
    /// </summary>
    public bool IsDismissed { get; set; } = false;
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
    public List<FailurePattern> FailurePatterns { get; set; } = new();
    public int AutoEnrichedKeywords { get; set; }
    public int CachedSuccessfulResponses { get; set; }
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
    public bool WasAutoApplied { get; set; } = false;
    public DateTime? AppliedAt { get; set; }
}

/// <summary>
/// Cached successful query-response pair for learning
/// </summary>
public class SuccessfulResponse
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Query { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string AgentType { get; set; } = "General";
    public float[] QueryEmbedding { get; set; } = Array.Empty<float>();
    public int UseCount { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Pattern of queries that consistently fail
/// </summary>
public class FailurePattern
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PatternDescription { get; set; } = string.Empty;
    public List<string> SampleQueries { get; set; } = new();
    public int FailureCount { get; set; }
    public string? SuggestedAction { get; set; }
    public bool IsAlerted { get; set; } = false;
    public DateTime FirstOccurrence { get; set; } = DateTime.UtcNow;
    public DateTime LastOccurrence { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Auto-learning configuration
/// </summary>
public class AutoLearningConfig
{
    /// <summary>
    /// Minimum occurrences before auto-enriching keywords
    /// </summary>
    public int KeywordEnrichmentThreshold { get; set; } = 3;
    
    /// <summary>
    /// Minimum similarity to use cached response
    /// </summary>
    public double CachedResponseSimilarityThreshold { get; set; } = 0.92;
    
    /// <summary>
    /// Minimum failures before creating an alert
    /// </summary>
    public int FailureAlertThreshold { get; set; } = 5;
    
    /// <summary>
    /// Maximum cached responses to keep
    /// </summary>
    public int MaxCachedResponses { get; set; } = 200;
}
