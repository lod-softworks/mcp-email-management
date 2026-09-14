using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Mcp;
using Lod.EmailManagement.Mcp.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configuration options
builder.Services.Configure<KeyVaultOptions>(builder.Configuration.GetSection(KeyVaultOptions.SectionName));

// Services
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
app.MapControllers();
app.MapMcpEndpoints();

app.Run();
