namespace OperationsOneCentre.Domain.Common;

/// <summary>
/// Centralized application constants. Eliminates hardcoded URLs, magic numbers,
/// and other scattered literal values across services.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// Jira-related constants
    /// </summary>
    public static class Jira
    {
        /// <summary>
        /// Configuration key for the Jira instance base URL.
        /// Services should read this instead of hardcoding "https://antolin.atlassian.net".
        /// </summary>
        public const string BaseUrlConfigKey = "Jira:BaseUrl";
        
        /// <summary>
        /// Fallback Jira browse URL template. Use {0} for ticket key.
        /// </summary>
        public const string BrowseUrlTemplate = "{0}/browse/{1}";
        
        /// <summary>
        /// Configuration key for fallback ticket creation links
        /// </summary>
        public const string FallbackTicketUrlConfigKey = "Jira:FallbackTicketUrl";
        public const string FallbackSapTicketUrlConfigKey = "Jira:FallbackSapTicketUrl";
        public const string FallbackNetworkTicketUrlConfigKey = "Jira:FallbackNetworkTicketUrl";
    }

    /// <summary>
    /// Search/AI thresholds — configurable via IConfiguration
    /// </summary>
    public static class Search
    {
        /// <summary>
        /// Default minimum cosine similarity score to consider a document relevant.
        /// </summary>
        public const double DefaultRelevanceThreshold = 0.65;

        /// <summary>
        /// Semantic cache similarity threshold — queries above this are considered duplicates.
        /// </summary>
        public const double SemanticCacheSimilarityThreshold = 0.95;

        /// <summary>
        /// Reciprocal Rank Fusion constant (industry standard is 60).
        /// </summary>
        public const int RrfConstant = 60;
    }

    /// <summary>
    /// Cache duration constants
    /// </summary>
    public static class Cache
    {
        public static readonly TimeSpan QueryResultExpiration = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan EmbeddingExpiration = TimeSpan.FromHours(24);
        public static readonly TimeSpan SearchResultExpiration = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan JiraMonitoringCacheExpiration = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Harvester timing
    /// </summary>
    public static class Harvester
    {
        public static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(6);
        public static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Limits and guards
    /// </summary>
    public static class Limits
    {
        /// <summary>Maximum pages to fetch from Confluence per space (prevent runaway loops).</summary>
        public const int MaxConfluencePagesPerSpace = 2000;
        
        /// <summary>Maximum entries in the semantic cache.</summary>
        public const int MaxSemanticCacheEntries = 500;
        
        /// <summary>Maximum Jira tickets to fetch per query.</summary>
        public const int MaxJiraTicketsPerQuery = 100;
    }
}
