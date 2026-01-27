using RecipeSearchWeb.Models;

namespace RecipeSearchWeb.Interfaces;

/// <summary>
/// Service contract for managing chat feedback and auto-learning
/// Stores feedback in Azure Blob Storage for analysis and improvement
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// Initialize service and load existing feedback from Azure Blob Storage
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Submit feedback for a chat response
    /// </summary>
    Task<ChatFeedback> SubmitFeedbackAsync(
        string query,
        string response,
        bool isHelpful,
        string? comment = null,
        string? userId = null,
        string agentType = "General",
        double bestScore = 0,
        bool wasLowConfidence = false);

    /// <summary>
    /// Submit feedback with user correction (for negative feedback)
    /// Automatically enriches context documents with the correction
    /// </summary>
    Task<ChatFeedback> SubmitFeedbackWithCorrectionAsync(
        string query,
        string response,
        string userCorrection,
        List<string> sourcesUsed,
        string? userId = null,
        string agentType = "General",
        double bestScore = 0,
        bool wasLowConfidence = false);

    /// <summary>
    /// Get all feedback entries
    /// </summary>
    Task<List<ChatFeedback>> GetAllFeedbackAsync();

    /// <summary>
    /// Get only negative feedback (for improvement analysis)
    /// </summary>
    Task<List<ChatFeedback>> GetNegativeFeedbackAsync();

    /// <summary>
    /// Get unreviewed feedback
    /// </summary>
    Task<List<ChatFeedback>> GetUnreviewedFeedbackAsync();

    /// <summary>
    /// Get feedback statistics
    /// </summary>
    Task<FeedbackStats> GetStatsAsync();

    /// <summary>
    /// Mark feedback as reviewed
    /// </summary>
    Task MarkAsReviewedAsync(string feedbackId);

    /// <summary>
    /// Mark feedback improvement as applied
    /// </summary>
    Task MarkAsAppliedAsync(string feedbackId);

    /// <summary>
    /// Dismiss feedback (not relevant for training)
    /// </summary>
    Task DismissFeedbackAsync(string feedbackId);

    /// <summary>
    /// Check if the service is properly configured and can persist data
    /// </summary>
    Task<(bool IsOperational, string Message)> CheckHealthAsync();

    /// <summary>
    /// Clean up old feedback (older than specified days)
    /// </summary>
    Task CleanupOldFeedbackAsync(int daysToKeep = 90);
}
