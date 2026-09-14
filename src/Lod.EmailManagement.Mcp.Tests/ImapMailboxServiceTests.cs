using FluentAssertions;
using Lod.EmailManagement.Mcp.Models;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Lod.EmailManagement.Mcp.Tests;

public class ImapMailboxServiceTests
{
    private readonly Mock<IImapClientFactory> _clientFactoryMock = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();
    private readonly Mock<ILogger<ImapMailboxService>> _loggerMock = new();

    [Fact]
    public async Task SendEmailLogsWarningAndThrowsNotImplementedException()
    {
        ImapMailboxService service = new(_clientFactoryMock.Object, _configuration, _loggerMock.Object);

        SendEmailRequest request = new(
            ["recipient@example.com"],
            "Important Update",
            "This is the body");

        Func<Task> act = async () => await service.SendEmail("primary", request);

        await act.Should().ThrowAsync<NotImplementedException>()
            .WithMessage("*Sending email is not implemented*");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempted to send email")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
