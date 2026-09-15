using FluentAssertions;
using Lod.EmailManagement.Mcp.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Lod.EmailManagement.Mcp.Tests;

public class ApiKeyAuthenticationTests
{
    [Fact]
    public async Task ValidateKeyMatchesKeyFromJsonArrayInKeyVault()
    {
        ReadOnlySpan<byte> json = """
                                  {
                                    "Authentication": {
                                      "ApiKeys": [
                                        "vault-key-1",
                                        "vault-key-2"
                                      ]
                                    }
                                  }
                                  """u8;

        IConfiguration config = new ConfigurationBuilder().AddJsonStream(new MemoryStream(json.ToArray())).Build();
        ApiKeyValidator validator = new(config);

        (await validator.ValidateKey("vault-key-1")).Should().BeTrue();
        (await validator.ValidateKey("vault-key-2")).Should().BeTrue();
        (await validator.ValidateKey("vault-key-3")).Should().BeFalse();
        (await validator.ValidateKey("wrong-key")).Should().BeFalse();
    }

    [Fact]
    public async Task ValidateKeyRejectsEmptyOrWhitespace()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        ApiKeyValidator validator = new(config);

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
        context.Request.Headers.Authorization = "Bearer bearer-token-123";

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
