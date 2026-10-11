using System.Net.Http.Json;
namespace Nimbus.Api.Tests.Integration;

/// <summary>
///     A browser-like cookie jar for one signed-in (or anonymous) user. One <see cref="HttpClient" /> is shared
///     across many users in these tests, so each user carries their own cookies — the auth cookie, the
///     antiforgery cookie and the readable XSRF-TOKEN cookie — and <see cref="Apply" /> attaches them (plus the
///     <c>X-XSRF-TOKEN</c> header) the way the Angular app does.
/// </summary>
public sealed class TestSession
{
    public const string AuthCookie = "nimbus.auth";
    public const string XsrfCookie = "XSRF-TOKEN";
    public const string XsrfHeader = "X-XSRF-TOKEN";

    private readonly Dictionary<string, string> _cookies = new();

    public string? Xsrf => _cookies.GetValueOrDefault(XsrfCookie);
    public bool IsSignedIn => _cookies.ContainsKey(AuthCookie);
    public string? Cookie(string name) => _cookies.GetValueOrDefault(name);

    /// <summary>Starts an anonymous session the way the SPA does: GET the antiforgery endpoint.</summary>
    public static async Task<TestSession> StartAsync(HttpClient client)
    {
        var session = new TestSession();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/antiforgery");
        session.Absorb(await client.SendAsync(req));
        return session;
    }

    /// <summary>Records Set-Cookie headers (a cleared cookie removes the entry).</summary>
    public void Absorb(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return;
        }

        foreach (var line in values)
        {
            var pair = line.Split(';')[0];
            var eq = pair.IndexOf('=');
            var name = pair[..eq];
            var value = pair[(eq + 1)..];
            if (value.Length == 0 || line.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
            {
                _cookies.Remove(name);
            }
            else
            {
                _cookies[name] = value;
            }
        }
    }

    public HttpRequestMessage Apply(HttpRequestMessage request)
    {
        if (_cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}")));
        }
        if (Xsrf is not null)
        {
            request.Headers.Add(XsrfHeader, Xsrf);
        }
        return request;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, object? body = null)
    {
        using var req = Apply(new HttpRequestMessage(method, url));
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }
        var response = await client.SendAsync(req);
        Absorb(response);
        return response;
    }
}
