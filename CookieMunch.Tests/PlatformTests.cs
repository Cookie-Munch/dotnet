using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace CookieMunch.Tests;

/// <summary>The shapes that matter on the platform surface: what is sent, and what comes back.</summary>
public class PlatformTests
{
    private readonly List<(string Method, string Url, string Body)> _calls = new();

    private CookieMunchClient Client(HttpStatusCode status = HttpStatusCode.OK, string body = "{}", string contentType = "application/json")
    {
        _calls.Clear();
        var handler = new MockHttpMessageHandler((req, sent) =>
        {
            _calls.Add((req.Method.Method, req.RequestUri!.ToString(), sent));
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };
        });
        return new CookieMunchClient("fck_test", "https://api.example.test", new HttpClient(handler));
    }

    [Fact]
    public async Task DeprovisionSuspendsByDefaultAndPurgesOnlyWhenAsked()
    {
        using var c = Client(HttpStatusCode.NoContent, "");
        await c.Reseller.DeprovisionAsync("c1");
        await c.Reseller.DeprovisionAsync("c1", purge: true);
        Assert.Equal("https://api.example.test/v1/reseller/customers/c1", _calls[0].Url);
        Assert.Equal("https://api.example.test/v1/reseller/customers/c1?purge=true", _calls[1].Url);
    }

    /// <summary>The client ignores nulls when writing records; a JsonObject's null has to survive.</summary>
    [Fact]
    public async Task ClearingDsarRoutingAndWideningAWebhookSendExplicitNulls()
    {
        using var c = Client();
        await c.Reseller.UpdateAsync("c1", new JsonObject { ["dsarRouting"] = null });
        await c.Webhooks.UpdateAsync("w1", new JsonObject { ["cbid"] = null, ["active"] = false });
        using var reseller = JsonDocument.Parse(_calls[0].Body);
        using var webhook = JsonDocument.Parse(_calls[1].Body);
        Assert.Equal(JsonValueKind.Null, reseller.RootElement.GetProperty("dsarRouting").ValueKind);
        Assert.Equal(JsonValueKind.Null, webhook.RootElement.GetProperty("cbid").ValueKind);
        Assert.False(webhook.RootElement.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task IssueSendsLeastPrivilegeFieldsAndOmitsTheRest()
    {
        using var c = Client(body: "{\"key\":\"k\",\"prefix\":\"p\"}");
        await c.Keys.IssueAsync(new ApiKeyIssueInput("agency", new() { "consent:read" }, new() { "cb_shop" }, 30));
        await c.Keys.IssueAsync(new ApiKeyIssueInput("ci"));
        Assert.Equal("{\"name\":\"agency\",\"scopes\":[\"consent:read\"],\"cbids\":[\"cb_shop\"],\"expiresInDays\":30}", _calls[0].Body);
        Assert.Equal("{\"name\":\"ci\"}", _calls[1].Body);
    }

    [Fact]
    public async Task PolicyIsMarkdownWithOptionsInTheQuery()
    {
        using var c = Client(body: "# Privacy policy", contentType: "text/markdown");
        var md = await c.Sites.PolicyAsync("s1", new PolicyOptions("dpo@x.com", Jurisdictions: new() { "gdpr", "ccpa" }));
        Assert.Equal("# Privacy policy", md);
        Assert.Equal("https://api.example.test/v1/sites/s1/policy?contactEmail=dpo%40x.com&jurisdictions=gdpr%2Cccpa", _calls[0].Url);
    }

    [Fact]
    public async Task RopaExportAndDsarNoticeAreText()
    {
        using (var c = Client(body: "a,b\n", contentType: "text/csv")) Assert.Equal("a,b\n", await c.Ropa.ExportCsvAsync());
        using (var c = Client(body: "Dear subject", contentType: "text/plain")) Assert.Equal("Dear subject", await c.Dsar.ResponseAsync("d1"));
    }

    [Fact]
    public async Task IdentifiersTravelInTheBodyNeverTheUrl()
    {
        using var c = Client();
        var ids = new List<Identifier> { new("email_sha256", "abc") };
        await c.Identity.ResolveAsync(ids);
        await c.Vault.CurrentAsync(ids);
        await c.Profile.ActivateAsync(ids, "marketing");
        foreach (var (method, url, body) in _calls)
        {
            Assert.Equal("POST", method);
            Assert.DoesNotContain("abc", url);
            Assert.Contains("\"identifiers\":[{\"space\":\"email_sha256\",\"value\":\"abc\"}]", body);
        }
    }
}
