using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Lod.EmailManagement.Mcp.Authentication;
using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Lod.EmailManagement.Mcp.Tests;

public class ApiKeyAuthenticationTests
{
    private readonly Mock<ISecretService> _secretServiceMock = new();
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
    private readonly IOptions<ClientApiKeyOptions> _options = Microsoft.Extensions.Options.Options.Create(new ClientApiKeyOptions
    {
        Keys = ["config-key-123"],
        CacheDurationMinutes = 5
    });

    [Fact]
    public async Task ValidateKeyMatchesKeyFromJsonArrayInKeyVault()
    {
        _secretServiceMock.Setup(s => s.GetSecret("ApiKeys", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"vault-key-1\", \"vault-key-2\"]");

        IConfiguration config = new ConfigurationBuilder().Build();
        ApiKeyValidator validator = new(
            _secretServiceMock.Object,
            config,
            _options,
            _memoryCache,
            NullLogger<ApiKeyValidator>.Instance);

        bool isValid = await validator.ValidateKey("vault-key-2");
        bool isInvalid = await validator.ValidateKey("wrong-key");

        isValid.Should().BeTrue();
        isInvalid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateKeyMatchesKeyFromCommaSeparatedStringInKeyVault()
    {
        _secretServiceMock.Setup(s => s.GetSecret("ApiKeys", It.IsAny<CancellationToken>()))
            .ReturnsAsync("vault-comma-1, vault-comma-2");

        IConfiguration config = new ConfigurationBuilder().Build();
        ApiKeyValidator validator = new(
            _secretServiceMock.Object,
            config,
            _options,
            _memoryCache,
            NullLogger<ApiKeyValidator>.Instance);

        bool isValid = await validator.ValidateKey("vault-comma-1");

        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateKeyMatchesKeyFromLocalConfiguration()
    {
        _secretServiceMock.Setup(s => s.GetSecret(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        IConfiguration config = new ConfigurationBuilder().Build();
        ApiKeyValidator validator = new(
            _secretServiceMock.Object,
            config,
            _options,
            _memoryCache,
            NullLogger<ApiKeyValidator>.Instance);

        bool isValid = await validator.ValidateKey("config-key-123");

        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateKeyRejectsEmptyOrWhitespace()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        ApiKeyValidator validator = new(
            _secretServiceMock.Object,
            config,
            _options,
            _memoryCache,
            NullLogger<ApiKeyValidator>.Instance);

        bool isNullValid = await validator.ValidateKey(string.Empty);
        bool isSpaceValid = await validator.ValidateKey("   ");

        isNullValid.Should().BeFalse();
        isSpaceValid.Should().BeFalse();
    }

    [Fact]
    public async Task HandlerAuthenticatesWithHeader()
    {
        Mock<IApiKeyValidator> validatorMock = new();
        validatorMock.Setup(v => v.ValidateKey("test-header-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ApiKeyAuthenticationHandler handler = CreateHandler(validatorMock.Object);

        DefaultHttpContext context = new();
        context.Request.Headers["X-API-Key"] = "test-header-key";

        await handler.InitializeAsync(
            new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler)),
            context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal?.Identity?.IsAuthenticated.Should().BeTrue();
        result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("McpClient");
    }

    [Fact]
    public async Task HandlerAuthenticatesWithBearerAuthorizationHeader()
    {
        Mock<IApiKeyValidator> validatorMock = new();
        validatorMock.Setup(v => v.ValidateKey("bearer-token-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ApiKeyAuthenticationHandler handler = CreateHandler(validatorMock.Object);

        DefaultHttpContext context = new();
        context.Request.Headers["Authorization"] = "Bearer bearer-token-123";

        await handler.InitializeAsync(
            new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler)),
            context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandlerAuthenticatesWithQueryParameter()
    {
        Mock<IApiKeyValidator> validatorMock = new();
        validatorMock.Setup(v => v.ValidateKey("query-token-456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ApiKeyAuthenticationHandler handler = CreateHandler(validatorMock.Object);

        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString("?apiKey=query-token-456");

        await handler.InitializeAsync(
            new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler)),
            context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandlerFailsWhenKeyIsInvalid()
    {
        Mock<IApiKeyValidator> validatorMock = new();
        validatorMock.Setup(v => v.ValidateKey(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ApiKeyAuthenticationHandler handler = CreateHandler(validatorMock.Object);

        DefaultHttpContext context = new();
        context.Request.Headers["X-API-Key"] = "invalid-key";

        await handler.InitializeAsync(
            new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler)),
            context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure?.Message.Should().Contain("Invalid API Key");
    }

    [Fact]
    public async Task HandlerReturnsNoResultWhenNoKeyProvided()
    {
        Mock<IApiKeyValidator> validatorMock = new();
        ApiKeyAuthenticationHandler handler = CreateHandler(validatorMock.Object);

        DefaultHttpContext context = new();

        await handler.InitializeAsync(
            new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler)),
            context);

        AuthenticateResult result = await handler.AuthenticateAsync();

        result.None.Should().BeTrue();
    }

    private static ApiKeyAuthenticationHandler CreateHandler(IApiKeyValidator validator)
    {
        Mock<IOptionsMonitor<AuthenticationSchemeOptions>> optionsMock = new();
        optionsMock.Setup(o => o.Get(It.IsAny<string>()))
            .Returns(new AuthenticationSchemeOptions());

        Mock<ILoggerFactory> loggerFactoryMock = new();
        loggerFactoryMock.Setup(l => l.CreateLogger(It.IsAny<string>()))
            .Returns(NullLogger.Instance);

        return new ApiKeyAuthenticationHandler(
            optionsMock.Object,
            loggerFactoryMock.Object,
            UrlEncoder.Default,
            validator);
    }
}
