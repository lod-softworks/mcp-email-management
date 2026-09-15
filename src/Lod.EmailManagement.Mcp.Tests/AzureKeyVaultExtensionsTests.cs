using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Lod.EmailManagement.Mcp.Tests;

public class AzureKeyVaultExtensionsTests
{
    [Fact]
    public void AddAzureKeyVaultReturnsConfigurationWhenNoVaultUriConfigured()
    {
        ConfigurationManager configuration = new();

        IConfigurationManager result = configuration.AddAzureKeyVault();

        result.Should().BeSameAs(configuration);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddAzureKeyVaultReturnsConfigurationWhenVaultUriIsEmptyOrWhitespace(string vaultUri)
    {
        ConfigurationManager configuration = new();
        configuration["KeyVault:VaultUri"] = vaultUri;

        IConfigurationManager result = configuration.AddAzureKeyVault();

        result.Should().BeSameAs(configuration);
    }

    [Fact]
    public void AddAzureKeyVaultReturnsConfigurationWhenVaultUriIsInvalid()
    {
        ConfigurationManager configuration = new();
        configuration["KeyVault:VaultUri"] = "not-a-valid-uri";

        IConfigurationManager result = configuration.AddAzureKeyVault();

        result.Should().BeSameAs(configuration);
    }

    [Fact]
    public void AddAzureKeyVaultChecksAzureKeyVaultPrefixBeforeKeyVault()
    {
        ConfigurationManager configuration = new();
        configuration["AzureKeyVault:VaultUri"] = "";
        configuration["KeyVault:VaultUri"] = "invalid-uri";

        IConfigurationManager result = configuration.AddAzureKeyVault();

        result.Should().BeSameAs(configuration);
    }
}
