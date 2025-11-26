namespace RecipeSearchWeb.Services;

/// <summary>
/// Service to fetch latest PowerShell and Microsoft tech news
/// </summary>
public class NewsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NewsService> _logger;

    public NewsService(HttpClient httpClient, ILogger<NewsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<NewsArticle>> GetLatestNewsAsync()
    {
        try
        {
            // Using a simple RSS to JSON service for PowerShell and Microsoft news
            // In production, you might want to use official news APIs
            
            // Simulating news articles for now (you can integrate with RSS feeds or News API)
            return GetStaticNews();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news");
            return GetStaticNews();
        }
    }

    private List<NewsArticle> GetStaticNews()
    {
        return new List<NewsArticle>
        {
            new()
            {
                Title = "PowerShell 7.4 Released with New Features",
                Source = "PowerShell Blog",
                PublishedDate = DateTime.Now.AddDays(-2),
                Url = "https://devblogs.microsoft.com/powershell/",
                Category = "PowerShell",
                Summary = "Microsoft announces PowerShell 7.4 with improved performance, new cmdlets, and enhanced cross-platform support."
            },
            new()
            {
                Title = "Azure Automation Updates for 2025",
                Source = "Azure Updates",
                PublishedDate = DateTime.Now.AddDays(-5),
                Url = "https://azure.microsoft.com/updates/",
                Category = "Azure",
                Summary = "New automation capabilities in Azure including enhanced PowerShell integration and workflow improvements."
            },
            new()
            {
                Title = "Windows Server 2025 Administration Tips",
                Source = "Microsoft Docs",
                PublishedDate = DateTime.Now.AddDays(-7),
                Url = "https://docs.microsoft.com/windows-server/",
                Category = "Windows Server",
                Summary = "Best practices for administering Windows Server 2025 using PowerShell for automation and management."
            },
            new()
            {
                Title = "Microsoft Graph PowerShell SDK v2.0",
                Source = "Microsoft Graph Blog",
                PublishedDate = DateTime.Now.AddDays(-10),
                Url = "https://devblogs.microsoft.com/microsoft365dev/",
                Category = "Microsoft Graph",
                Summary = "Major update to Microsoft Graph PowerShell SDK brings new authentication methods and simplified cmdlets."
            },
            new()
            {
                Title = "Security Best Practices with PowerShell",
                Source = "Microsoft Security",
                PublishedDate = DateTime.Now.AddDays(-12),
                Url = "https://www.microsoft.com/security/",
                Category = "Security",
                Summary = "Essential security considerations when writing and executing PowerShell scripts in enterprise environments."
            }
        };
    }
}

public class NewsArticle
{
    public required string Title { get; set; }
    public required string Source { get; set; }
    public DateTime PublishedDate { get; set; }
    public required string Url { get; set; }
    public required string Category { get; set; }
    public required string Summary { get; set; }
    
    public string TimeAgo
    {
        get
        {
            var span = DateTime.Now - PublishedDate;
            if (span.Days > 0) return $"{span.Days}d ago";
            if (span.Hours > 0) return $"{span.Hours}h ago";
            return $"{span.Minutes}m ago";
        }
    }
    
    public string CategoryEmoji => Category switch
    {
        "PowerShell" => "⚡",
        "Azure" => "☁️",
        "Windows Server" => "🖥️",
        "Microsoft Graph" => "🔗",
        "Security" => "🔒",
        _ => "📰"
    };
}
