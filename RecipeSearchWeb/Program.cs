using Azure.AI.OpenAI;
using RecipeSearchWeb.Components;
using RecipeSearchWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Azure OpenAI
var endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"] ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT not set");
var model = builder.Configuration["AZURE_OPENAI_GPT_NAME"] ?? throw new InvalidOperationException("AZURE_OPENAI_GPT_NAME not set");
var apiKey = builder.Configuration["AZURE_OPENAI_API_KEY"] ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY not set");

var azureClient = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));
var embeddingClient = azureClient.GetEmbeddingClient(model);

// Register our services
builder.Services.AddSingleton(embeddingClient);
builder.Services.AddSingleton<ScriptSearchService>();
builder.Services.AddHttpClient<NewsService>();

var app = builder.Build();

// Initialize scripts on startup
var scriptService = app.Services.GetRequiredService<ScriptSearchService>();
await scriptService.InitializeAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
