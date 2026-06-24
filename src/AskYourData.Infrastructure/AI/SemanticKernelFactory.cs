using AskYourData.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.Net.Http;

#pragma warning disable SKEXP0070  // Ollama connector is experimental
#pragma warning disable SKEXP0010  // OpenAI embedding generator is experimental

namespace AskYourData.Infrastructure.AI;

/// <summary>
/// Creates and caches a Semantic Kernel instance.
/// Primary: Ollama (local, on-prem). Fallback: OpenAI if enabled and Ollama unreachable.
/// </summary>
public class SemanticKernelFactory
{
    private readonly OllamaOptions _ollamaOpts;
    private readonly OpenAIFallbackOptions _openAIFallbackOpts;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SemanticKernelFactory> _logger;

    private Kernel? _kernel;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SemanticKernelFactory(
        IOptions<OllamaOptions> ollamaOpts,
        IOptions<OpenAIFallbackOptions> openAIFallbackOpts,
        IHttpClientFactory httpClientFactory,
        ILogger<SemanticKernelFactory> logger)
    {
        _ollamaOpts        = ollamaOpts.Value;
        _openAIFallbackOpts = openAIFallbackOpts.Value;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    public async Task<Kernel> GetKernelAsync(CancellationToken cancellationToken = default)
    {
        if (_kernel is not null) return _kernel;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_kernel is not null) return _kernel;

            bool ollamaAvailable = await IsOllamaAvailableAsync(cancellationToken);

            var builder = Kernel.CreateBuilder();

            if (ollamaAvailable)
            {
                _logger.LogInformation("Using Ollama at {BaseUrl}, model={Model}",
                    _ollamaOpts.BaseUrl, _ollamaOpts.ChatModel);

                builder.AddOllamaChatCompletion(
                    modelId: _ollamaOpts.ChatModel,
                    endpoint: new Uri(_ollamaOpts.BaseUrl));

                builder.AddOllamaEmbeddingGenerator(
                    modelId: _ollamaOpts.EmbeddingModel,
                    endpoint: new Uri(_ollamaOpts.BaseUrl));
            }
            else if (_openAIFallbackOpts.Enabled && !string.IsNullOrWhiteSpace(_openAIFallbackOpts.ApiKey))
            {
                _logger.LogWarning("Ollama unavailable — falling back to OpenAI model={Model}",
                    _openAIFallbackOpts.ChatModel);

                builder.AddOpenAIChatCompletion(
                    modelId: _openAIFallbackOpts.ChatModel,
                    apiKey: _openAIFallbackOpts.ApiKey);

                builder.AddOpenAIEmbeddingGenerator(
                    modelId: _openAIFallbackOpts.EmbeddingModel,
                    apiKey: _openAIFallbackOpts.ApiKey);
            }
            else
            {
                throw new InvalidOperationException(
                    "Ollama is unreachable and OpenAI fallback is disabled. " +
                    "Ensure Ollama is running on the host machine.");
            }

            _kernel = builder.Build();
            return _kernel;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<bool> IsOllamaAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            var response = await http.GetAsync($"{_ollamaOpts.BaseUrl}/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
