using Lod.EmailManagement.Mcp.Authentication;
using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.AspNetCore.Authentication;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configuration options
builder.Services.Configure<KeyVaultOptions>(builder.Configuration.GetSection(KeyVaultOptions.SectionName));

// Caching
builder.Services.AddMemoryCache();

// Security & Authentication
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

// Core Services
builder.Services.AddSingleton<IImapClientFactory, ImapClientFactory>();
builder.Services.AddScoped<IMailboxService, ImapMailboxService>();

// MCP Services (Official ModelContextProtocol SDK)
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<EmailMcpTools>();

WebApplication app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp")
    .RequireAuthorization();

await app.RunAsync();

