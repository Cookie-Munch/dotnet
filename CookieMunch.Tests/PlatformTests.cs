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

    /// <summary>Numestri F-01: per-language purposes, and dryRun on every declaration write.</summary>
    [Fact]
    public async Task DeclaredCookiesCarryPurposesAndDryRun()
    {
        using var c = Client(body: "{\"dryRun\":true,\"declared\":[],\"diff\":{\"added\":[],\"removed\":[],\"changed\":[]}}");
        await c.Sites.DeclareCookieAsync("cb", "sid", "necessary", "Signed in.", purposes: new Dictionary<string, string> { ["es"] = "Sesión." });
        await c.Sites.DeclareCookieAsync("cb", "sid", "necessary", "Signed in.", dryRun: true);
        await c.Sites.DeclareCookiesAsync("cb", new[] { new JsonObject { ["name"] = "a", ["category"] = "necessary", ["purpose"] = "A" } }, replace: true, dryRun: true);
        var dry = await c.Sites.UndeclareCookieDryRunAsync("cb", "sid");
        Assert.Equal(new[]
        {
            "PUT https://api.example.test/v1/sites/cb/cookies/sid",
            "PUT https://api.example.test/v1/sites/cb/cookies/sid?dryRun=1",
            "PUT https://api.example.test/v1/sites/cb/cookies?dryRun=1",
            "DELETE https://api.example.test/v1/sites/cb/cookies/sid?dryRun=1",
        }, _calls.Select(x => $"{x.Method} {x.Url}").ToArray());
        using var first = JsonDocument.Parse(_calls[0].Body);
        Assert.Equal("Sesión.", first.RootElement.GetProperty("purposes").GetProperty("es").GetString());
        using var second = JsonDocument.Parse(_calls[1].Body);
        Assert.False(second.RootElement.TryGetProperty("purposes", out _));
        Assert.True(dry["dryRun"]!.GetValue<bool>());
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

    [Fact]
    public async Task RightsLinksCreateSendsTheSubjectAndOptions()
    {
        using var c = Client(HttpStatusCode.Created, "{\"url\":\"https://cookiemunch.net/r/s1?t=a.b\",\"expiresAt\":1}");
        var outp = await c.RightsLinks.CreateAsync("s1", "p@x.com", userId: "u-1", rights: new[] { "access" }, ttlSeconds: 600);
        await c.RightsLinks.CreateAsync("s1", "p@x.com");
        Assert.Equal("https://cookiemunch.net/r/s1?t=a.b", (string?)outp["url"]);
        Assert.Equal("POST", _calls[0].Method);
        Assert.Equal("https://api.example.test/v1/sites/s1/rights-links", _calls[0].Url);
        Assert.Equal("{\"subject\":{\"email\":\"p@x.com\",\"userId\":\"u-1\"},\"rights\":[\"access\"],\"ttlSeconds\":600}", _calls[0].Body);
        Assert.Equal("{\"subject\":{\"email\":\"p@x.com\"}}", _calls[1].Body);
    }

    [Fact]
    public async Task RightsPageDomains()
    {
        using var c = Client();
        await c.RightsPage.ListDomainsAsync("s1");
        await c.RightsPage.AddDomainAsync("s", "privacy.acme.com");
        await c.RightsPage.VerifyDomainAsync("s", "privacy.acme.com");
        await c.RightsPage.RemoveDomainAsync("s", "privacy.acme.com");
        const string b = "https://api.example.test/v1/sites";
        Assert.Equal(new[] { "GET", "POST", "POST", "DELETE" }, _calls.Select(x => x.Method).ToArray());
        Assert.Equal(b + "/s1/rights-page/domains", _calls[0].Url);
        Assert.Equal("{\"hostname\":\"privacy.acme.com\"}", _calls[1].Body);
        Assert.Equal(b + "/s/rights-page/domains/privacy.acme.com/verify", _calls[2].Url);
        Assert.Equal(b + "/s/rights-page/domains/privacy.acme.com", _calls[3].Url);
    }

    [Fact]
    public async Task ExecutorWebhookCheckDeliveriesAndDryRuns()
    {
        using var c = Client();
        await c.Fulfillment.TestExecutorWebhookAsync("ex_1", new Dictionary<string, string> { ["x-sig"] = "v1,abc" }, "{\"a\":1}");
        await c.Fulfillment.DryRunConnectExecutorAsync("crm", "https://crm.example", "sk_12345678", new JsonObject());
        await c.Fulfillment.DryRunUpdateExecutorAsync("ex_1", new JsonObject { ["auto"] = false });
        await c.Fulfillment.UpdateExecutorAsync("ex_1", new JsonObject { ["auto"] = false });
        Assert.Equal(new[]
        {
            "POST https://api.example.test/v1/dsar/executors/ex_1/webhook/test",
            "POST https://api.example.test/v1/dsar/executors?dryRun=1",
            "PATCH https://api.example.test/v1/dsar/executors/ex_1?dryRun=1",
            "PATCH https://api.example.test/v1/dsar/executors/ex_1",
        }, _calls.Select(x => $"{x.Method} {x.Url}").ToArray());
        using var sent = JsonDocument.Parse(_calls[0].Body);
        Assert.Equal("v1,abc", sent.RootElement.GetProperty("headers").GetProperty("x-sig").GetString());
        Assert.Equal("{\"a\":1}", sent.RootElement.GetProperty("body").GetString());

        using var list = Client(body: "[]");
        await list.Fulfillment.ExecutorWebhookDeliveriesAsync("ex_1");
        Assert.Equal("GET https://api.example.test/v1/dsar/executors/ex_1/webhook/deliveries", $"{_calls[0].Method} {_calls[0].Url}");
    }

    [Fact]
    public async Task RetireAndUnretire()
    {
        using var c = Client(body: "{\"dryRun\":true,\"retired\":[]}");
        await c.Sites.RetireCookieAsync("cb", "old", renamedTo: "new", note: "v2");
        await c.Sites.RetireCookieAsync("cb", "theme", dryRun: true);
        var dry = await c.Sites.UnretireCookieDryRunAsync("cb", "old");
        await c.Sites.UnretireCookieAsync("cb", "old");
        Assert.Equal(new[]
        {
            "POST https://api.example.test/v1/sites/cb/cookies/old/retire",
            "POST https://api.example.test/v1/sites/cb/cookies/theme/retire?dryRun=1",
            "DELETE https://api.example.test/v1/sites/cb/cookies/old/retire?dryRun=1",
            "DELETE https://api.example.test/v1/sites/cb/cookies/old/retire",
        }, _calls.Select(x => $"{x.Method} {x.Url}").ToArray());
        using var first = JsonDocument.Parse(_calls[0].Body);
        Assert.Equal("new", first.RootElement.GetProperty("renamedTo").GetString());
        Assert.Equal("v2", first.RootElement.GetProperty("note").GetString());
        Assert.Equal("{}", _calls[1].Body);
        Assert.True(dry["dryRun"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ScanWaitAndWaitForScan()
    {
        using var c = Client(body: "{\"status\":\"idle\",\"state\":\"done\",\"scanId\":\"s1\",\"cookieCount\":3,\"keptFromEarlier\":12,\"pagesVisited\":5,\"durationMs\":31000,\"lastScannedAt\":9}");
        await c.Sites.ScanAsync("cb", TimeSpan.FromSeconds(30));
        await c.Sites.ScanStatusAsync("cb", TimeSpan.FromMilliseconds(1500));
        var r = await c.Sites.WaitForScanAsync("cb", TimeSpan.FromMinutes(2));
        Assert.Equal("https://api.example.test/v1/sites/cb/scan?wait=30", _calls[0].Url);
        Assert.Equal("https://api.example.test/v1/sites/cb/scan?wait=1.5", _calls[1].Url);
        // HttpClient's default timeout is 100 s, so the server may hold the full 60.
        Assert.Equal("https://api.example.test/v1/sites/cb/scan?wait=60", _calls[2].Url);
        Assert.Equal(("done", "s1", 3, 12, 5, 31000L), (r.State, r.ScanId, r.CookieCount, r.KeptFromEarlier, r.PagesVisited, r.DurationMs));
    }
}
