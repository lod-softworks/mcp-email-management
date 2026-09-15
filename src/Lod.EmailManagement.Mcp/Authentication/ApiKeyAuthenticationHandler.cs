using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Lod.EmailManagement.Mcp.Authentication;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyValidator apiKeyValidator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-API-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? apiKey = ExtractApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.NoResult();
        }

        bool isValid = await apiKeyValidator.ValidateKey(apiKey, Context.RequestAborted);
        if (!isValid)
        {
            return AuthenticateResult.Fail("Invalid API Key.");
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, "McpClient"),
            new(ClaimTypes.Name, "AuthorizedAgent"),
            new(ClaimTypes.AuthenticationMethod, SchemeName)
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    private string? ExtractApiKey()
    {
        // 1. Check 'X-API-Key' header
        if (Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues headerValue) && !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString().Trim();
        }

        // 2. Check 'Authorization' header (Bearer <key> or ApiKey <key>)
        if (Request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues authHeader) && !string.IsNullOrWhiteSpace(authHeader))
        {
            string authString = authHeader.ToString().Trim();
            if (authString.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authString["Bearer ".Length..].Trim();
            }

            if (authString.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
            {
                return authString["ApiKey ".Length..].Trim();
            }
        }

        // 3. Check query parameters (for SSE EventSource clients that cannot send custom headers)
        if (Request.Query.TryGetValue("apiKey", out Microsoft.Extensions.Primitives.StringValues queryApiKey) && !string.IsNullOrWhiteSpace(queryApiKey))
        {
            return queryApiKey.ToString().Trim();
        }

        if (Request.Query.TryGetValue("api_key", out Microsoft.Extensions.Primitives.StringValues queryApiKeySnake) && !string.IsNullOrWhiteSpace(queryApiKeySnake))
        {
            return queryApiKeySnake.ToString().Trim();
        }

        return null;
    }
}
