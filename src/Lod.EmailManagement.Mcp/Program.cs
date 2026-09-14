using Lod.EmailManagement.Mcp.Authentication;
using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Mcp;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.AspNetCore.Authentication;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configuration options
builder.Services.Configure<KeyVaultOptions>(builder.Configuration.GetSection(KeyVaultOptions.SectionName));
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));

// Caching
builder.Services.AddMemoryCache();

// Security & Authentication
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

// Core Services
builder.Services.AddSingleton<ISecretService, KeyVaultSecretService>();
builder.Services.AddSingleton<IImapClientFactory, ImapClientFactory>();
builder.Services.AddScoped<IMailboxService, ImapMailboxService>();

// MCP Services
builder.Services.AddSingleton<McpSessionManager>();
builder.Services.AddScoped<IMcpToolHandler, EmailMcpToolHandler>();

// Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapMcpEndpoints();

app.Run();
