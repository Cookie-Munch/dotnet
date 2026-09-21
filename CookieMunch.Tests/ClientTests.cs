using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace CookieMunch.Tests;

public class ClientTests
{
    private const string ApiKey = "fck_test_abc123";

    private static CookieMunchClient MakeClient(MockHttpMessageHandler handler)
        => new(ApiKey, "https://api.example.test", new HttpClient(handler));

    [Fact]
    public async Task Sends_bearer_and_api_key_headers()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"orgId":"org_1","plan":"pro","keyPrefix":"fck_test"}""");
        using var client = MakeClient(handler);

        var me = await client.MeAsync();

        Assert.Equal("org_1", me.OrgId);
        Assert.Equal("pro", me.Plan);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization?.Scheme);
        Assert.Equal(ApiKey, handler.LastRequest.Headers.Authorization?.Parameter);
        Assert.True(handler.LastRequest.Headers.TryGetValues("X-API-Key", out var vals));
        Assert.Equal(ApiKey, Assert.Single(vals!));
        // /v1 prefix is appended to the developer-API path.
        Assert.Equal("https://api.example.test/v1/me", handler.LastRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task Get_sites_parses_list_and_uses_get()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK,
            """
            [
              {"cbid":"cb_1","orgId":"org_1","domain":"a.com","verified":true,"verifyToken":"t1"},
              {"cbid":"cb_2","orgId":"org_1","domain":"b.com","verified":false,"verifyToken":"t2"}
            ]
            """);
        using var client = MakeClient(handler);

        var sites = await client.Sites.ListAsync();

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("https://api.example.test/v1/sites", handler.LastRequest.RequestUri?.ToString());
        Assert.Equal(2, sites.Count);
        Assert.Equal("cb_1", sites[0].Cbid);
        Assert.True(sites[0].Verified);
        Assert.False(sites[1].Verified);
    }

    [Fact]
    public async Task Consent_record_posts_to_public_ingest_with_body()
    {
        var handler = new MockHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = MakeClient(handler);

        await client.Consent.RecordAsync(new ConsentRecordInput
        {
            Cbid = "cb_1",
            Stamp = "stamp-xyz",
            Choices = new ConsentChoices { Preferences = true, Statistics = false, Marketing = true },
            Method = "explicit",
            Ver = 3,
            Utc = 1_700_000_000_000,
            Url = "app://settings/privacy",
        });

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        // The public ingest endpoint is NOT under the /v1 developer prefix.
        Assert.Equal("https://api.example.test/api/v1/consent", handler.LastRequest.RequestUri?.ToString());

        using var doc = JsonDocument.Parse(handler.LastBody);
        var root = doc.RootElement;
        Assert.Equal("cb_1", root.GetProperty("cbid").GetString());
        Assert.Equal("stamp-xyz", root.GetProperty("stamp").GetString());
        Assert.Equal("explicit", root.GetProperty("method").GetString());
        Assert.Equal(3, root.GetProperty("ver").GetInt32());
        Assert.Equal(1_700_000_000_000, root.GetProperty("utc").GetInt64());
        Assert.True(root.GetProperty("choices").GetProperty("preferences").GetBoolean());
        Assert.False(root.GetProperty("choices").GetProperty("statistics").GetBoolean());
        Assert.True(root.GetProperty("choices").GetProperty("marketing").GetBoolean());
        // Null optionals are omitted from the wire payload.
        Assert.False(root.TryGetProperty("tcString", out _));
        // subjectId is a null optional here, so it must be omitted too.
        Assert.False(root.TryGetProperty("subjectId", out _));
    }

    [Fact]
    public async Task Consent_record_includes_subject_id_when_set()
    {
        var handler = new MockHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = MakeClient(handler);

        await client.Consent.RecordAsync(new ConsentRecordInput
        {
            Cbid = "cb_1",
            Stamp = "stamp-xyz",
            Choices = new ConsentChoices { Preferences = true, Statistics = false, Marketing = true },
            Method = "explicit",
            Ver = 1,
            Utc = 1_700_000_000_000,
            Url = "app://settings/privacy",
            SubjectId = "user-42",
        });

        using var doc = JsonDocument.Parse(handler.LastBody);
        Assert.Equal("user-42", doc.RootElement.GetProperty("subjectId").GetString());
    }

    [Fact]
    public async Task Non_2xx_throws_with_status_body_and_error()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.NotFound,
            """{"error":"site not found","code":"no_site"}""");
        using var client = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<CookieMunchApiException>(() => client.Sites.GetAsync("missing"));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal("site not found", ex.Error);
        Assert.Equal("site not found", ex.Message);
        Assert.Equal("no_site", ex.Code);
        Assert.Contains("no_site", ex.Body);
    }

    [Fact]
    public async Task Non_json_error_body_is_preserved()
    {
        var handler = new MockHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream boom"),
        });
        using var client = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<CookieMunchApiException>(() => client.Sites.ListAsync());

        Assert.Equal(502, ex.StatusCode);
        Assert.Null(ex.Error);
        Assert.Equal("upstream boom", ex.Body);
    }

    [Fact]
    public async Task Query_parameters_are_encoded()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
        using var client = MakeClient(handler);

        await client.Consent.LogAsync("cb_1", new LogQuery { From = 100, To = 200, Limit = 50 });

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Equal("https://api.example.test/v1/sites/cb_1/consent/log?from=100&to=200&limit=50", uri);
    }
    // ── the operations that used to exist only in the dashboard ──────────────

    /// <summary>
    /// Removing a logo and leaving it alone are different requests. The serializer is
    /// configured WhenWritingNull, so a record would drop the null and silently turn
    /// "take it down" into "leave it" — the patch is a JsonObject for exactly that reason.
    /// This asserts on the bytes that went out, not on the object handed in.
    /// </summary>
    [Fact]
    public async Task Org_update_sends_logo_null_only_when_clearing()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"id":"org_1","name":"Acme","plan":"pro","logoUrl":null}""");
        using var client = MakeClient(handler);

        await client.Org.UpdateAsync(new JsonObject { ["logoUrl"] = null });
        Assert.Contains("\"logoUrl\":null", handler.LastBody);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest!.Method);
        Assert.Equal("https://api.example.test/v1/org", handler.LastRequest.RequestUri?.ToString());

        await client.Org.UpdateAsync(new JsonObject { ["name"] = "Acme Ltd" });
        Assert.DoesNotContain("logoUrl", handler.LastBody);
    }

    [Fact]
    public async Task Org_get_reads_the_organisation()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"id":"org_1","name":"Acme","plan":"pro","logoUrl":"https://cdn/x.png"}""");
        using var client = MakeClient(handler);

        var org = await client.Org.GetAsync();

        Assert.Equal("Acme", org.Name);
        Assert.Equal("https://cdn/x.png", org.LogoUrl);
        Assert.Equal("https://api.example.test/v1/org", handler.LastRequest!.RequestUri?.ToString());
    }

    [Fact]
    public async Task Audit_unwraps_entries_and_passes_the_limit()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"entries":[{"id":"a1","orgId":"org_1","actorUserId":"apikey:fck_test","action":"org.renamed","at":1}]}""");
        using var client = MakeClient(handler);

        var entries = await client.AuditAsync(50);

        Assert.Equal("apikey:fck_test", Assert.Single(entries).ActorUserId);
        Assert.Equal("https://api.example.test/v1/audit?limit=50", handler.LastRequest!.RequestUri?.ToString());

        await client.AuditAsync();
        Assert.Equal("https://api.example.test/v1/audit", handler.LastRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task Asset_upload_base64_encodes_the_image()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK, """{"url":"https://cdn/x.png"}""");
        using var client = MakeClient(handler);

        var url = await client.Assets.UploadAsync(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, "image/png");

        Assert.Equal("https://cdn/x.png", url);
        Assert.Contains("\"data\":\"iVBORw==\"", handler.LastBody);
        Assert.Contains("\"contentType\":\"image/png\"", handler.LastBody);
    }

    [Fact]
    public async Task Key_roll_and_update_hit_the_right_paths()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK, """{"prefix":"fck_new","key":"fck_new_secret"}""");
        using var client = MakeClient(handler);

        var rolled = await client.Keys.RollAsync("fck_ab12");
        Assert.Equal("fck_new_secret", rolled.Key);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.example.test/v1/keys/fck_ab12/roll", handler.LastRequest.RequestUri?.ToString());

        await client.Keys.UpdateAsync("fck_ab12", new ApiKeyUpdate { Scopes = new[] { "sites:read" } });
        Assert.Equal(HttpMethod.Patch, handler.LastRequest.Method);
        Assert.Equal("https://api.example.test/v1/keys/fck_ab12", handler.LastRequest.RequestUri?.ToString());
        // Unset fields stay out of the body: omitted means unchanged.
        Assert.DoesNotContain("name", handler.LastBody);
        Assert.Contains("\"scopes\":[\"sites:read\"]", handler.LastBody);
    }

    [Fact]
    public async Task Webhook_roll_test_and_dead_letters()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK, """{"secret":"whsec_new"}""");
        using var client = MakeClient(handler);
        Assert.Equal("whsec_new", await client.Webhooks.RollSecretAsync("w1"));
        Assert.Equal("https://api.example.test/v1/webhooks/w1/roll", handler.LastRequest!.RequestUri?.ToString());

        var testHandler = MockHttpMessageHandler.Json(HttpStatusCode.OK, """{"ok":true,"status":200}""");
        using var testClient = MakeClient(testHandler);
        var result = await testClient.Webhooks.TestAsync("w1");
        Assert.True(result.Ok);
        Assert.Equal(200, result.Status);
        Assert.Equal("https://api.example.test/v1/webhooks/w1/test", testHandler.LastRequest!.RequestUri?.ToString());

        var dlqHandler = MockHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"deadLetters":[{"id":"dlq_1","orgId":"org_1","subscriptionId":"w1","url":"https://h","eventType":"consent.recorded","cbid":null,"attempts":5,"failedAt":7}]}""");
        using var dlqClient = MakeClient(dlqHandler);
        var dead = await dlqClient.Webhooks.DeadLettersAsync();
        Assert.Equal("dlq_1", Assert.Single(dead).Id);
        Assert.Equal("https://api.example.test/v1/webhooks/dead-letters", dlqHandler.LastRequest!.RequestUri?.ToString());

        await dlqClient.Webhooks.ReplayDeadLetterAsync("dlq_1");
        Assert.Equal("https://api.example.test/v1/webhooks/dead-letters/dlq_1/replay", dlqHandler.LastRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task Dsar_erase_surfaces_the_dormant_crypto_warning()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"erased":0,"encryptionEnabled":false,"warning":"Crypto-erase is not configured (no KEK)","request":{"id":"d1"}}""");
        using var client = MakeClient(handler);

        var res = await client.Dsar.EraseAsync("d1", "cb_shop", "st-1");

        Assert.False(res.EncryptionEnabled);
        Assert.Contains("no KEK", res.Warning);
        Assert.Equal("https://api.example.test/v1/dsar/d1/erase", handler.LastRequest!.RequestUri?.ToString());
        Assert.Contains("\"cbid\":\"cb_shop\"", handler.LastBody);
        Assert.Contains("\"stamp\":\"st-1\"", handler.LastBody);
    }

    [Fact]
    public async Task Dsar_export_and_preference_get_hit_the_right_paths()
    {
        var handler = MockHttpMessageHandler.Json(HttpStatusCode.OK, """{"records":[],"count":0,"request":{"id":"d1"}}""");
        using var client = MakeClient(handler);

        var res = await client.Dsar.ExportAsync("d1", "cb_shop", "st-1");
        Assert.Equal(0, res.Count);
        Assert.Equal("https://api.example.test/v1/dsar/d1/export", handler.LastRequest!.RequestUri?.ToString());

        await client.Preferences.GetAsync("jane@example.com");
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("https://api.example.test/v1/preferences/jane%40example.com", handler.LastRequest.RequestUri?.ToString());
    }
}
