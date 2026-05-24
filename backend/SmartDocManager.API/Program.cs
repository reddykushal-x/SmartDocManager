using Microsoft.EntityFrameworkCore;
using SmartDocManager.Infrastructure;
using SmartDocManager.Infrastructure.Data;
using SmartDocManager.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "https://smart-doc-manager-frontend.vercel.app"
               )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              // Explicitly expose streaming headers so the browser doesn't block the SSE connection
              .WithExposedHeaders("Content-Type", "Cache-Control", "Connection")
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// Add Infrastructure services
builder.Services.AddInfrastructure(builder.Configuration);

// Add HttpClient for RAGService
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
   app.MapOpenApi();
}

// 1. Explicit Preflight Interceptor Middleware
// This guarantees that ANY browser 'OPTIONS' check gets a clean 200 OK with your CORS origins immediately
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        var origin = context.Request.Headers["Origin"].ToString();
        var allowedOrigins = new[] { "http://localhost:5173", "http://localhost:3000", "https://smart-doc-manager-frontend.vercel.app" };
        
        if (allowedOrigins.Contains(origin))
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", origin);
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, Accept");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
            context.Response.StatusCode = 200;
            await context.Response.CompleteAsync();
            return;
        }
    }
    await next();
});

app.UseCors("AllowReactApp");
app.UseAuthorization();
app.MapControllers();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
    
    // Initialize vector store
    try
    {
        var vectorStoreService = scope.ServiceProvider.GetRequiredService<IVectorStoreService>();
        await vectorStoreService.InitializeVectorStoreAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to initialize vector store. Application will continue without vector search functionality.");
        if (ex.InnerException != null)
        {
            logger.LogError(ex.InnerException, "Inner exception during vector store initialization");
        }
    }
}

app.Run();