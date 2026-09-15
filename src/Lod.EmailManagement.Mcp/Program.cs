using Azure.Identity;
using Lod.EmailManagement.Mcp.Authentication;
using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.AspNetCore.Authentication;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Azure Key Vault
builder.Configuration.AddAzureKeyVault();

// Configuration options
builder.Services.Configure<KeyVaultOptions>(builder.Configuration.GetSection(KeyVaultOptions.SectionName));
builder.Services.Configure<EmailSendingOptions>(builder.Configuration.GetSection(EmailSendingOptions.SectionName));

// Caching
builder.Services.AddMemoryCache();

// Security & Authentication
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

// Core Services
builder.Services.AddSingleton<IImapClientFactory, ImapClientFactory>();
builder.Services.AddSingleton<ISmtpClientFactory, SmtpClientFactory>();
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

public static class AzureKeyVaultExtensions
{
    public static IConfigurationManager AddAzureKeyVault(this IConfigurationManager configuration)
    {
        string? vaultUri = configuration["AzureKeyVault:VaultUri"] ?? configuration["KeyVault:VaultUri"];

        if (!string.IsNullOrEmpty(vaultUri) && Uri.TryCreate(vaultUri, UriKind.Absolute, out Uri? uri))
        {
            configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
        }

        return configuration;
    }
}
