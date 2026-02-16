using Azure.AI.OpenAI;
using OperationsOneCentre.Components;
using OperationsOneCentre.Extensions;
using OperationsOneCentre.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Enable response compression for faster page loads (Brotli + Gzip)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/javascript", "text/css", "application/json", "text/html", "image/svg+xml"]);
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// Configure Blazor Server SignalR circuit for reliability
// Prevents frequent "Rejoining the server..." disconnections
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(10);  // How long server waits for client ping
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);      // Server-to-client keepalive interval
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);       // Handshake timeout
    options.MaximumReceiveMessageSize = 512 * 1024;            // 512 KB max message size
});

builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10); // Keep circuit alive for 10 min
        options.DisconnectedCircuitMaxRetained = 100;                          // Max retained disconnected circuits
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2);         // JS interop timeout
    });

// Add API Controllers for test endpoints
builder.Services.AddControllers();

// ============================================================================
// Operations One Centre - Clean Architecture Services Registration
// ============================================================================

// Configure Azure OpenAI
var endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"] ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT not set");
var model = builder.Configuration["AZURE_OPENAI_GPT_NAME"] ?? throw new InvalidOperationException("AZURE_OPENAI_GPT_NAME not set");
var apiKey = builder.Configuration["AZURE_OPENAI_API_KEY"] ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY not set");

var azureClient = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));
var embeddingClient = azureClient.GetEmbeddingClient(model);

// Register Azure AI clients
builder.Services.AddSingleton(azureClient);
builder.Services.AddSingleton(embeddingClient);

// Add HttpContextAccessor first (needed by some services)
builder.Services.AddHttpContextAccessor();

// Add all Operations One Centre services with clean architecture pattern
builder.Services.AddStorageServices();      // Azure Blob Storage services
// builder.Services.AddSharePointServices();  // SharePoint KB integration (disabled)
builder.Services.AddConfluenceServices();   // Confluence KB integration
builder.Services.AddSearchServices();       // Vector search with embeddings
builder.Services.AddCachingServices();      // Query caching (Tier 2 optimization)
builder.Services.AddJiraSolutionServices(builder.Configuration); // Jira Solution Harvester (Learning from tickets)
builder.Services.AddTicketLookupServices(); // Real-time Jira ticket lookup for chatbot
builder.Services.AddSapServices();          // SAP specialist agent (Tier 3)
builder.Services.AddNetworkServices();      // Network specialist agent (Tier 3)
builder.Services.AddFeedbackServices();     // Feedback for bot training
builder.Services.AddAgentServices();        // AI RAG Agent with multi-agent routing
builder.Services.AddAuthServices();         // Azure Easy Auth
builder.Services.AddDocumentServices();     // Word/PDF processing

var app = builder.Build();

// Initialize all services that require startup initialization
await app.Services.InitializeServicesWithLoggingAsync(app.Logger);

// Response compression must be first in the pipeline
app.UseResponseCompression();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

// All diagnostic endpoints moved to Controllers/ConfluenceDiagnosticsController.cs
// and Controllers/DiagnosticsController.cs for clean architecture

app.MapStaticAssets();
app.MapControllers(); // API Controllers (Jira test endpoints)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Extension method for initialization with logging
namespace OperationsOneCentre.Extensions
{
    public static class ServiceInitializationExtensions
    {
        public static Task InitializeServicesWithLoggingAsync(this IServiceProvider serviceProvider, ILogger logger)
        {
            // ALL services initialize in BACKGROUND to avoid Azure App Service startup timeout (120s)
            // The app will start immediately and services will be ready shortly after
            
            logger.LogInformation("Starting all service initializations in background...");

            // Initialize ScriptSearchService IN BACKGROUND
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Starting ScriptSearchService initialization...");
                    var scriptService = serviceProvider.GetRequiredService<ScriptSearchService>();
                    await scriptService.InitializeAsync();
                    logger.LogInformation("ScriptSearchService initialized successfully");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to initialize ScriptSearchService - script search may not work correctly");
                }
            });

            // Initialize KnowledgeSearchService IN BACKGROUND
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Starting KnowledgeSearchService initialization...");
                    var knowledgeService = serviceProvider.GetRequiredService<KnowledgeSearchService>();
                    await knowledgeService.InitializeAsync();
                    logger.LogInformation("KnowledgeSearchService initialized successfully");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to initialize KnowledgeSearchService - knowledge search may not work correctly");
                }
            });

            // Initialize KnowledgeImageService IN BACKGROUND
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Starting KnowledgeImageService initialization...");
                    var imageService = serviceProvider.GetRequiredService<KnowledgeImageService>();
                    await imageService.InitializeAsync();
                    logger.LogInformation("KnowledgeImageService initialized successfully");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to initialize KnowledgeImageService - images may not work correctly");
                }
            });

            // Initialize ContextSearchService IN BACKGROUND
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Starting ContextSearchService initialization...");
                    var contextService = serviceProvider.GetRequiredService<ContextSearchService>();
                    await contextService.InitializeAsync();
                    logger.LogInformation("ContextSearchService initialized successfully");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to initialize ContextSearchService - agent context may not work correctly");
                }
            });

            // Initialize Confluence KB service IN BACKGROUND
            var confluenceService = serviceProvider.GetRequiredService<ConfluenceKnowledgeService>();
            if (confluenceService.IsConfigured)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        logger.LogInformation("Starting ConfluenceKnowledgeService initialization...");
                        await confluenceService.InitializeAsync();
                        logger.LogInformation("ConfluenceKnowledgeService initialized with {Count} pages", 
                            confluenceService.GetCachedPageCount());
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to initialize ConfluenceKnowledgeService");
                    }
                });
            }
            else
            {
                logger.LogInformation("ConfluenceKnowledgeService not configured - skipping");
            }

            // Initialize SAP Knowledge service IN BACKGROUND
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Starting SapKnowledgeService initialization...");
                    var sapKnowledgeService = serviceProvider.GetRequiredService<SapKnowledgeService>();
                    await sapKnowledgeService.InitializeAsync();
                    
                    var stats = sapKnowledgeService.GetStatistics();
                    logger.LogInformation("SapKnowledgeService initialized: {Positions} positions, {Roles} roles, {Trans} transactions, {Mappings} mappings",
                        stats.TotalPositions, stats.TotalRoles, stats.TotalTransactions, stats.TotalMappings);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to initialize SapKnowledgeService - SAP queries may not work correctly");
                }
            });

            logger.LogInformation("All service initializations started in background - app is ready to serve requests");
            
            return Task.CompletedTask;
        }
    }
}
