using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Nimbus.Application.Helpers;
using Nimbus.Contracts.DTOs.Features.Auth;
using Nimbus.Contracts.DTOs.Features.Auth.Register;
namespace Nimbus.Api.Tests.Integration;

/// <summary>Issue #130: the account lifecycle sends the right template, and password reset leaks nothing.</summary>
public class AccountEmailTests
{
    private const string Password = "Sup3rSecure!Pass";
    private NimbusApiFactory _factory = null!;
    private RecordingEmailQueue Mail => _factory.Services.GetRequiredService<RecordingEmailQueue>();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new NimbusApiFactory();
        await _factory.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Register_Approve_Reject_SendTheMatchingTemplate()
    {
        using var client = _factory.CreateClient();
        var adminToken = await LoginAsync(client, _factory.AdminEmail, _factory.AdminPassword);

        var approved = await RegisterAsync(client);
        Assert.That(Mail.To(approved.Email).Select(m => m.Template), Is.EquivalentTo(new[] { EmailTemplates.EmailConfirmation, EmailTemplates.AccountPending }));
        Assert.That((await PostAdminAsync(client, adminToken, approved.Id, "approve")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(Mail.To(approved.Email).Select(m => m.Template), Does.Contain(EmailTemplates.AccountApproved));

        var rejected = await RegisterAsync(client);
        Assert.That((await PostAdminAsync(client, adminToken, rejected.Id, "reject")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var rejectedMail = Mail.To(rejected.Email).Select(m => m.Template).ToList();
        Assert.That(rejectedMail, Does.Contain(EmailTemplates.AccountRejected));
        Assert.That(rejectedMail, Does.Not.Contain(EmailTemplates.AccountApproved));
    }

    [Test]
    public async Task ForgotPassword_IsIndistinguishable_ForKnownAndUnknownAddresses()
    {
        using var client = _factory.CreateClient();
        var user = await _factory.CreateUserAsync();
        var unknown = $"nobody-{Guid.NewGuid():N}@example.test";

        var known = await client.PostAsJsonAsync("/api/authentication/forgot-password", new ForgotPasswordRequestDto(user.Email));
        var missing = await client.PostAsJsonAsync("/api/authentication/forgot-password", new ForgotPasswordRequestDto(unknown));

        Assert.That(known.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        Assert.That(missing.StatusCode, Is.EqualTo(known.StatusCode));
        Assert.That(await missing.Content.ReadAsStringAsync(), Is.EqualTo(await known.Content.ReadAsStringAsync()));
        Assert.That(Mail.To(user.Email).Select(m => m.Template), Does.Contain(EmailTemplates.PasswordReset));
        Assert.That(Mail.To(unknown), Is.Empty);
    }

    [Test]
    public async Task ForgotPassword_TakesComparableTime_ForKnownAndUnknownAddresses()
    {
        using var client = _factory.CreateClient();
        var user = await _factory.CreateUserAsync();

        var known = await TimeAsync(client, user.Email);
        var missing = await TimeAsync(client, $"nobody-{Guid.NewGuid():N}@example.test");

        Assert.That(known, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(450)));
        Assert.That(missing, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(450)));
    }

    [Test]
    public async Task ResetLink_PointsAtAngularRoute_AndTokenIsSingleUse()
    {
        using var client = _factory.CreateClient();
        var user = await _factory.CreateUserAsync();
        await client.PostAsJsonAsync("/api/authentication/forgot-password", new ForgotPasswordRequestDto(user.Email));

        var mail = Mail.To(user.Email).Last(m => m.Template == EmailTemplates.PasswordReset);
        var link = Regex.Match(mail.TextBody, @"https?://\S+").Value;
        Assert.That(link, Does.StartWith("http://localhost/reset-password?"));
        Assert.That(link, Does.Not.Contain("/api/"));

        var query = System.Web.HttpUtility.ParseQueryString(new Uri(link).Query);
        var token = query["token"]!;
        const string newPassword = "N3wPassw0rd!Value";

        var first = await client.PostAsJsonAsync("/api/authentication/reset-password", new ResetPasswordRequestDto(user.Email, token, newPassword));
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(await LoginAsync(client, user.Email, newPassword), Is.Not.Empty);

        var replay = await client.PostAsJsonAsync("/api/authentication/reset-password", new ResetPasswordRequestDto(user.Email, token, "An0therPassw0rd!"));
        Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "a reset token must work exactly once");
    }

    [Test]
    public async Task ForgotPassword_AlsoMailsRejectedUsers()
    {
        using var client = _factory.CreateClient();
        var user = await _factory.CreateUserAsync(approved: false, rejected: true);
        await client.PostAsJsonAsync("/api/authentication/forgot-password", new ForgotPasswordRequestDto(user.Email));
        Assert.That(Mail.To(user.Email).Select(m => m.Template), Does.Contain(EmailTemplates.PasswordReset));
    }

    [Test]
    public async Task Emails_AreDutchHtmlWithPlainTextAlternative()
    {
        using var client = _factory.CreateClient();
        var user = await _factory.CreateUserAsync();
        await client.PostAsJsonAsync("/api/authentication/forgot-password", new ForgotPasswordRequestDto(user.Email));
        var all = Mail.To(user.Email);
        var mail = all[^1];
        Assert.That(mail.HtmlBody, Does.Contain("<!DOCTYPE html>").And.Contain("Nieuw wachtwoord kiezen"));
        Assert.That(mail.TextBody, Does.Contain("Hallo").And.Not.Contain("<"));
    }

    [Test]
    public async Task ResetPassword_RejectsGarbageToken()
    {
        using var client = _factory.CreateClient();
        var user = await _factory.CreateUserAsync();
        var res = await client.PostAsJsonAsync("/api/authentication/reset-password", new ResetPasswordRequestDto(user.Email, "not-a-token", "N3wPassw0rd!Value"));
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    private static async Task<TimeSpan> TimeAsync(HttpClient client, string email)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await client.PostAsJsonAsync("/api/authentication/forgot-password", new ForgotPasswordRequestDto(email));
        return sw.Elapsed;
    }

    private async Task<(Guid Id, string Email)> RegisterAsync(HttpClient client)
    {
        var email = $"new-{Guid.NewGuid():N}@example.test";
        var res = await client.PostAsJsonAsync("/api/authentication/register", new RegisterRequestDto(email, Password, "Test", "User"));
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Nimbus.Infrastructure.Identity.ApplicationUser>>();
        return (Guid.Parse((await users.FindByEmailAsync(email))!.Id), email);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/authentication/login", new LoginRequestDto(email, password));
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await res.Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken;
    }

    private static Task<HttpResponseMessage> PostAdminAsync(HttpClient client, string token, Guid id, string action)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{id}/{action}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(req);
    }
}
