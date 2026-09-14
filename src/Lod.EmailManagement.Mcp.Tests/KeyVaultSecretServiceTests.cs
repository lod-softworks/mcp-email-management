using FluentAssertions;
using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lod.EmailManagement.Mcp.Tests;

public class KeyVaultSecretServiceTests
{
    private readonly IOptions<KeyVaultOptions> _emptyKeyVaultOptions = Options.Create(new KeyVaultOptions { VaultUri = string.Empty });

    [Theory]
    [InlineData("Passwords--email1@firstdomain.net", "Passwords--email1-firstdomain-net")]
    [InlineData("Passwords:second-email@diffdomain.com", "Passwords--second-email-diffdomain-com")]
    [InlineData("email1@firstdomain.net", "email1-firstdomain-net")]
    [InlineData("Mailboxes--primary--Password", "Mailboxes--primary--Password")]
    public void SanitizeKeyVaultSecretNameReplacesDisallowedCharactersWithHyphen(string input, string expected)
    {
        string sanitized = KeyVaultSecretService.SanitizeKeyVaultSecretName(input);

        sanitized.Should().Be(expected);
    }

    [Fact]
    public async Task GetSecretResolvesPasswordFromPasswordsSectionUsingDoubleDash()
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            ["Passwords:email1@firstdomain.net"] = "asdfasdf",
            ["Passwords:second-email@diffdomain.com"] = "woah"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        KeyVaultSecretService service = new(
            _emptyKeyVaultOptions,
            configuration,
            NullLogger<KeyVaultSecretService>.Instance);

        string? result = await service.GetSecret("Passwords--email1@firstdomain.net");

        result.Should().Be("asdfasdf");
    }

    [Fact]
    public async Task GetSecretResolvesPasswordFromPasswordsSectionUsingColon()
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            ["Passwords:second-email@diffdomain.com"] = "woah"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        KeyVaultSecretService service = new(
            _emptyKeyVaultOptions,
            configuration,
            NullLogger<KeyVaultSecretService>.Instance);

        string? result = await service.GetSecret("Passwords:second-email@diffdomain.com");

        result.Should().Be("woah");
    }

    [Fact]
    public async Task GetSecretResolvesDirectConfigurationKey()
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            ["Mailboxes--primary--Password"] = "super-secret"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        KeyVaultSecretService service = new(
            _emptyKeyVaultOptions,
            configuration,
            NullLogger<KeyVaultSecretService>.Instance);

        string? result = await service.GetSecret("Mailboxes--primary--Password");

        result.Should().Be("super-secret");
    }

    [Fact]
    public async Task GetSecretReturnsNullWhenSecretDoesNotExist()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        KeyVaultSecretService service = new(
            _emptyKeyVaultOptions,
            configuration,
            NullLogger<KeyVaultSecretService>.Instance);

        string? result = await service.GetSecret("Passwords--nonexistent@example.com");

        result.Should().BeNull();
    }
}
