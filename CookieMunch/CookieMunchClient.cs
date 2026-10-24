using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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
    /// <summary>The hosted rights page.</summary>
    public RightsPageResource RightsPage { get; }
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
    /// <summary>The identity surface.</summary>
    public IdentityResource Identity { get; }
    /// <summary>The vault surface.</summary>
    public VaultResource Vault { get; }
    /// <summary>The profile surface.</summary>
    public ProfileResource Profile { get; }
    /// <summary>The subscriptions surface.</summary>
    public SubscriptionsResource Subscriptions { get; }
    /// <summary>The assessments surface.</summary>
    public AssessmentsResource Assessments { get; }
    /// <summary>The discovery surface.</summary>
    public DiscoveryResource Discovery { get; }
    /// <summary>The ai surface.</summary>
    public AiResource Ai { get; }
    /// <summary>The fulfillment surface.</summary>
    public FulfillmentResource Fulfillment { get; }
    /// <summary>The regulatory surface.</summary>
    public RegulatoryResource Regulatory { get; }
    /// <summary>The reseller surface.</summary>
    public ResellerResource Reseller { get; }
    /// <summary>The subjects surface.</summary>
    public SubjectsResource Subjects { get; }
    /// <summary>Account-linked rights requests (single-use links to the hosted rights page).</summary>
    public RightsLinksResource RightsLinks { get; }
    /// <summary>A site's rights-page captcha: your own keys (recommended), or off.</summary>
    public RightsCaptchaResource RightsCaptcha { get; }
    /// <summary>A site's consent-sync signing secret (hosted consent sync).</summary>
    public ConsentSyncSecretResource ConsentSyncSecret { get; }
    /// <summary>The hosted preference centre: mint a token for a visitor your server has signed in.</summary>
    public PreferenceCenterResource PreferenceCenter { get; }
    /// <summary>A site's sign-in provider: a visitor signed in to your site is verified by their session, with no code mail.</summary>
    public RightsIdentityResource RightsIdentity { get; }
    /// <summary>A site's rights-request settings: graceDays (0–30; 7 for a new site, 0 until set for an existing one). Needs dsar:read / dsar:write.</summary>
    public DsarSettingsResource DsarSettings { get; }
    /// <summary>Cards for support, CRM and helpdesk sidebars. Needs dsar:read.</summary>
    public IntegrationsResource Integrations { get; }
    /// <summary>The key's own organisation: name and logo.</summary>
    public OrgResource Org { get; }
    /// <summary>Banner images.</summary>
    public AssetsResource Assets { get; }
    /// <summary>Tags on created resources (/v1/metadata/{type}/{id}).</summary>
    public MetadataResource Metadata { get; }
    /// <summary>Suite-standard partner links (/v1/partner_links).</summary>
    public PartnerLinksResource PartnerLinks { get; }

    /// <summary>
    /// <c>x25519:&lt;base64 public key&gt;</c>: once-shown secrets come back sealed (<c>sealed_secret</c>), never in plaintext.
    /// Sent as <c>X-Secret-Recipient</c> on writes.
    /// </summary>
    public string? SecretRecipient { get; set; }

    /// <summary>Extra headers on every request, e.g. <c>["Idempotency-Key"] = ref + ":" + run</c> on a client made for one create.</summary>
    public Dictionary<string, string> ExtraHeaders { get; } = new();

    /// <summary>
    /// Send <c>Prefer: strict-unknown-fields</c> on writes: a body field the API does not declare
    /// is refused (<c>400 unknown_field</c>) instead of ignored.
    /// </summary>
    public bool StrictUnknownFields { get; set; }

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
        // HttpClient sends no User-Agent by default, and the API's edge refuses requests
        // without a recognisable one (a bare 403 "error code: 1010"). Name ourselves unless the
        // caller's HttpClient already does.
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("cookiemunch-dotnet/0.1.0");

        Sites = new SitesResource(this);
        Consent = new ConsentResource(this);
        Dsar = new DsarResource(this);
        Vendors = new VendorsResource(this);
        Ropa = new RopaResource(this);
        BrandKits = new BrandKitsResource(this);
        RightsPage = new RightsPageResource(this);
        Preferences = new PreferencesResource(this);
        Members = new MembersResource(this);
        Keys = new KeysResource(this);
        Webhooks = new WebhooksResource(this);
        Banners = new BannersResource(this);
        Identity = new IdentityResource(this);
        Vault = new VaultResource(this);
        Profile = new ProfileResource(this);
        Subscriptions = new SubscriptionsResource(this);
        Assessments = new AssessmentsResource(this);
        Discovery = new DiscoveryResource(this);
        Ai = new AiResource(this);
        Fulfillment = new FulfillmentResource(this);
        Regulatory = new RegulatoryResource(this);
        Reseller = new ResellerResource(this);
        Subjects = new SubjectsResource(this);
        RightsLinks = new RightsLinksResource(this);
        RightsCaptcha = new RightsCaptchaResource(this);
        ConsentSyncSecret = new ConsentSyncSecretResource(this);
        PreferenceCenter = new PreferenceCenterResource(this);
        RightsIdentity = new RightsIdentityResource(this);
        DsarSettings = new DsarSettingsResource(this);
        Integrations = new IntegrationsResource(this);
        Org = new OrgResource(this);
        Assets = new AssetsResource(this);
        Metadata = new MetadataResource(this);
        PartnerLinks = new PartnerLinksResource(this);
    }

    /// <summary><c>?metadata[k]=v</c> tag filters for list endpoints.</summary>
    internal static string MetaQuery(IDictionary<string, string>? metadata) =>
        metadata is null || metadata.Count == 0 ? "" : "?" + string.Join("&", metadata.Select(kv => $"metadata%5B{Enc(kv.Key)}%5D={Enc(kv.Value)}"));

    /// <summary>
    /// <see cref="CreateOrgAsync(string, CancellationToken)"/> with options: <c>mintKey</c>, <c>keyScopes</c>
    /// (required for an <c>orgs:create</c> key), <c>keyExpiresInDays</c> (default 365), <c>keyOwner</c>,
    /// <c>externalRef</c>, <c>metadata</c>.
    /// </summary>
    public Task<JsonObject> CreateOrgAsync(string name, JsonObject options, CancellationToken ct = default)
    {
        var body = JsonNode.Parse(options.ToJsonString())!.AsObject();
        body["name"] = name;
        return SendAsync<JsonObject>(HttpMethod.Post, "/v1/orgs", body, ct);
    }

    /// <summary>Organisations with these tags (GET /v1/orgs?metadata[k]=v).</summary>
    public Task<JsonObject> ListOrgsTaggedAsync(IDictionary<string, string> metadata, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Get, "/v1/orgs" + MetaQuery(metadata), null, ct);

    /// <summary>
    /// Remove a SANDBOX organisation of this account (DELETE /v1/orgs/{id}?hard=1). Anything not created
    /// with <c>metadata.forgedeck_sandbox</c> is 403 <c>not_a_sandbox</c>.
    /// </summary>
    public Task<JsonObject> DeleteSandboxOrgAsync(string id, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Delete, $"/v1/orgs/{Enc(id)}?hard=1", null, ct);

    /// <summary>
    /// What <c>forgedeck env</c> writes for the product provisioned with this ref (GET /v1/provision/env):
    /// names, non-secret values, and references for secrets.
    /// </summary>
    public Task<JsonObject> ProvisionEnvAsync(string @ref, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Get, "/v1/provision/env?ref=" + Enc(@ref), null, ct);

    /// <summary><see cref="ProvisionAsync"/> resuming from a stable step id (a version 2 body).</summary>
    public Task<JsonObject> ProvisionFromAsync(JsonObject input, string fromStep, bool dryRun = false, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Post, "/v1/provision?fromStep=" + Enc(fromStep) + (dryRun ? "&dryRun=1" : ""), input, ct);

    /// <summary>Identity / echo for SDK bootstrapping (GET /v1/me).</summary>
    public Task<Identity> MeAsync(CancellationToken ct = default) =>
        SendAsync<Identity>(HttpMethod.Get, "/v1/me", null, ct);

    /// <summary>
    /// Every organisation on this key's account (GET /v1/orgs), each with its effective plan,
    /// planSource (org = its own subscription, account = covered by a higher plan another org
    /// on the account pays for) and subscribedPlan, plus orgLimit and upgradeUrl. Needs an
    /// unscoped key that is not locked to properties.
    /// </summary>
    public Task<JsonObject> ListOrgsAsync(CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Get, "/v1/orgs", null, ct);

    /// <summary>
    /// Create a sibling organisation owned by the same account (POST /v1/orgs), for starting a
    /// separate business of your own. Needs an unscoped key and counts against the account's plan
    /// org allowance (403 org_limit names the plan). Not reseller provisioning, which is for
    /// organisations you run on behalf of YOUR customers.
    /// </summary>
    public Task<JsonObject> CreateOrgAsync(string name, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Post, "/v1/orgs", new JsonObject { ["name"] = name }, ct);

    /// <summary>
    /// The languages the banner already has copy for (GET /v1/languages). Diff it against
    /// your visitors' locales to find the ones you still have to write.
    /// </summary>
    public Task<List<SupportedLanguage>> LanguagesAsync(CancellationToken ct = default) =>
        SendAsync<List<SupportedLanguage>>(HttpMethod.Get, "/v1/languages", null, ct);

    /// <summary>Current resource usage for the org (GET /v1/usage).</summary>
    public Task<Usage> UsageAsync(CancellationToken ct = default) =>
        SendAsync<Usage>(HttpMethod.Get, "/v1/usage", null, ct);

    /// <summary>
    /// What the org may use, from its plan (GET /v1/entitlements): every feature as
    /// <c>{key, enabled}</c> and every limit as <c>limits.&lt;name&gt;</c> with <c>limit</c> (null =
    /// unlimited) and <c>used</c> where metered. Any key may read it.
    /// </summary>
    public Task<JsonObject> EntitlementsAsync(CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Get, "/v1/entitlements", null, ct);

    /// <summary>
    /// Every link a site's privacy wiring depends on, one row each with <c>ok</c> (null = could not
    /// check) and a <c>fixHint</c> (GET /v1/integration-health). <paramref name="cbid"/> may be null
    /// when the org has one site.
    /// </summary>
    public Task<JsonObject> IntegrationHealthAsync(string? cbid = null, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Get, "/v1/integration-health" + (cbid is null ? "" : "?cbid=" + Uri.EscapeDataString(cbid)), null, ct);

    /// <summary>
    /// The site's embed manifest (GET /v1/embed?cbid=): the tenant variant of
    /// /.well-known/forgedeck-embed.json — every origin by CSP directive and every browser key the
    /// embed writes for this site's config. Public: no key is needed.
    /// </summary>
    public Task<JsonObject> EmbedManifestAsync(string cbid, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Get, "/v1/embed?cbid=" + Uri.EscapeDataString(cbid), null, ct);

    /// <summary>
    /// Wire a product's privacy in one call — site, connected systems, Atlas, Forumulate (POST
    /// /v1/provision). Idempotent; <paramref name="dryRun"/> writes nothing anywhere. Secrets in
    /// <paramref name="input"/> are used for this request only; none is returned.
    /// </summary>
    public Task<JsonObject> ProvisionAsync(JsonObject input, bool dryRun = false, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Post, "/v1/provision" + (dryRun ? "?dryRun=1" : ""), input, ct);

    /// <summary>
    /// The org's audit log, newest first (GET /v1/audit): administrative changes made in the
    /// dashboard or through the API, and who made them — API actions as <c>apikey:&lt;prefix&gt;</c>.
    /// <paramref name="limit"/> is 1–500 and defaults to 200 server-side. Requires an unscoped key
    /// that is not property-locked.
    /// </summary>
    public async Task<List<AuditEntry>> AuditAsync(int? limit = null, CancellationToken ct = default)
    {
        var body = await SendAsync<AuditLogPage>(
            HttpMethod.Get, "/v1/audit" + BuildQuery(("limit", limit)), null, ct).ConfigureAwait(false);
        return body.Entries;
    }

    /// <summary>
    /// One actor's audit rows (GET /v1/audit?actor=). <c>"self"</c> is the calling key's own rows,
    /// the one form a scoped or property-locked key may read.
    /// </summary>
    public async Task<List<AuditEntry>> AuditAsync(int? limit, string actor, CancellationToken ct = default)
    {
        var body = await SendAsync<AuditLogPage>(
            HttpMethod.Get, "/v1/audit" + BuildQuery(("limit", limit), ("actor", actor)), null, ct).ConfigureAwait(false);
        return body.Entries;
    }

    /// <summary>Where the audit log is streamed as CloudEvents, never the secret (GET /v1/audit/sink).</summary>
    public Task<JsonObject> GetAuditSinkAsync(CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Get, "/v1/audit/sink", null, ct);

    /// <summary>Stream the audit log to an HTTPS endpoint (PUT /v1/audit/sink {type: "webhook", url, secret?}). A generated secret is returned once.</summary>
    public Task<JsonObject> SetAuditSinkAsync(JsonObject input, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Put, "/v1/audit/sink", input, ct);

    /// <summary>Stop the audit-log export (DELETE /v1/audit/sink).</summary>
    public Task RemoveAuditSinkAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, "/v1/audit/sink", null, ct);

    /// <summary>The checks the org chose not to meet (GET /v1/integration-health/waivers).</summary>
    public Task<JsonObject> HealthWaiversAsync(string? cbid = null, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Get, "/v1/integration-health/waivers" + BuildQuery(("cbid", cbid)), null, ct);

    /// <summary>Waive a check (PUT /v1/integration-health/waivers {cbid?, check, subject?, reason, until?}). Audited.</summary>
    public Task<JsonObject> SetHealthWaiverAsync(JsonObject input, CancellationToken ct = default) =>
        SendAsync<JsonObject>(HttpMethod.Put, "/v1/integration-health/waivers", input, ct);

    /// <summary>Remove a waiver (DELETE /v1/integration-health/waivers). cbid and subject may be null.</summary>
    public Task RemoveHealthWaiverAsync(string check, string? cbid = null, string? subject = null, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, "/v1/integration-health/waivers" + BuildQuery(("check", check), ("cbid", cbid), ("subject", subject)), null, ct);

    private sealed record AuditLogPage
    {
        [System.Text.Json.Serialization.JsonPropertyName("entries")]
        public List<AuditEntry> Entries { get; init; } = new();
    }

    // ── transport ────────────────────────────────────────────────────────────

    internal async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct, IReadOnlyDictionary<string, string>? headers = null)
    {
        using var res = await SendCoreAsync(method, path, body, ct, headers).ConfigureAwait(false);
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

    private Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? body, CancellationToken ct, IReadOnlyDictionary<string, string>? headers = null)
    {
        // path always begins with '/'; resolve against the origin (baseUri ends with '/').
        var uri = new Uri(_baseUri, path.TrimStart('/'));
        var req = new HttpRequestMessage(method, uri);
        if (headers is not null)
            foreach (var (k, v) in headers) req.Headers.TryAddWithoutValidation(k, v);
        if (StrictUnknownFields && method != HttpMethod.Get)
            req.Headers.TryAddWithoutValidation("Prefer", "strict-unknown-fields");
        if (SecretRecipient is not null && method != HttpMethod.Get)
            req.Headers.TryAddWithoutValidation("X-Secret-Recipient", SecretRecipient);
        foreach (var kv in ExtraHeaders)
            req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
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

    /// <summary>The HttpClient's timeout: a long-poll asks the server to hold less than this.</summary>
    internal TimeSpan HttpTimeout => _http.Timeout;

    /// <summary>Dispose the owned <see cref="HttpClient"/> (no-op if one was supplied by the caller).</summary>
    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
