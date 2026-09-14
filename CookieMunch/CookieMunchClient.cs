using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CookieMunch;

/// <summary>
/// A typed client for the Cookie Munch Developer API (the <c>/v1</c> surface) plus the
/// public consent-log ingest endpoint. Construct it with an API key; the org is derived
/// server-side from the key, so callers never pass an orgId.
/// <code>
/// using var client = new CookieMunchClient("fck_live_…");
/// var sites = await client.Sites.ListAsync();
/// var receipt = await client.Consent.ReceiptAsync("cbid", "stamp");
/// </code>
/// The instance is thread-safe and should be reused (it owns a single <see cref="HttpClient"/>).
/// </summary>
public sealed class CookieMunchClient : IDisposable
{
    /// <summary>Default API origin. The <c>/v1</c> prefix is appended per-request.</summary>
    public const string DefaultBaseUrl = "https://api.cookiemunch.net";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _baseUri;

    /// <summary>Identity / bootstrapping (GET /v1/me) plus resource groups.</summary>
    public SitesResource Sites { get; }
    /// <summary>Consent stats, log, export, receipts, subject erase/export, and the public ingest helper.</summary>
    public ConsentResource Consent { get; }
    /// <summary>Data-subject access requests.</summary>
    public DsarResource Dsar { get; }
    /// <summary>Third-party vendors + risk scoring.</summary>
    public VendorsResource Vendors { get; }
    /// <summary>Records of Processing Activities.</summary>
    public RopaResource Ropa { get; }
    /// <summary>Reusable banner themes.</summary>
    public BrandKitsResource BrandKits { get; }
    /// <summary>Preference-center records.</summary>
    public PreferencesResource Preferences { get; }
    /// <summary>Org members.</summary>
    public MembersResource Members { get; }
    /// <summary>API-key management.</summary>
    public KeysResource Keys { get; }
    /// <summary>Webhook subscriptions.</summary>
    public WebhooksResource Webhooks { get; }
    /// <summary>Reusable account-level banner designs.</summary>
    public BannersResource Banners { get; }

    /// <summary>
    /// Create a client.
    /// </summary>
    /// <param name="apiKey">The API key (an <c>fck_…</c> token). Sent as both <c>Authorization: Bearer</c> and <c>X-API-Key</c>.</param>
    /// <param name="baseUrl">API origin. Defaults to <see cref="DefaultBaseUrl"/>.</param>
    /// <param name="httpClient">Optional caller-owned <see cref="HttpClient"/> (e.g. from IHttpClientFactory). When supplied it is not disposed by this client.</param>
    public CookieMunchClient(string apiKey, string? baseUrl = null, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("apiKey is required.", nameof(apiKey));

        var root = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
        _baseUri = new Uri(root + "/", UriKind.Absolute);

        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        // Some deployments accept the key via X-API-Key as well; send both harmlessly.
        if (!_http.DefaultRequestHeaders.Contains("X-API-Key"))
            _http.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        Sites = new SitesResource(this);
        Consent = new ConsentResource(this);
        Dsar = new DsarResource(this);
        Vendors = new VendorsResource(this);
        Ropa = new RopaResource(this);
        BrandKits = new BrandKitsResource(this);
        Preferences = new PreferencesResource(this);
        Members = new MembersResource(this);
        Keys = new KeysResource(this);
        Webhooks = new WebhooksResource(this);
        Banners = new BannersResource(this);
    }

    /// <summary>Identity / echo for SDK bootstrapping (GET /v1/me).</summary>
    public Task<Identity> MeAsync(CancellationToken ct = default) =>
        SendAsync<Identity>(HttpMethod.Get, "/v1/me", null, ct);

    /// <summary>Current resource usage for the org (GET /v1/usage).</summary>
    public Task<Usage> UsageAsync(CancellationToken ct = default) =>
        SendAsync<Usage>(HttpMethod.Get, "/v1/usage", null, ct);

    // ── transport ────────────────────────────────────────────────────────────

    internal async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var res = await SendCoreAsync(method, path, body, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(res, ct).ConfigureAwait(false);
        // 204 / empty body with a value-return call: surface as default (shouldn't happen for typed reads).
        if (res.StatusCode == HttpStatusCode.NoContent)
            return default!;
        var value = await res.Content.ReadFromJsonAsync<T>(Json, ct).ConfigureAwait(false);
        return value ?? throw new CookieMunchApiException((int)res.StatusCode, "", "empty response body");
    }

    internal async Task SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var res = await SendCoreAsync(method, path, body, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(res, ct).ConfigureAwait(false);
    }

    internal async Task<string> SendRawAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var res = await SendCoreAsync(method, path, body, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(res, ct).ConfigureAwait(false);
        return await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        // path always begins with '/'; resolve against the origin (baseUri ends with '/').
        var uri = new Uri(_baseUri, path.TrimStart('/'));
        var req = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            var payload = JsonSerializer.Serialize(body, body.GetType(), Json);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }
        return _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var body = res.Content is null ? "" : await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        string? error = null, code = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                    error = e.GetString();
                if (doc.RootElement.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                    code = c.GetString();
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body — keep the raw body only.
        }
        throw new CookieMunchApiException((int)res.StatusCode, body, error, code);
    }

    /// <summary>Percent-encode + join a set of query parameters, skipping null values.</summary>
    internal static string BuildQuery(params (string Key, object? Value)[] parameters)
    {
        var parts = new List<string>();
        foreach (var (key, value) in parameters)
        {
            if (value is null) continue;
            var s = value is bool b ? (b ? "true" : "false")
                : value is IFormattable f ? f.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                : value.ToString();
            if (s is null) continue;
            parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(s)}");
        }
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    internal static string Enc(string segment) => Uri.EscapeDataString(segment);

    /// <summary>Dispose the owned <see cref="HttpClient"/> (no-op if one was supplied by the caller).</summary>
    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
