using OperationsOneCentre.Interfaces;
using OperationsOneCentre.Models;
using OperationsOneCentre.Domain.Common;

namespace OperationsOneCentre.Services;

/// <summary>
/// Service to get Jira ticket statistics for monitoring dashboard
/// </summary>
public class JiraMonitoringService
{
    private readonly IJiraClient _jiraClient;
    private readonly ILogger<JiraMonitoringService> _logger;
    private readonly IConfiguration _configuration;
    
    // Cache for performance (refresh every 5 minutes)
    private JiraStats? _cachedStats;
    private DateTime _lastCacheTime = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = AppConstants.Cache.JiraMonitoringCacheExpiration;
    
    public JiraMonitoringService(
        IJiraClient jiraClient, 
        IConfiguration configuration,
        ILogger<JiraMonitoringService> logger)
    {
        _jiraClient = jiraClient;
        _configuration = configuration;
        _logger = logger;
    }
    
    public bool IsConfigured => _jiraClient.IsConfigured;
    
    /// <summary>
    /// Get Jira statistics for the monitoring dashboard
    /// </summary>
    public async Task<JiraStats> GetStatsAsync(bool forceRefresh = false)
    {
        // Return cached stats if valid
        if (!forceRefresh && _cachedStats != null && DateTime.UtcNow - _lastCacheTime < _cacheDuration)
        {
            _logger.LogDebug("Returning cached Jira stats");
            return _cachedStats;
        }
        
        if (!_jiraClient.IsConfigured)
        {
            _logger.LogWarning("Jira client not configured, returning empty stats");
            return new JiraStats { IsConfigured = false };
        }
        
        try
        {
            var stats = new JiraStats { IsConfigured = true, LastUpdated = DateTime.UtcNow };
            
            // Get project keys from configuration (comma-separated list, e.g., "MT,MTT")
            // If not configured, search all projects the user has access to
            var projectKeysConfig = _configuration["Jira:ProjectKeys"] ?? _configuration["JIRA_PROJECT_KEYS"] ?? "MT,MTT";
            var projectKeys = projectKeysConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            // Build project filter: "project IN (MT, MTT)" or empty for all projects
            var projectFilter = projectKeys.Length > 0 
                ? $"project IN ({string.Join(", ", projectKeys)}) AND " 
                : "";
            
            // Use CET/CEST timezone for Spain
            var spainTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            var nowInSpain = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spainTimeZone);
            var todayStart = nowInSpain.Date.ToString("yyyy-MM-dd");
            var todayEnd = nowInSpain.Date.AddDays(1).ToString("yyyy-MM-dd");
            
            _logger.LogInformation("Fetching Jira stats for projects {Projects}, date range: {Start} to {End}", 
                string.Join(",", projectKeys), todayStart, todayEnd);
            
            // Execute all JQL queries in parallel for performance
            // Using moderate maxResults - we only need counts and recent items, not all data
            var createdTodayTask = _jiraClient.SearchTicketsAsync(
                $"{projectFilter}created >= {todayStart} AND created < {todayEnd} ORDER BY created DESC", 
                maxResults: 100);
            
            var resolvedTodayTask = _jiraClient.SearchTicketsAsync(
                $"{projectFilter}resolved >= {todayStart} AND resolved < {todayEnd} ORDER BY resolved DESC", 
                maxResults: 100);
            
            var openTicketsTask = _jiraClient.SearchTicketsAsync(
                $"{projectFilter}status NOT IN (Resolved, Closed, Done, Cancelled) ORDER BY created DESC", 
                maxResults: 200);
            
            var inProgressTask = _jiraClient.SearchTicketsAsync(
                $"{projectFilter}status IN (\"In Progress\", \"En curso\", \"Working\") ORDER BY updated DESC", 
                maxResults: 100);
            
            var last7DaysTask = _jiraClient.SearchTicketsAsync(
                $"{projectFilter}created >= -7d ORDER BY created DESC", 
                maxResults: 100);
            
            var resolvedLast7DaysTask = _jiraClient.SearchTicketsAsync(
                $"{projectFilter}resolved >= -7d ORDER BY resolved DESC", 
                maxResults: 100);
            
            // Wait for all queries
            await Task.WhenAll(createdTodayTask, resolvedTodayTask, openTicketsTask, 
                              inProgressTask, last7DaysTask, resolvedLast7DaysTask);
            
            var createdToday = await createdTodayTask;
            var resolvedToday = await resolvedTodayTask;
            var openTickets = await openTicketsTask;
            var inProgress = await inProgressTask;
            var last7Days = await last7DaysTask;
            var resolvedLast7Days = await resolvedLast7DaysTask;
            
            // Fill stats
            stats.TicketsCreatedToday = createdToday.Count;
            stats.TicketsResolvedToday = resolvedToday.Count;
            stats.TicketsOpen = openTickets.Count;
            stats.TicketsInProgress = inProgress.Count;
            
            // Recent tickets for display (last 50 created today)
            stats.RecentTickets = createdToday
                .Take(50)
                .Select(t => new JiraTicketSummary
                {
                    Key = t.Key,
                    Summary = t.Summary,
                    Status = t.Status,
                    Priority = t.Priority,
                    Created = t.Created,
                    Assignee = t.Assignee,
                    Reporter = t.Reporter
                })
                .ToList();
            
            // Calculate trend data for the last 7 days
            stats.TrendData = CalculateTrend(last7Days, resolvedLast7Days, spainTimeZone);
            
            // Calculate average resolution time from resolved tickets
            var resolvedWithTime = resolvedLast7Days
                .Where(t => t.Resolved.HasValue && t.Created != DateTime.MinValue)
                .ToList();
            
            if (resolvedWithTime.Any())
            {
                var avgHours = resolvedWithTime
                    .Average(t => (t.Resolved!.Value - t.Created).TotalHours);
                stats.AverageResolutionHours = Math.Round(avgHours, 1);
            }
            
            // Cache the results
            _cachedStats = stats;
            _lastCacheTime = DateTime.UtcNow;
            
            _logger.LogInformation("Jira stats retrieved: Created={Created}, Resolved={Resolved}, Open={Open}, InProgress={InProgress}", 
                stats.TicketsCreatedToday, stats.TicketsResolvedToday, stats.TicketsOpen, stats.TicketsInProgress);
            
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Jira stats");
            return new JiraStats 
            { 
                IsConfigured = true, 
                HasError = true, 
                ErrorMessage = ex.Message 
            };
        }
    }
    
    /// <summary>
    /// Calculate trend data for the last 7 days
    /// </summary>
    private List<JiraTrendPoint> CalculateTrend(List<JiraTicket> created, List<JiraTicket> resolved, TimeZoneInfo tz)
    {
        var trend = new List<JiraTrendPoint>();
        var nowInSpain = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        
        for (int i = 6; i >= 0; i--)
        {
            var date = nowInSpain.Date.AddDays(-i);
            var dayName = date.ToString("ddd");
            
            var createdCount = created.Count(t => 
                TimeZoneInfo.ConvertTimeFromUtc(t.Created.ToUniversalTime(), tz).Date == date);
            
            var resolvedCount = resolved.Count(t => 
                t.Resolved.HasValue && 
                TimeZoneInfo.ConvertTimeFromUtc(t.Resolved.Value.ToUniversalTime(), tz).Date == date);
            
            trend.Add(new JiraTrendPoint
            {
                Date = date,
                DayLabel = dayName,
                Created = createdCount,
                Resolved = resolvedCount
            });
        }
        
        return trend;
    }
}

/// <summary>
/// Jira statistics model
/// </summary>
public class JiraStats
{
    public bool IsConfigured { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Today's metrics
    public int TicketsCreatedToday { get; set; }
    public int TicketsResolvedToday { get; set; }
    public int TicketsOpen { get; set; }
    public int TicketsInProgress { get; set; }
    
    // Performance metrics
    public double AverageResolutionHours { get; set; }
    
    // Recent tickets
    public List<JiraTicketSummary> RecentTickets { get; set; } = new();
    
    // Trend data (last 7 days)
    public List<JiraTrendPoint> TrendData { get; set; } = new();
}

/// <summary>
/// Summary of a Jira ticket for display
/// </summary>
public class JiraTicketSummary
{
    public string Key { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public DateTime Created { get; set; }
    public string? Assignee { get; set; }
    public string? Reporter { get; set; }
}

/// <summary>
/// Trend data point for charts
/// </summary>
public class JiraTrendPoint
{
    public DateTime Date { get; set; }
    public string DayLabel { get; set; } = "";
    public int Created { get; set; }
    public int Resolved { get; set; }
}
