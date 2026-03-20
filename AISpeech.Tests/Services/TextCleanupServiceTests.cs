using System.Net;
using System.Text;
using System.Text.Json;
using AISpeech.Services;
using FluentAssertions;
using Xunit;

namespace AISpeech.Tests.Services;

public class TextCleanupServiceTests
{
    private static AppSettings CreateConfiguredSettings() => new()
    {
        AzureOpenAIEndpoint = "https://test.openai.azure.com",
        AzureOpenAIApiKey = "test-api-key",
        AzureOpenAIDeployment = "gpt-4",
        AzureOpenAIApiVersion = "2024-02-15-preview"
    };

    private static AppSettings CreateUnconfiguredSettings() => new()
    {
        AzureOpenAIEndpoint = "",
        AzureOpenAIApiKey = "",
        AzureOpenAIDeployment = ""
    };

    private static string CreateChatCompletionResponse(string content)
    {
        return JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new { role = "assistant", content }
                }
            }
        });
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public FakeHandler(HttpStatusCode statusCode, string responseBody)
        {
            _handler = _ => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return await _handler(request);
        }
    }

    // --- IsConfigured ---

    [Fact]
    public void IsConfigured_AllFieldsPresent_ReturnsTrue()
    {
        using var service = new TextCleanupService(CreateConfiguredSettings());

        service.IsConfigured.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "key", "deployment")]
    [InlineData("https://test.openai.azure.com", "", "deployment")]
    [InlineData("https://test.openai.azure.com", "key", "")]
    public void IsConfigured_MissingField_ReturnsFalse(string endpoint, string apiKey, string deployment)
    {
        var settings = new AppSettings
        {
            AzureOpenAIEndpoint = endpoint,
            AzureOpenAIApiKey = apiKey,
            AzureOpenAIDeployment = deployment
        };
        using var service = new TextCleanupService(settings);

        service.IsConfigured.Should().BeFalse();
    }

    // --- Unconfigured passthrough ---

    [Fact]
    public async Task CleanupAsync_NotConfigured_ReturnsRawText()
    {
        using var service = new TextCleanupService(CreateUnconfiguredSettings());

        var result = await service.CleanupAsync("um hello world", CaptureMode.Transcribe);

        result.Should().Be("um hello world");
    }

    // --- Successful requests ---

    [Fact]
    public async Task CleanupAsync_SuccessfulResponse_ReturnsCleanedText()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, CreateChatCompletionResponse("Hello world."));
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        var result = await service.CleanupAsync("um hello world", CaptureMode.Transcribe);

        result.Should().Be("Hello world.");
    }

    [Fact]
    public async Task CleanupAsync_TrimsWhitespaceFromResponse()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, CreateChatCompletionResponse("  Hello world.  "));
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        var result = await service.CleanupAsync("hello", CaptureMode.Transcribe);

        result.Should().Be("Hello world.");
    }

    // --- Request formatting ---

    [Fact]
    public async Task CleanupAsync_SendsCorrectUrl()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, CreateChatCompletionResponse("ok"));
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        await service.CleanupAsync("test", CaptureMode.Transcribe);

        handler.LastRequest!.RequestUri!.ToString().Should().Be(
            "https://test.openai.azure.com/openai/deployments/gpt-4/chat/completions?api-version=2024-02-15-preview");
    }

    [Fact]
    public async Task CleanupAsync_SendsApiKeyHeader()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, CreateChatCompletionResponse("ok"));
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        await service.CleanupAsync("test", CaptureMode.Transcribe);

        handler.LastRequest!.Headers.GetValues("api-key").Should().ContainSingle("test-api-key");
    }

    [Fact]
    public async Task CleanupAsync_SendsUserTextInRequestBody()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, CreateChatCompletionResponse("ok"));
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        await service.CleanupAsync("the raw speech text", CaptureMode.Transcribe);

        var body = JsonDocument.Parse(handler.LastRequestBody!);
        var messages = body.RootElement.GetProperty("messages");
        messages[1].GetProperty("role").GetString().Should().Be("user");
        messages[1].GetProperty("content").GetString().Should().Be("the raw speech text");
    }

    [Fact]
    public async Task CleanupAsync_SetsMaxCompletionTokens()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, CreateChatCompletionResponse("ok"));
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        await service.CleanupAsync("test", CaptureMode.Transcribe);

        var body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("max_completion_tokens").GetInt32().Should().Be(4096);
    }

    // --- Mode-specific system prompts ---

    [Theory]
    [InlineData(CaptureMode.Transcribe, "speech-to-text cleanup")]
    [InlineData(CaptureMode.Prompt, "prompt refinement")]
    [InlineData(CaptureMode.Ralph, "prompt refinement")]
    public async Task CleanupAsync_UsesCorrectSystemPromptForMode(CaptureMode mode, string expectedFragment)
    {
        var handler = new FakeHandler(HttpStatusCode.OK, CreateChatCompletionResponse("ok"));
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        await service.CleanupAsync("test", mode);

        var body = JsonDocument.Parse(handler.LastRequestBody!);
        var systemContent = body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        systemContent.Should().Contain(expectedFragment);
    }

    [Fact]
    public async Task CleanupAsync_RalphMode_ContainsTaskPlanInstructions()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, CreateChatCompletionResponse("ok"));
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        await service.CleanupAsync("test", CaptureMode.Ralph);

        var body = JsonDocument.Parse(handler.LastRequestBody!);
        var systemContent = body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        systemContent.Should().Contain("plan-<ticket");
    }

    // --- Error handling ---

    [Fact]
    public async Task CleanupAsync_Non2xxResponse_ThrowsHttpRequestException()
    {
        var handler = new FakeHandler(HttpStatusCode.BadRequest, """{"error":"bad request"}""");
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        var act = () => service.CleanupAsync("test", CaptureMode.Transcribe);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*400*");
    }

    [Fact]
    public async Task CleanupAsync_InternalServerError_ThrowsWithStatusCode()
    {
        var handler = new FakeHandler(HttpStatusCode.InternalServerError, """{"error":"oops"}""");
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        var act = () => service.CleanupAsync("test", CaptureMode.Transcribe);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*500*");
    }

    [Fact]
    public async Task CleanupAsync_EmptyContent_ThrowsInvalidOperationException()
    {
        var response = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { role = "assistant", content = (string?)null } }
            }
        });
        var handler = new FakeHandler(HttpStatusCode.OK, response);
        using var service = new TextCleanupService(CreateConfiguredSettings(), handler);

        var act = () => service.CleanupAsync("test", CaptureMode.Transcribe);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*empty content*");
    }

    [Fact]
    public async Task CleanupAsync_EndpointTrailingSlash_StrippedFromUrl()
    {
        var settings = CreateConfiguredSettings();
        settings.AzureOpenAIEndpoint = "https://test.openai.azure.com/";
        var handler = new FakeHandler(HttpStatusCode.OK, CreateChatCompletionResponse("ok"));
        using var service = new TextCleanupService(settings, handler);

        await service.CleanupAsync("test", CaptureMode.Transcribe);

        handler.LastRequest!.RequestUri!.ToString().Should().StartWith("https://test.openai.azure.com/openai/");
    }
}
