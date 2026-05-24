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
                "https://smart-doc-manager-frontend.vercel.app" // Explicit live production storefront
               )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10)); // Optimizes performance by caching preflight responses
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

//app.UseHttpsRedirection();

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
