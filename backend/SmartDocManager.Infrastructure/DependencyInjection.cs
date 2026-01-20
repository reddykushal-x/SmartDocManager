using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using SmartDocManager.Application.Interfaces;
using SmartDocManager.Infrastructure.Data;
using SmartDocManager.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http;


namespace SmartDocManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
       this IServiceCollection services,
       IConfiguration configuration)
    {
        // 1. Database & Basic Services
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=SmartDocManager.db"));

        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IAIService, SemanticKernelAIService>();
        services.AddScoped<IVectorStoreService, VectorStoreService>();
        services.AddScoped<IRAGService, RAGService>();

        // 2. Setup AI Configuration
        var groqApiKey = configuration["Groq:ApiKey"];
        var groqModelId = configuration["Groq:ModelId"] ?? "llama-3.3-70b-versatile";
        var groqEndpoint = configuration["Groq:Endpoint"] ?? "https://api.groq.com/openai/v1";

        // 3. Register the Kernel
        services.AddTransient<Kernel>(sp =>
        {
            var kernelBuilder = Kernel.CreateBuilder();

            if (!string.IsNullOrWhiteSpace(groqApiKey))
            {
                // Use the handler to force the reroute to Groq
                var httpClient = new HttpClient(new GroqRouteHandler());

                var chatService = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIChatCompletionService(
                    modelId: groqModelId,
                    apiKey: groqApiKey,
                    httpClient: httpClient
                );

                kernelBuilder.Services.AddKeyedSingleton<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>(null, chatService);
                kernelBuilder.Services.AddKeyedSingleton<Microsoft.SemanticKernel.TextGeneration.ITextGenerationService>(null, chatService);
            }

            return kernelBuilder.Build();
        });

        return services;
    }
}

public class GroqRouteHandler : DelegatingHandler
{
    public GroqRouteHandler() : base(new HttpClientHandler()) { }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Intercept the request and swap the host to Groq
        var groqUri = new UriBuilder(request.RequestUri!)
        {
            Host = "api.groq.com",
            Path = "/openai" + request.RequestUri.AbsolutePath
        };

        request.RequestUri = groqUri.Uri;
        return base.SendAsync(request, cancellationToken);
    }
}