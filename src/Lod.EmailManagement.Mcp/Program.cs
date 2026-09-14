using Lod.EmailManagement.Mcp.Authentication;
using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Mcp;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.AspNetCore.Authentication;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configuration options
builder.Services.Configure<KeyVaultOptions>(builder.Configuration.GetSection(KeyVaultOptions.SectionName));
builder.Services.Configure<ClientApiKeyOptions>(builder.Configuration.GetSection(ClientApiKeyOptions.SectionName));

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

// MCP Services (Official ModelContextProtocol SDK)
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<EmailMcpTools>();

builder.Services.AddSingleton<McpSessionManager>();
builder.Services.AddScoped<IMcpToolHandler, EmailMcpToolHandler>();

// Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapGet("", () => Results.Redirect("/scalar"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapMcp("/mcp").RequireAuthorization();
app.MapMcpEndpoints();

await app.RunAsync();

public partial class Program { }
