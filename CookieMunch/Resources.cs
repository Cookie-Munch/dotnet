using System.Text.Json.Nodes;

namespace CookieMunch;

/// <summary>Base for resource groups; holds the shared client transport.</summary>
public abstract class ResourceBase
{
    private protected readonly CookieMunchClient Client;
    private protected ResourceBase(CookieMunchClient client) => Client = client;
}

/// <summary>Sites: registration, config, cookies, scans, A/B, snippet, verification, brand, v2 flow.</summary>
public sealed class SitesResource : ResourceBase
{
    internal SitesResource(CookieMunchClient client) : base(client) { }

    /// <summary>List sites for the key's org (GET /v1/sites).</summary>
    public Task<List<Site>> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<Site>>(HttpMethod.Get, "/v1/sites", null, ct);

    /// <summary>Create a site; cbid is auto-generated when omitted (POST /v1/sites).</summary>
    public Task<Site> CreateAsync(SiteCreate input, CancellationToken ct = default) =>
        Client.SendAsync<Site>(HttpMethod.Post, "/v1/sites", input, ct);

    /// <summary>Get a site (GET /v1/sites/:cbid).</summary>
    public Task<Site> GetAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<Site>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}", null, ct);

    /// <summary>Delete a site (DELETE /v1/sites/:cbid).</summary>
    public Task DeleteAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}", null, ct);

    /// <summary>Get a site's config (GET /v1/sites/:cbid/config).</summary>
    public Task<JsonObject> GetConfigAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/config", null, ct);

    /// <summary>Upsert a site's config (PUT /v1/sites/:cbid/config). Partial configs are merged server-side.</summary>
    public Task<JsonObject> PutConfigAsync(string cbid, JsonObject config, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/config", config, ct);

    /// <summary>
    /// Change part of a site's config (PATCH /v1/sites/:cbid/config). Omitted fields keep
    /// their stored value; PutConfigAsync replaces the whole document.
    /// </summary>
    public Task<JsonObject> PatchConfigAsync(string cbid, JsonObject config, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(new HttpMethod("PATCH"), $"/v1/sites/{CookieMunchClient.Enc(cbid)}/config", config, ct);

    /// <summary>Latest categorized cookie declaration (GET /v1/sites/:cbid/cookies).</summary>
    public async Task<List<SiteCookie>> CookiesAsync(string cbid, CancellationToken ct = default)
    {
        // The endpoint returns a CookieDeclaration { updatedAt, cookies }; unwrap the array.
        var decl = await Client.SendAsync<CookieDeclaration>(
            HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies", null, ct).ConfigureAwait(false);
        return decl.Cookies;
    }

    /// <summary>Kick off an async cookie crawl (POST /v1/sites/:cbid/scan).</summary>
    public Task<ScanResult> ScanAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<ScanResult>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/scan", null, ct);

    /// <summary>Current cookie-scan status (GET /v1/sites/:cbid/scan).</summary>
    public Task<ScanResult> ScanStatusAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<ScanResult>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/scan", null, ct);

    /// <summary>A/B experiment results for the site (GET /v1/sites/:cbid/ab).</summary>
    public Task<List<AbResult>> AbAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<List<AbResult>>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/ab", null, ct);

    /// <summary>Get the install snippet for a site (GET /v1/sites/:cbid/snippet).</summary>
    public Task<InstallSnippet> SnippetAsync(string cbid, SnippetOptions? options = null, CancellationToken ct = default)
    {
        var q = CookieMunchClient.BuildQuery(("blockingmode", options?.BlockingMode), ("culture", options?.Culture));
        return Client.SendAsync<InstallSnippet>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/snippet{q}", null, ct);
    }

    /// <summary>Verify the site's domain (POST /v1/sites/:cbid/verify). <paramref name="method"/> is dns | meta | file.</summary>
    public Task<VerifyResult> VerifyAsync(string cbid, string method, CancellationToken ct = default) =>
        Client.SendAsync<VerifyResult>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/verify", new { method }, ct);

    /// <summary>Suggest theme tokens by extracting the site's brand ("Match my site") (POST /v1/sites/:cbid/brand).</summary>
    public Task<BrandExtractionResult> BrandAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<BrandExtractionResult>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/brand", new { }, ct);

    /// <summary>Read a site's v2 banner flow (views, categories) plus any lint issues (GET /v1/sites/:cbid/flow).</summary>
    public Task<SiteFlow> GetFlowAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<SiteFlow>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/flow", null, ct);

    /// <summary>Apply an ordered batch of structured edit ops (POST /v1/sites/:cbid/flow/ops). Check <see cref="FlowOpResponse.Ok"/>.</summary>
    public Task<FlowOpResponse> EditFlowAsync(string cbid, IEnumerable<JsonObject> operations, CancellationToken ct = default) =>
        Client.SendAsync<FlowOpResponse>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/flow/ops",
            new { operations = operations.ToList() }, ct);

    /// <summary>Wholesale-replace the flow with a full v2 config (PUT /v1/sites/:cbid/flow). Check <see cref="FlowOpResponse.Ok"/>.</summary>
    public Task<FlowOpResponse> SetFlowAsync(string cbid, JsonObject config, CancellationToken ct = default) =>
        Client.SendAsync<FlowOpResponse>(HttpMethod.Put, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/flow", config, ct);

    /// <summary>
    /// Read a cookie declaration exported from another CMP and translate its categories into
    /// ours (POST /v1/sites/:cbid/import). Nothing is applied — their vocabulary is not ours,
    /// so the result comes back for review.
    /// </summary>
    public Task<JsonObject> ImportDeclarationAsync(string cbid, string data, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/import", new { data }, ct);

    /// <summary>
    /// Pages where the embed could not load its banner renderer (GET /v1/sites/:cbid/blocked).
    /// The host page's Content Security Policy or Trusted Types policy refused it, so nobody
    /// there can be asked for consent. An empty list is the healthy answer.
    /// </summary>
    public Task<JsonObject> BlockedAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/blocked", null, ct);

    /// <summary>Which banner design the site uses (GET /v1/sites/:cbid/banner). <c>bannerId</c> is null when none is assigned.</summary>
    public Task<JsonObject> BannerAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/banner", null, ct);

    /// <summary>The site's privacy and cookie policy, as Markdown (GET /v1/sites/:cbid/policy).</summary>
    public Task<string> PolicyAsync(string cbid, PolicyOptions? options = null, CancellationToken ct = default) =>
        Client.SendRawAsync(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/policy" + CookieMunchClient.BuildQuery(
            ("contactEmail", options?.ContactEmail),
            ("effectiveDate", options?.EffectiveDate),
            ("jurisdictions", options?.Jurisdictions is null ? null : string.Join(",", options.Jurisdictions))), null, ct);

    /// <summary>Add or remove the separate personalised-ads choice on the site's banner (POST /v1/sites/:cbid/elements/ad-personalization).</summary>
    public Task<JsonObject> SetAdPersonalizationAsync(string cbid, AdPersonalizationInput input, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/elements/ad-personalization", input, ct);

    /// <summary>Which trackers fired after opt-out in a captured session, and what personal data left the page (POST /v1/sites/:cbid/sentry).</summary>
    public Task<JsonObject> AnalyzeSessionAsync(string cbid, SessionAnalysisInput input, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/sentry", input, ct);

    /// <summary>Exactly what to publish to prove control of the domain, per method (GET /v1/sites/:cbid/verify/challenge).</summary>
    public Task<JsonObject> VerifyChallengeAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/verify/challenge", null, ct);

    /// <summary>Create up to 100 sites (POST /v1/sites/bulk). Partial success: each result reports <c>ok</c> or its own error.</summary>
    public Task<JsonObject> CreateBulkAsync(List<BulkSite> sites, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/sites/bulk", new { sites }, ct);
}

/// <summary>Internal wrapper for the CookieDeclaration response.</summary>
internal sealed record CookieDeclaration
{
    [System.Text.Json.Serialization.JsonPropertyName("updatedAt")] public long UpdatedAt { get; init; }
    [System.Text.Json.Serialization.JsonPropertyName("cookies")] public List<SiteCookie> Cookies { get; init; } = new();
}

/// <summary>Consent stats, log, CSV export, signed receipts, subject erase/export, and the public ingest helper.</summary>
public sealed class ConsentResource : ResourceBase
{
    internal ConsentResource(CookieMunchClient client) : base(client) { }

    /// <summary>
    /// Verify the consent log's tamper-evident hash chain (GET /v1/sites/:cbid/consent/verify).
    /// Each record carries the hash of the one before it, so an edited, reordered or removed
    /// record answers false.
    /// </summary>
    public async Task<bool> VerifyAsync(string cbid, CancellationToken ct = default)
    {
        var res = await Client.SendAsync<JsonObject>(
            HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/consent/verify", null, ct).ConfigureAwait(false);
        return res? ["valid"]?.GetValue<bool>() ?? false;
    }

    /// <summary>Aggregated per-day consent stats (GET /v1/sites/:cbid/consent/stats).</summary>
    public Task<List<ConsentDay>> StatsAsync(string cbid, RangeQuery? query = null, CancellationToken ct = default)
    {
        var q = CookieMunchClient.BuildQuery(("from", query?.From), ("to", query?.To));
        return Client.SendAsync<List<ConsentDay>>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/consent/stats{q}", null, ct);
    }

    /// <summary>Recent anonymised consent records (GET /v1/sites/:cbid/consent/log).</summary>
    public Task<List<ConsentLogRow>> LogAsync(string cbid, LogQuery? query = null, CancellationToken ct = default)
    {
        var q = CookieMunchClient.BuildQuery(("from", query?.From), ("to", query?.To), ("limit", query?.Limit));
        return Client.SendAsync<List<ConsentLogRow>>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/consent/log{q}", null, ct);
    }

    /// <summary>CSV audit export (GET /v1/sites/:cbid/consent/export). Returns the raw CSV text.</summary>
    public Task<string> ExportAsync(string cbid, RangeQuery? query = null, CancellationToken ct = default)
    {
        var q = CookieMunchClient.BuildQuery(("from", query?.From), ("to", query?.To));
        return Client.SendRawAsync(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/consent/export{q}", null, ct);
    }

    /// <summary>Signed ISO-27560 consent receipt as JSON (GET /v1/sites/:cbid/receipt/:stamp).</summary>
    public Task<JsonNode> ReceiptAsync(string cbid, string stamp, CancellationToken ct = default) =>
        Client.SendAsync<JsonNode>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/receipt/{CookieMunchClient.Enc(stamp)}", null, ct);

    /// <summary>Crypto-erase a subject's consent records by their receipt stamp. Irreversible (POST /v1/sites/:cbid/erase-consent).</summary>
    public Task<EraseResult> EraseSubjectAsync(string cbid, string stamp, CancellationToken ct = default) =>
        Client.SendAsync<EraseResult>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/erase-consent", new { stamp }, ct);

    /// <summary>Export a subject's consent records by their receipt stamp (GET /v1/sites/:cbid/subject-export).</summary>
    public Task<SubjectExport> ExportSubjectAsync(string cbid, string stamp, CancellationToken ct = default) =>
        Client.SendAsync<SubjectExport>(HttpMethod.Get,
            $"/v1/sites/{CookieMunchClient.Enc(cbid)}/subject-export?stamp={CookieMunchClient.Enc(stamp)}", null, ct);

    /// <summary>
    /// Log a consent decision to the public, unauthenticated ingest endpoint (POST /api/v1/consent).
    /// Records are anonymised and hash-chained server-side. Ideal for desktop apps (WPF/WinUI/MAUI)
    /// that render their own consent UI. Returns on the 204 acknowledgement.
    /// </summary>
    public Task RecordAsync(ConsentRecordInput input, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Post, "/api/v1/consent", input, ct);
}

/// <summary>Data-subject access requests.</summary>
public sealed class DsarResource : ResourceBase
{
    internal DsarResource(CookieMunchClient client) : base(client) { }

    /// <summary>List DSARs (GET /v1/dsar).</summary>
    public Task<List<DsarRequest>> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<DsarRequest>>(HttpMethod.Get, "/v1/dsar", null, ct);

    /// <summary>Create a DSAR (POST /v1/dsar).</summary>
    public Task<DsarResponse> CreateAsync(DsarCreate input, CancellationToken ct = default) =>
        Client.SendAsync<DsarResponse>(HttpMethod.Post, "/v1/dsar", input, ct);

    /// <summary>Advance a DSAR to a new status (POST /v1/dsar/:id/advance). Use the <see cref="DsarStatus"/> constants.</summary>
    public Task<DsarResponse> AdvanceAsync(string id, string toStatus, CancellationToken ct = default) =>
        Client.SendAsync<DsarResponse>(HttpMethod.Post, $"/v1/dsar/{CookieMunchClient.Enc(id)}/advance", new { toStatus }, ct);

    /// <summary>The subject-facing response notice for a request, as plain text (GET /v1/dsar/:id/response).</summary>
    public Task<string> ResponseAsync(string id, CancellationToken ct = default) =>
        Client.SendRawAsync(HttpMethod.Get, $"/v1/dsar/{CookieMunchClient.Enc(id)}/response", null, ct);

    /// <summary>
    /// Erase a subject's consent records on one site, for a deletion request (POST /v1/dsar/:id/erase).
    /// The server refuses unless the request is a deletion past identity verification, and notes the
    /// erasure on it. Irreversible. Needs dsar:write and consent:write. Check
    /// <see cref="DsarEraseResult.Warning"/>: when set, no KEK is configured and nothing was erased.
    /// </summary>
    public Task<DsarEraseResult> EraseAsync(string id, string cbid, string stamp, CancellationToken ct = default) =>
        Client.SendAsync<DsarEraseResult>(HttpMethod.Post, $"/v1/dsar/{CookieMunchClient.Enc(id)}/erase", new { cbid, stamp }, ct);

    /// <summary>
    /// A subject's consent records on one site, for an access or portability request past identity
    /// verification (POST /v1/dsar/:id/export). The export is noted on the request. Needs dsar:write
    /// and consent:read.
    /// </summary>
    public Task<DsarExportResult> ExportAsync(string id, string cbid, string stamp, CancellationToken ct = default) =>
        Client.SendAsync<DsarExportResult>(HttpMethod.Post, $"/v1/dsar/{CookieMunchClient.Enc(id)}/export", new { cbid, stamp }, ct);
}

/// <summary>Third-party vendors + risk scoring.</summary>
public sealed class VendorsResource : ResourceBase
{
    internal VendorsResource(CookieMunchClient client) : base(client) { }

    /// <summary>List vendors with risk scores (GET /v1/vendors).</summary>
    public Task<List<ScoredVendor>> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<ScoredVendor>>(HttpMethod.Get, "/v1/vendors", null, ct);

    /// <summary>Create a vendor (POST /v1/vendors).</summary>
    public Task<VendorCreateResult> CreateAsync(VendorInput input, CancellationToken ct = default) =>
        Client.SendAsync<VendorCreateResult>(HttpMethod.Post, "/v1/vendors", input, ct);
}

/// <summary>Records of Processing Activities.</summary>
public sealed class RopaResource : ResourceBase
{
    internal RopaResource(CookieMunchClient client) : base(client) { }

    /// <summary>List RoPA entries (GET /v1/ropa).</summary>
    public Task<List<RopaEntry>> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<RopaEntry>>(HttpMethod.Get, "/v1/ropa", null, ct);

    /// <summary>Create a RoPA entry (POST /v1/ropa).</summary>
    public Task<RopaCreateResult> CreateAsync(RopaInput input, CancellationToken ct = default) =>
        Client.SendAsync<RopaCreateResult>(HttpMethod.Post, "/v1/ropa", input, ct);

    /// <summary>The org's RoPA (GDPR Art. 30), as CSV (GET /v1/ropa/export.csv).</summary>
    public Task<string> ExportCsvAsync(CancellationToken ct = default) =>
        Client.SendRawAsync(HttpMethod.Get, "/v1/ropa/export.csv", null, ct);
}

/// <summary>Reusable, org-level banner themes.</summary>
public sealed class BrandKitsResource : ResourceBase
{
    internal BrandKitsResource(CookieMunchClient client) : base(client) { }

    /// <summary>List brand kits (GET /v1/brand-kits).</summary>
    public Task<List<BrandKit>> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<BrandKit>>(HttpMethod.Get, "/v1/brand-kits", null, ct);

    /// <summary>Create a brand kit (POST /v1/brand-kits).</summary>
    public Task<BrandKitCreateResult> CreateAsync(BrandKitCreate input, CancellationToken ct = default) =>
        Client.SendAsync<BrandKitCreateResult>(HttpMethod.Post, "/v1/brand-kits", input, ct);

    /// <summary>Delete a brand kit (DELETE /v1/brand-kits/:id).</summary>
    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/brand-kits/{CookieMunchClient.Enc(id)}", null, ct);
}

/// <summary>Preference-center records.</summary>
public sealed class PreferencesResource : ResourceBase
{
    internal PreferencesResource(CookieMunchClient client) : base(client) { }

    /// <summary>List the org's preference-center records (GET /v1/preferences).</summary>
    public Task<List<PreferenceItem>> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<PreferenceItem>>(HttpMethod.Get, "/v1/preferences", null, ct);

    /// <summary>Save a subject's purposes (POST /v1/preferences).</summary>
    public Task<JsonNode> SaveAsync(string subjectId, IReadOnlyDictionary<string, bool> purposes, CancellationToken ct = default) =>
        Client.SendAsync<JsonNode>(HttpMethod.Post, "/v1/preferences", new { subjectId, purposes }, ct);

    /// <summary>
    /// One subject's record (GET /v1/preferences/:subjectId). A subject with nothing saved comes
    /// back with empty purposes. Needs consent:read.
    /// </summary>
    public Task<JsonNode> GetAsync(string subjectId, CancellationToken ct = default) =>
        Client.SendAsync<JsonNode>(HttpMethod.Get, $"/v1/preferences/{CookieMunchClient.Enc(subjectId)}", null, ct);
}

/// <summary>Org members.</summary>
public sealed class MembersResource : ResourceBase
{
    internal MembersResource(CookieMunchClient client) : base(client) { }

    /// <summary>List the org's members (GET /v1/members).</summary>
    public Task<List<Member>> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<Member>>(HttpMethod.Get, "/v1/members", null, ct);

    /// <summary>Invite a member (POST /v1/members).</summary>
    public Task<MemberResult> InviteAsync(string email, string role, CancellationToken ct = default) =>
        Client.SendAsync<MemberResult>(HttpMethod.Post, "/v1/members", new { email, role }, ct);

    /// <summary>Change a member's role (PATCH /v1/members/:userId).</summary>
    public Task<MemberResult> SetRoleAsync(string userId, string role, CancellationToken ct = default) =>
        Client.SendAsync<MemberResult>(HttpMethod.Patch, $"/v1/members/{CookieMunchClient.Enc(userId)}", new { role }, ct);

    /// <summary>Remove a member (DELETE /v1/members/:userId).</summary>
    public Task RemoveAsync(string userId, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/members/{CookieMunchClient.Enc(userId)}", null, ct);
}

/// <summary>API-key management.</summary>
public sealed class KeysResource : ResourceBase
{
    internal KeysResource(CookieMunchClient client) : base(client) { }

    /// <summary>List API-key prefixes for the org (GET /v1/keys). Never returns the secret.</summary>
    public Task<List<ApiKey>> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<ApiKey>>(HttpMethod.Get, "/v1/keys", null, ct);

    /// <summary>Issue a new API key (POST /v1/keys). The <see cref="ApiKey.Key"/> is returned ONCE.</summary>
    public Task<ApiKey> IssueAsync(string? name = null, CancellationToken ct = default) =>
        Client.SendAsync<ApiKey>(HttpMethod.Post, "/v1/keys", name is null ? new { } : new { name }, ct);

    /// <summary>
    /// Issue a least-privilege key (POST /v1/keys): <paramref name="input"/> may carry scopes,
    /// cbids (a property lock — the key then works only on those sites and on no org-wide
    /// endpoint) and an expiry. The <see cref="ApiKey.Key"/> is returned ONCE.
    /// </summary>
    public Task<ApiKey> IssueAsync(ApiKeyIssueInput input, CancellationToken ct = default) =>
        Client.SendAsync<ApiKey>(HttpMethod.Post, "/v1/keys", input, ct);

    /// <summary>Revoke a key by its prefix (DELETE /v1/keys/:prefix). Immediate.</summary>
    public Task RevokeAsync(string prefix, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/keys/{CookieMunchClient.Enc(prefix)}", null, ct);

    /// <summary>
    /// Rotate a key (POST /v1/keys/:prefix/roll). The new <see cref="ApiKey.Key"/> is returned
    /// ONCE and keeps the old key's name, scopes, property lock and expiry; the old secret stops
    /// working immediately — including this client's, if it is the one being rolled.
    /// </summary>
    public Task<ApiKey> RollAsync(string prefix, CancellationToken ct = default) =>
        Client.SendAsync<ApiKey>(HttpMethod.Post, $"/v1/keys/{CookieMunchClient.Enc(prefix)}/roll", null, ct);

    /// <summary>
    /// Rename a key, or replace its scopes or property lock (PATCH /v1/keys/:prefix). Only the
    /// fields set on <paramref name="patch"/> are sent; the secret is unchanged.
    /// </summary>
    public Task<JsonObject> UpdateAsync(string prefix, ApiKeyUpdate patch, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Patch, $"/v1/keys/{CookieMunchClient.Enc(prefix)}", patch, ct);
}

/// <summary>Webhook subscriptions.</summary>
public sealed class WebhooksResource : ResourceBase
{
    internal WebhooksResource(CookieMunchClient client) : base(client) { }

    /// <summary>List webhook subscriptions (GET /v1/webhooks). Secrets are omitted.</summary>
    public Task<List<WebhookSubscription>> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<WebhookSubscription>>(HttpMethod.Get, "/v1/webhooks", null, ct);

    /// <summary>Create a webhook subscription (POST /v1/webhooks). The signing secret is returned ONCE.</summary>
    public Task<WebhookSubscription> CreateAsync(WebhookCreate input, CancellationToken ct = default) =>
        Client.SendAsync<WebhookSubscription>(HttpMethod.Post, "/v1/webhooks", input, ct);

    /// <summary>Delete a webhook subscription (DELETE /v1/webhooks/:id).</summary>
    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/webhooks/{CookieMunchClient.Enc(id)}", null, ct);

    /// <summary>
    /// Change or pause a subscription (PATCH /v1/webhooks/:id). <paramref name="patch"/> is sent
    /// as-is: <c>["active"] = false</c> pauses it, and <c>["cbid"] = null</c> widens it to every
    /// property in the org — a <see cref="JsonObject"/> keeps that null, where a record would drop it.
    /// </summary>
    public Task<JsonObject> UpdateAsync(string id, JsonObject patch, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Patch, $"/v1/webhooks/{CookieMunchClient.Enc(id)}", patch, ct);

    /// <summary>
    /// Rotate a subscription's signing secret (POST /v1/webhooks/:id/roll). The new secret is
    /// returned once; deliveries are signed with it from now on, so update the receiving endpoint.
    /// </summary>
    public async Task<string> RollSecretAsync(string id, CancellationToken ct = default)
    {
        var body = await Client.SendAsync<JsonObject>(
            HttpMethod.Post, $"/v1/webhooks/{CookieMunchClient.Enc(id)}/roll", null, ct).ConfigureAwait(false);
        return body["secret"]?.GetValue<string>() ?? "";
    }

    /// <summary>Send a signed test event now and report what the endpoint answered (POST /v1/webhooks/:id/test).</summary>
    public Task<WebhookTestResult> TestAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<WebhookTestResult>(HttpMethod.Post, $"/v1/webhooks/{CookieMunchClient.Enc(id)}/test", null, ct);

    /// <summary>
    /// Deliveries that failed every retry, newest first (GET /v1/webhooks/dead-letters).
    /// A property-locked key sees only its own properties'.
    /// </summary>
    public async Task<List<WebhookDeadLetter>> DeadLettersAsync(CancellationToken ct = default)
    {
        var body = await Client.SendAsync<DeadLetterList>(
            HttpMethod.Get, "/v1/webhooks/dead-letters", null, ct).ConfigureAwait(false);
        return body.DeadLetters;
    }

    /// <summary>Deliver a dead-lettered event again, to the subscription as it is now (POST /v1/webhooks/dead-letters/:id/replay).</summary>
    public Task<JsonObject> ReplayDeadLetterAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/webhooks/dead-letters/{CookieMunchClient.Enc(id)}/replay", null, ct);

    private sealed record DeadLetterList
    {
        [System.Text.Json.Serialization.JsonPropertyName("deadLetters")]
        public List<WebhookDeadLetter> DeadLetters { get; init; } = new();
    }
}

/// <summary>Reusable account-level banner designs.</summary>
public sealed class BannersResource : ResourceBase
{
    internal BannersResource(CookieMunchClient client) : base(client) { }

    /// <summary>List the org's banner designs (GET /v1/banners).</summary>
    public Task<List<BannerSummary>> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<BannerSummary>>(HttpMethod.Get, "/v1/banners", null, ct);

    /// <summary>Create a banner design (POST /v1/banners).</summary>
    public Task<BannerRecord> CreateAsync(BannerCreate input, CancellationToken ct = default) =>
        Client.SendAsync<BannerRecord>(HttpMethod.Post, "/v1/banners", input, ct);

    /// <summary>Get a banner design (GET /v1/banners/:id).</summary>
    public Task<BannerRecord> GetAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<BannerRecord>(HttpMethod.Get, $"/v1/banners/{CookieMunchClient.Enc(id)}", null, ct);

    /// <summary>Update a banner design's name and/or json (PUT /v1/banners/:id).</summary>
    public Task<BannerRecord> UpdateAsync(string id, BannerUpdate patch, CancellationToken ct = default) =>
        Client.SendAsync<BannerRecord>(HttpMethod.Put, $"/v1/banners/{CookieMunchClient.Enc(id)}", patch, ct);

    /// <summary>Delete a banner design (DELETE /v1/banners/:id). Fails while still assigned to any site.</summary>
    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/banners/{CookieMunchClient.Enc(id)}", null, ct);

    /// <summary>List the site cbids this design is assigned to (GET /v1/banners/:id/assignments).</summary>
    public Task<BannerAssignments> AssignmentsAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<BannerAssignments>(HttpMethod.Get, $"/v1/banners/{CookieMunchClient.Enc(id)}/assignments", null, ct);

    /// <summary>Set the sites this design is assigned to (PUT /v1/banners/:id/assignments).</summary>
    public Task<BannerAssignments> SetAssignmentsAsync(string id, IEnumerable<string> cbids, CancellationToken ct = default) =>
        Client.SendAsync<BannerAssignments>(HttpMethod.Put, $"/v1/banners/{CookieMunchClient.Enc(id)}/assignments",
            new { cbids = cbids.ToList() }, ct);

    /// <summary>Compile the design into every assigned site's config (POST /v1/banners/:id/publish).</summary>
    public Task<BannerPublishResult> PublishAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<BannerPublishResult>(HttpMethod.Post, $"/v1/banners/{CookieMunchClient.Enc(id)}/publish", null, ct);
}

/// <summary>
/// The key's own organisation (/v1/org). Requires an unscoped key that is not property-locked.
/// Deleting an organisation is deliberately not part of the API: it stays a signed-in owner's act.
/// </summary>
public sealed class OrgResource : ResourceBase
{
    internal OrgResource(CookieMunchClient client) : base(client) { }

    /// <summary>The organisation: id, name, plan and logo (GET /v1/org).</summary>
    public Task<Org> GetAsync(CancellationToken ct = default) =>
        Client.SendAsync<Org>(HttpMethod.Get, "/v1/org", null, ct);

    /// <summary>
    /// Rename the organisation and/or set its logo (PATCH /v1/org). <paramref name="patch"/> is sent
    /// as-is: <c>["logoUrl"] = null</c> REMOVES the logo, while leaving the key out leaves the logo
    /// alone — a <see cref="JsonObject"/> keeps that null, where a record would drop it and silently
    /// turn "take it down" into "leave it".
    /// </summary>
    public Task<Org> UpdateAsync(JsonObject patch, CancellationToken ct = default) =>
        Client.SendAsync<Org>(HttpMethod.Patch, "/v1/org", patch, ct);
}

/// <summary>Images used by banners (/v1/assets).</summary>
public sealed class AssetsResource : ResourceBase
{
    internal AssetsResource(CookieMunchClient client) : base(client) { }

    /// <summary>
    /// Store an image and get its public URL (POST /v1/assets) — for a banner's logo or
    /// <see cref="OrgResource.UpdateAsync"/>. PNG, JPEG, WebP, GIF or SVG, up to 1,000,000 bytes;
    /// the bytes are base64-encoded here. Needs sites:write.
    /// </summary>
    public async Task<string> UploadAsync(byte[] data, string contentType, CancellationToken ct = default)
    {
        var body = await Client.SendAsync<JsonObject>(
            HttpMethod.Post, "/v1/assets", new { data = Convert.ToBase64String(data), contentType }, ct).ConfigureAwait(false);
        return body["url"]?.GetValue<string>() ?? "";
    }

    /// <summary>
    /// Delete a stored image (DELETE /v1/assets/{fileName}). Pass the URL
    /// <see cref="UploadAsync"/> returned, or just its file name. Only this organisation's
    /// images are reachable: the folder comes from the API key, not from the name sent.
    /// </summary>
    public Task DeleteAsync(string urlOrFileName, CancellationToken ct = default)
    {
        var name = urlOrFileName[(urlOrFileName.LastIndexOf('/') + 1)..];
        return Client.SendAsync(HttpMethod.Delete, $"/v1/assets/{CookieMunchClient.Enc(name)}", null, ct);
    }
}
