using System.Net;
using System.Text.Json;
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
}
