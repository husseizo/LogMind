using LogMind.API;
using LogMind.API.Middleware;
using LogMind.Core.Interfaces;
using LogMind.Infrastructure.Data;
using LogMind.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LogMindDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=logmind.db"));

// Repositories
builder.Services.AddScoped<ILogRepository, LogRepository>();
builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IExplanationCacheRepository, ExplanationCacheRepository>();
builder.Services.AddScoped<IOperationalKnowledgeRepository, OperationalKnowledgeRepository>();
builder.Services.AddScoped<IOperationalDependencyRepository, OperationalDependencyRepository>();
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();

// Search
builder.Services.AddScoped<KeywordSearchService>();
builder.Services.AddScoped<EmbeddingSearchService>();
builder.Services.AddScoped<ISearchService>(sp => sp.GetRequiredService<EmbeddingSearchService>());

// Ollama settings singleton — shared across all Ollama services, hot-swappable
builder.Services.AddSingleton<OllamaSettings>();

var ollamaBase    = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
var ollamaTimeout = builder.Configuration.GetValue<int>("Ollama:TimeoutSeconds", 120);

builder.Services.AddHttpClient<OllamaAiExplanationService>(client =>
{
    client.BaseAddress = new Uri(ollamaBase);
    client.Timeout = TimeSpan.FromSeconds(ollamaTimeout);
});
// Resolve via the typed-client factory so the HttpClient carries the configured BaseAddress
builder.Services.AddScoped<IAiExplanationService>(sp => sp.GetRequiredService<OllamaAiExplanationService>());

builder.Services.AddHttpClient<OllamaEmbeddingService>(client =>
{
    client.BaseAddress = new Uri(ollamaBase);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IEmbeddingService>(sp => sp.GetRequiredService<OllamaEmbeddingService>());

// Explanation cache orchestrator — wraps IAiExplanationService with the 3-tier cache cascade
builder.Services.AddScoped<ExplanationCacheService>();
builder.Services.AddScoped<SolutionFeedbackService>();

// Notifications — both registered; each checks its own Enabled flag before sending
builder.Services.AddScoped<INotificationService, EmailNotificationService>();
builder.Services.AddHttpClient<TeamsNotificationService>();
builder.Services.AddScoped<INotificationService, TeamsNotificationService>();

// Background services
builder.Services.AddHostedService<LogParserService>();
builder.Services.AddHostedService<AlertDetectionService>();
builder.Services.AddHostedService<EmbeddingIndexService>();
builder.Services.AddHostedService<IncidentCorrelationService>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "LogMind API", Version = "v1" }));

builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(_ => true)   // allows localhost + ngrok + any tunnel
     .AllowAnyHeader()
     .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LogMindDbContext>();
    db.Database.Migrate();
    await TimestampNormalizer.NormalizeAsync(db, app.Logger);
    await OperationalKnowledgeSeeder.SeedAsync(db);
    await OperationalDependencySeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers();
app.Run();
