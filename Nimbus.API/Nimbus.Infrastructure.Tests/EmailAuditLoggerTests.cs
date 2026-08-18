using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nimbus.Application.Abstraction;
using Nimbus.Infrastructure.Persistence;
using Nimbus.Infrastructure.Services;
using Testcontainers.MsSql;
namespace Nimbus.Infrastructure.Tests;

/// <summary>
///     Integration tests for <see cref="EmailAuditLogger" /> (issue #128's audit-row
///     requirement) against a real SQL Server instance, consistent with
///     <see cref="AppDbContextMigrationTests" />.
/// </summary>
[TestFixture]
public class EmailAuditLoggerTests
{
    private MsSqlContainer _sqlServer = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _sqlServer.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        await _sqlServer.DisposeAsync();
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_sqlServer.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    [Test]
    public async Task LogAsync_WritesAnAuditRow_ForASuccessfulSend()
    {
        await using var db = CreateContext();
        var sut = new EmailAuditLogger(db, NullLogger<EmailAuditLogger>.Instance);

        var message = new EmailMessage
        {
            ToAddress = "person@example.com",
            Subject = "Reset your password",
            HtmlBody = "<p>hi</p>",
            TextBody = "hi",
            Template = "password-reset"
        };

        await sut.LogAsync(message, EmailSendResult.Success("provider-id-123"));

        var row = await db.SentEmails.SingleAsync(s => s.Recipient == "person@example.com");
        Assert.Multiple(() =>
        {
            Assert.That(row.Succeeded, Is.True);
            Assert.That(row.Template, Is.EqualTo("password-reset"));
            Assert.That(row.ProviderMessageId, Is.EqualTo("provider-id-123"));
            Assert.That(row.FailureReason, Is.Null);
        });
    }

    [Test]
    public async Task LogAsync_WritesAnAuditRow_ForAFailedSend()
    {
        await using var db = CreateContext();
        var sut = new EmailAuditLogger(db, NullLogger<EmailAuditLogger>.Instance);

        var message = new EmailMessage
        {
            ToAddress = "unreachable@example.com",
            Subject = "Reset your password",
            HtmlBody = "<p>hi</p>",
            TextBody = "hi",
            Template = "password-reset"
        };

        await sut.LogAsync(message, EmailSendResult.Permanent("550 no such mailbox"));

        var row = await db.SentEmails.SingleAsync(s => s.Recipient == "unreachable@example.com");
        Assert.Multiple(() =>
        {
            Assert.That(row.Succeeded, Is.False);
            Assert.That(row.ProviderMessageId, Is.Null);
            Assert.That(row.FailureReason, Is.EqualTo("550 no such mailbox"));
        });
    }
}
