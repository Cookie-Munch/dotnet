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

    /// <summary>Sites with these tags (GET /v1/sites?metadata[k]=v).</summary>
    public Task<List<Site>> ListTaggedAsync(IDictionary<string, string> metadata, CancellationToken ct = default) =>
        Client.SendAsync<List<Site>>(HttpMethod.Get, "/v1/sites" + CookieMunchClient.MetaQuery(metadata), null, ct);

    /// <summary>Create a site; cbid is auto-generated when omitted (POST /v1/sites).</summary>
    public Task<Site> CreateAsync(SiteCreate input, CancellationToken ct = default) =>
        Client.SendAsync<Site>(HttpMethod.Post, "/v1/sites", input, ct);

    /// <summary>Get a site (GET /v1/sites/:cbid).</summary>
    public Task<Site> GetAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<Site>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}", null, ct);

    /// <summary>
    /// Rename or move a site (PATCH /v1/sites/:cbid) with any of <c>domain</c>, <c>name</c> (a JSON
    /// null clears it) and <c>aliases</c> (an empty array clears them). The cbid and the consent ledger
    /// stay; a domain move clears verification. <paramref name="dryRun"/> answers with the diff and its
    /// effects, storing nothing.
    /// </summary>
    public Task<JsonObject> UpdateAsync(string cbid, JsonObject patch, bool dryRun = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(new HttpMethod("PATCH"), $"/v1/sites/{CookieMunchClient.Enc(cbid)}{(dryRun ? "?dryRun=1" : "")}", patch, ct);

    /// <summary>Delete a site (DELETE /v1/sites/:cbid).</summary>
    public Task DeleteAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}", null, ct);

    /// <summary>
    /// Purge a SANDBOX site (DELETE /v1/sites/:cbid?hard=1): its consent ledger, declarations and test
    /// rights requests too. Anything not created as a sandbox is 403 <c>not_a_sandbox</c>.
    /// </summary>
    public Task<JsonObject> DeleteSandboxAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}?hard=1", null, ct);

    /// <summary>Partner links and the partner catalogue (GET /v1/sites/:cbid/partners).</summary>
    public Task<JsonObject> ListPartnersAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/partners", null, ct);

    /// <summary>
    /// Grant a partner platform (<c>forumulate</c>, <c>atlas</c>) the site's consent events
    /// (POST /v1/sites/:cbid/partners). No secret is created; a repeat is a no-op.
    /// </summary>
    public Task<JsonObject> LinkPartnerAsync(string cbid, string partner, string externalId, string? label = null, bool dryRun = false, CancellationToken ct = default)
    {
        var body = new JsonObject { ["partner"] = partner, ["externalId"] = externalId };
        if (label is not null) body["label"] = label;
        return Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/partners{(dryRun ? "?dryRun=1" : "")}", body, ct);
    }

    /// <summary>Revoke a partner link; deliveries stop at once (DELETE /v1/sites/:cbid/partners/:linkId).</summary>
    public Task<JsonObject> UnlinkPartnerAsync(string cbid, string linkId, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/partners/{CookieMunchClient.Enc(linkId)}", null, ct);

    /// <summary>Get a site's config (GET /v1/sites/:cbid/config).</summary>
    public Task<JsonObject> GetConfigAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/config", null, ct);

    /// <summary>
    /// Replace a site's config (PUT /v1/sites/:cbid/config). A top-level field the config omits
    /// reverts to its default — unless <paramref name="keepUnspecified"/> (?keep=unspecified),
    /// which keeps its stored value. A top-level field sent as null reverts to its default.
    /// </summary>
    public Task<JsonObject> PutConfigAsync(string cbid, JsonObject config, bool keepUnspecified = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/config{(keepUnspecified ? "?keep=unspecified" : "")}", config, ct);

    /// <summary>
    /// Change part of a site's config (PATCH /v1/sites/:cbid/config) — a JSON Merge Patch
    /// (RFC 7386): omitted fields keep their stored value, and a field set to null is removed
    /// (it takes its default). PutConfigAsync replaces the whole document.
    /// </summary>
    public Task<JsonObject> PatchConfigAsync(string cbid, JsonObject config, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(new HttpMethod("PATCH"), $"/v1/sites/{CookieMunchClient.Enc(cbid)}/config", config, ct);

    /// <summary>
    /// Preview a config write without saving it (PATCH, or with <paramref name="replace"/> PUT,
    /// /v1/sites/:cbid/config?dryRun=1). Returns <c>config</c> (as it would be saved),
    /// <c>diff</c> (every leaf that would change) and any <c>warnings</c>.
    /// </summary>
    public Task<JsonObject> DryRunConfigAsync(string cbid, JsonObject config, bool replace = false, bool keepUnspecified = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(replace ? HttpMethod.Put : new HttpMethod("PATCH"), $"/v1/sites/{CookieMunchClient.Enc(cbid)}/config?dryRun=1{(replace && keepUnspecified ? "&keep=unspecified" : "")}", config, ct);

    /// <summary>
    /// Check a proposed config for consent-button parity without saving it
    /// (POST /v1/sites/:cbid/config/lint): is "Accept all" more prominent than "Reject all"
    /// anywhere (colour, size, placement, copy, custom CSS)? Returns <c>designWarnings</c>,
    /// <c>score</c> (0-100) and <c>suggestedReject</c>. It checks exactly what the write would
    /// store: merged as a PATCH would, or with <paramref name="replace"/> replaced as a PUT would. Needs only sites:read.
    /// </summary>
    public Task<JsonObject> LintConfigAsync(string cbid, JsonObject config, bool replace = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/config/lint{(replace ? "?replace=1" : "")}", config, ct);

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

    /// <summary>
    /// Start a cookie crawl and hold the request until it is done or failed, or until
    /// <paramref name="wait"/> (at most 60 s) runs out (POST /v1/sites/:cbid/scan?wait=).
    /// </summary>
    public Task<ScanResult> ScanAsync(string cbid, TimeSpan wait, CancellationToken ct = default) =>
        Client.SendAsync<ScanResult>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/scan{WaitQuery(wait)}", null, ct);

    /// <summary>The last scan: state, timings, cookie count, pages, error (GET /v1/sites/:cbid/scan).</summary>
    public Task<ScanResult> ScanStatusAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<ScanResult>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/scan", null, ct);

    /// <summary>The last scan, holding the request until a running scan settles or <paramref name="wait"/> (at most 60 s) runs out.</summary>
    public Task<ScanResult> ScanStatusAsync(string cbid, TimeSpan wait, CancellationToken ct = default) =>
        Client.SendAsync<ScanResult>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/scan{WaitQuery(wait)}", null, ct);

    /// <summary>
    /// Return once the site's scan is done or failed (read <c>State</c> and <c>Error</c>),
    /// long-polling the server; at <paramref name="timeout"/> it returns the still-running
    /// state. Each request asks the server to hold no longer than the HttpClient's own timeout allows.
    /// </summary>
    public async Task<ScanResult> WaitForScanAsync(string cbid, TimeSpan timeout, CancellationToken ct = default)
    {
        var until = DateTime.UtcNow + timeout;
        while (true)
        {
            var asked = until - DateTime.UtcNow;
            if (asked > TimeSpan.FromSeconds(60)) asked = TimeSpan.FromSeconds(60);
            var cap = Client.HttpTimeout - TimeSpan.FromSeconds(5);
            if (Client.HttpTimeout != System.Threading.Timeout.InfiniteTimeSpan && asked > cap) asked = cap;
            if (asked < TimeSpan.Zero) asked = TimeSpan.Zero;
            var started = DateTime.UtcNow;
            var r = await ScanStatusAsync(cbid, asked, ct).ConfigureAwait(false);
            if (r.Status != "scanning" || DateTime.UtcNow >= until) return r;
            // A server that answered sooner than asked is not hammered.
            var gap = until - DateTime.UtcNow < TimeSpan.FromSeconds(1) ? until - DateTime.UtcNow : TimeSpan.FromSeconds(1);
            var early = gap - (DateTime.UtcNow - started);
            if (early > TimeSpan.Zero) await Task.Delay(early, ct).ConfigureAwait(false);
        }
    }

    /// <summary><c>?wait=30</c>, <c>?wait=1.5</c>.</summary>
    private static string WaitQuery(TimeSpan wait) =>
        "?wait=" + Math.Round(wait.TotalSeconds, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);

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

    /// <summary>
    /// Mint a short-lived signed token to preview the banner as a visitor in <paramref name="region"/>
    /// speaking <paramref name="lang"/> sees it — headless browsers included, nothing recorded
    /// (POST /v1/sites/:cbid/preview-token). The result carries <c>token</c>, <c>expiresAt</c> and <c>previewUrl</c>.
    /// </summary>
    public Task<JsonObject> PreviewTokenAsync(string cbid, string? region = null, string? lang = null, string? url = null, int? ttlSeconds = null, CancellationToken ct = default)
    {
        var body = new JsonObject();
        if (region is not null) body["region"] = region;
        if (lang is not null) body["lang"] = lang;
        if (url is not null) body["url"] = url;
        if (ttlSeconds is not null) body["ttlSeconds"] = ttlSeconds;
        return Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/preview-token", body, ct);
    }

    /// <summary>
    /// The brand.json last imported for the site (GET /v1/sites/:cbid/brand): each role, the theme
    /// token it sets, its <c>source</c> (<c>brand.json</c> or <c>site</c>), <c>ignored</c> and <c>warnings</c>.
    /// </summary>
    public Task<JsonObject> GetBrandAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/brand", null, ct);

    /// <summary>
    /// Import a DTCG brand.json into the banner theme and the rights page (PUT /v1/sites/:cbid/brand).
    /// <paramref name="input"/> is <c>{"url": "https://…"}</c> or the document. Values the site set
    /// itself are kept (brand.json &lt; site banner theme &lt; per-banner variant).
    /// <paramref name="dryRun"/> saves nothing.
    /// </summary>
    public Task<JsonObject> SetBrandAsync(string cbid, JsonObject input, bool dryRun = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/brand{(dryRun ? "?dryRun=1" : "")}", input, ct);

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
    /// Import a declaration exported from another CMP AND declare it
    /// (POST /v1/sites/:cbid/import?apply=1). Every row whose category mapped to ours and that
    /// has a purpose is declared; the rest come back in <c>skipped</c>. Cookies the site already
    /// declared are kept unless <paramref name="overwrite"/>.
    /// </summary>
    public Task<JsonObject> ApplyImportAsync(string cbid, string data, bool overwrite = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post,
            $"/v1/sites/{CookieMunchClient.Enc(cbid)}/import?apply=1{(overwrite ? "&overwrite=1" : "")}", new { data }, ct);

    /// <summary>
    /// Declare what one cookie is (PUT /v1/sites/:cbid/cookies/:name). Overrides scanned,
    /// observed and database classification everywhere (API, preference modal,
    /// <c>data-cc="declaration"</c>). A name ending in <c>*</c> declares a prefix wildcard.
    /// <paramref name="category"/> is a standard category or one of the site's custom ids.
    /// <paramref name="purposes"/> is the purpose in other languages, keyed by BCP-47 tag
    /// (<c>es</c>, <c>pt-BR</c>); <paramref name="purpose"/> is the fallback.
    /// <paramref name="dryRun"/> stores nothing and answers with <c>declared</c> (the would-be
    /// list) and a <c>diff</c>.
    /// </summary>
    public Task<JsonObject> DeclareCookieAsync(string cbid, string name, string category, string purpose,
        string? provider = null, string? expiry = null, string? domain = null, bool? pattern = null,
        IReadOnlyDictionary<string, string>? purposes = null, bool dryRun = false, string? type = null, CancellationToken ct = default)
    {
        var body = new JsonObject { ["category"] = category, ["purpose"] = purpose };
        // localStorage, sessionStorage or indexedDB declares browser storage instead of a cookie.
        if (type is not null) body["type"] = type;
        if (provider is not null) body["provider"] = provider;
        if (expiry is not null) body["expiry"] = expiry;
        if (domain is not null) body["domain"] = domain;
        if (pattern is not null) body["pattern"] = pattern.Value;
        if (purposes is not null)
        {
            var map = new JsonObject();
            foreach (var (lang, text) in purposes) map[lang] = text;
            body["purposes"] = map;
        }
        return Client.SendAsync<JsonObject>(HttpMethod.Put,
            $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/{CookieMunchClient.Enc(name)}{(dryRun ? "?dryRun=1" : "")}", body, ct);
    }

    /// <summary>
    /// Declare many cookies at once (PUT /v1/sites/:cbid/cookies); each entry carries <c>name</c>,
    /// <c>category</c>, <c>purpose</c> and the optional fields. All-or-nothing;
    /// <paramref name="replace"/> also removes declarations the list does not name;
    /// <paramref name="dryRun"/> stores nothing and answers with what would be, plus a <c>diff</c>.
    /// </summary>
    public Task<JsonObject> DeclareCookiesAsync(string cbid, IEnumerable<JsonObject> cookies, bool replace = false, bool dryRun = false, CancellationToken ct = default)
    {
        var list = new JsonArray();
        foreach (var c in cookies) list.Add(c.DeepClone());
        var body = new JsonObject { ["cookies"] = list };
        if (replace) body["replace"] = true;
        return Client.SendAsync<JsonObject>(HttpMethod.Put, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies{(dryRun ? "?dryRun=1" : "")}", body, ct);
    }

    /// <summary>
    /// Retire a cookie the site renamed or stopped setting (POST /v1/sites/:cbid/cookies/:name/retire).
    /// It leaves the banner's cookie list, <c>data-cc="declaration"</c> and the public feed, and
    /// stays listed for the owner with <c>retired</c>. A name ending in <c>*</c> retires every
    /// cookie with that prefix. If a crawl or a visit sees it again afterwards it is listed
    /// again, flagged <c>retired_but_seen</c>. <paramref name="dryRun"/> stores nothing.
    /// </summary>
    public Task<JsonObject> RetireCookieAsync(string cbid, string name, string? renamedTo = null, string? note = null, bool dryRun = false, CancellationToken ct = default)
    {
        var body = new JsonObject();
        if (renamedTo is not null) body["renamedTo"] = renamedTo;
        if (note is not null) body["note"] = note;
        return Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/{CookieMunchClient.Enc(name)}/retire{(dryRun ? "?dryRun=1" : "")}", body, ct);
    }

    /// <summary>Un-retire a cookie (DELETE /v1/sites/:cbid/cookies/:name/retire); it is listed again everywhere.</summary>
    public Task UnretireCookieAsync(string cbid, string name, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/{CookieMunchClient.Enc(name)}/retire", null, ct);

    /// <summary>What un-retiring would leave (<c>retired</c>), with nothing removed (…/retire?dryRun=1).</summary>
    public Task<JsonObject> UnretireCookieDryRunAsync(string cbid, string name, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/{CookieMunchClient.Enc(name)}/retire?dryRun=1", null, ct);

    /// <summary>
    /// Every declaration preset (GET /v1/sites/:cbid/cookies/presets): atlas, forumulate, chatly,
    /// cookiemunch — parameters, what each declared here, and whether the vendor's manifest exists yet.
    /// </summary>
    public Task<JsonObject> CookiePresetsAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/presets", null, ct);

    /// <summary>
    /// Apply (or re-sync) a vendor preset (PUT /v1/sites/:cbid/cookies/presets/:vendor): declares what
    /// that platform writes, marked <c>vendor</c>. Atlas takes <c>frontendApiHost</c>/<c>crossSubdomainCookies</c>/<c>publishableKey</c>;
    /// Chatly <c>publicId</c>/<c>dragging</c>/<c>storage</c>/<c>tracking</c>/<c>category</c>; referral presets <c>cookieDomain</c>/<c>cookieDays</c>/<c>category</c> (referalli <c>legacyName</c>, impact <c>campaignId</c>). Names you declared are kept (<c>skipped</c>).
    /// </summary>
    public Task<JsonObject> ApplyCookiePresetAsync(string cbid, string vendor, JsonObject? parameters = null, bool dryRun = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/presets/{CookieMunchClient.Enc(vendor)}{(dryRun ? "?dryRun=1" : "")}", parameters ?? new JsonObject(), ct);

    /// <summary>
    /// Declare any vendor's cookies and storage from its embed manifest (PUT /v1/sites/:cbid/cookies/presets/custom)
    /// with <c>url</c> or <c>manifest</c> (and <c>params</c>). The vendor id is the manifest's <c>product</c>.
    /// </summary>
    public Task<JsonObject> ImportCookieManifestAsync(string cbid, JsonObject input, bool dryRun = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/presets/custom{(dryRun ? "?dryRun=1" : "")}", input, ct);

    /// <summary>What to declare (GET /v1/sites/:cbid/cookies/suggestions): detected vendors and one suggestion per undeclared row.</summary>
    public Task<JsonObject> CookieSuggestionsAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/suggestions", null, ct);

    /// <summary>Declare the suggestions (POST /v1/sites/:cbid/cookies/suggestions/declare) with optional <c>names</c> and <c>presets</c>. Run with <paramref name="dryRun"/> first.</summary>
    public Task<JsonObject> DeclareCookieSuggestionsAsync(string cbid, JsonObject? input = null, bool dryRun = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/suggestions/declare{(dryRun ? "?dryRun=1" : "")}", input ?? new JsonObject(), ct);

    /// <summary>Plan a provisioning run, storing nothing (POST /v1/sites/:cbid/cookies/plan) with optional <c>cookies</c>, <c>replace</c> and <c>presets</c> (vendor → params): the declarations PUT, then each preset in key order. Answers <c>dryRun, steps, declared, count, diff</c>.</summary>
    public Task<JsonObject> PlanCookiesAsync(string cbid, JsonObject? input = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/plan", input ?? new JsonObject(), ct);

    /// <summary>The site's own translation account (GET /v1/sites/:cbid/translation): provider, key last four and fingerprint. Never the key.</summary>
    public Task<JsonObject> TranslationAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/translation", null, ct);

    /// <summary>Connect the site's own DeepL or Google Cloud Translation key (PUT /v1/sites/:cbid/translation; <paramref name="provider"/> "deepl" or "google"). Stored sealed, never returned. <paramref name="verify"/> checks it first, sending no text.</summary>
    public Task<JsonObject> ConnectTranslationAsync(string cbid, string provider, string key, bool verify = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/translation{(verify ? "?verify=1" : "")}", new JsonObject { ["provider"] = provider, ["key"] = key }, ct);

    /// <summary>Remove the site's translation key (DELETE /v1/sites/:cbid/translation). Translations already stored stay.</summary>
    public Task<JsonObject> DisconnectTranslationAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/translation", null, ct);

    /// <summary>Machine-translate the missing purposes of the site's own cookie rows on its connected account (POST /v1/sites/:cbid/cookies/translate) with optional <c>languages</c> and <c>names</c>. Never overwrites a row's text; <paramref name="dryRun"/> calls no provider.</summary>
    public Task<JsonObject> TranslateCookiesAsync(string cbid, JsonObject? input = null, bool dryRun = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/translate{(dryRun ? "?dryRun=1" : "")}", input ?? new JsonObject(), ct);

    /// <summary>Remove every declaration a vendor preset made (DELETE /v1/sites/:cbid/cookies/presets/:vendor).</summary>
    public Task<JsonObject> RemoveCookiePresetAsync(string cbid, string vendor, bool dryRun = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/presets/{CookieMunchClient.Enc(vendor)}{(dryRun ? "?dryRun=1" : "")}", null, ct);

    /// <summary>Remove every stored (scanned or observed) row of this name (DELETE /v1/sites/:cbid/observed-cookies/:name); a declaration stays.</summary>
    public Task DeleteObservedCookieAsync(string cbid, string name, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/observed-cookies/{CookieMunchClient.Enc(name)}", null, ct);

    /// <summary>What deleting it would remove, with nothing removed (…/observed-cookies/:name?dryRun=1).</summary>
    public Task<JsonObject> DeleteObservedCookieDryRunAsync(string cbid, string name, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/observed-cookies/{CookieMunchClient.Enc(name)}?dryRun=1", null, ct);

    /// <summary>
    /// Purge observed since T (POST /v1/sites/:cbid/observed-cookies/purge): remove every row a real
    /// visit first reported at or after <paramref name="since"/> (and before <paramref name="until"/>),
    /// ISO-8601. Crawled rows and declarations stay.
    /// </summary>
    public Task<JsonObject> PurgeObservedCookiesAsync(string cbid, string since, string? until = null, bool dryRun = false, CancellationToken ct = default)
    {
        var body = new JsonObject { ["since"] = since };
        if (until is not null) body["until"] = until;
        return Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/observed-cookies/purge{(dryRun ? "?dryRun=1" : "")}", body, ct);
    }

    /// <summary>Remove a declaration (DELETE /v1/sites/:cbid/cookies/:name); the scanned classification applies again.</summary>
    public Task UndeclareCookieAsync(string cbid, string name, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/{CookieMunchClient.Enc(name)}", null, ct);

    /// <summary>
    /// What removing a declaration would leave — <c>declared</c> and a <c>diff</c> — with nothing
    /// removed (DELETE /v1/sites/:cbid/cookies/:name?dryRun=1).
    /// </summary>
    public Task<JsonObject> UndeclareCookieDryRunAsync(string cbid, string name, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/cookies/{CookieMunchClient.Enc(name)}?dryRun=1", null, ct);

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

    /// <summary>
    /// Opt-in by region and A/B variant, and the decision rate (GET /v1/sites/:cbid/consent/insights).
    /// Language and time-to-decision are not recorded (null).
    /// </summary>
    public Task<JsonObject> InsightsAsync(string cbid, RangeQuery? query = null, CancellationToken ct = default)
    {
        var q = CookieMunchClient.BuildQuery(("from", query?.From), ("to", query?.To));
        return Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/sites/{CookieMunchClient.Enc(cbid)}/consent/insights{q}", null, ct);
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

    /// <summary>The reserved categories (support_chat, referrals) a site switches on with categories.suite:
    /// stance, fallback, basis, wording in every banner language, known vendors. Public (GET /v1/categories/registry).</summary>
    public Task<JsonObject> CategoryRegistryAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/categories/registry", null, ct);

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

    /// <summary>One request, in the shape the list returns (GET /v1/dsar/:id).</summary>
    public Task<DsarRequest> GetAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<DsarRequest>(HttpMethod.Get, $"/v1/dsar/{CookieMunchClient.Enc(id)}", null, ct);

    /// <summary>
    /// Delete a TEST request with its tasks and log (DELETE /v1/dsar/:id). A real request is
    /// refused with 409 not_a_test_request; the deletion is audited.
    /// </summary>
    public Task DeleteAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/dsar/{CookieMunchClient.Enc(id)}", null, ct);

    /// <summary>
    /// Mark a CLOSED request (completed or rejected) as a test, after the fact
    /// (POST /v1/dsar/:id/mark-test). An open one is refused with 409 request_not_closed.
    /// Audited; DeleteAsync then works on it. Returns <c>{ request, changed }</c>.
    /// </summary>
    public Task<JsonObject> MarkTestAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/dsar/{CookieMunchClient.Enc(id)}/mark-test", null, ct);

    /// <summary>The request carrying one X-Trace-Id (GET /v1/dsar?trace=).</summary>
    public Task<List<JsonObject>> ListByTraceAsync(string trace, CancellationToken ct = default) =>
        Client.SendAsync<List<JsonObject>>(HttpMethod.Get, $"/v1/dsar?trace={CookieMunchClient.Enc(trace)}", null, ct);

    /// <summary>The requests with one origin (GET /v1/dsar?origin=): <c>account_deletion</c>, those a person's own account deletion opened.</summary>
    public Task<List<JsonObject>> ListByOriginAsync(string origin, CancellationToken ct = default) =>
        Client.SendAsync<List<JsonObject>>(HttpMethod.Get, $"/v1/dsar?origin={CookieMunchClient.Enc(origin)}", null, ct);

    /// <summary>
    /// Open a case fanned out to every connected system (POST /v1/dsar/requests). <paramref name="input"/>
    /// keys: cbid, type (access, deletion, closure, restriction, rectification, or an alias: erasure,
    /// erase, export), subject ({ emails, identity: { iss, sub, ext, principal } }), subjectToken, source
    /// (product or atlas attest the person and need dsar:write; admin files it received), reason,
    /// skipSystems, fulfill, test. <paramref name="idempotencyKey"/> (16–128 chars) makes a retry answer
    /// the same case; <paramref name="trace"/> is kept as the case's X-Trace-Id.
    /// </summary>
    public Task<JsonObject> OpenCaseAsync(JsonObject input, string? idempotencyKey = null, string? trace = null, CancellationToken ct = default)
    {
        var headers = new Dictionary<string, string>();
        if (idempotencyKey is not null) headers["Idempotency-Key"] = idempotencyKey;
        if (trace is not null) headers["X-Trace-Id"] = trace;
        return Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/dsar/requests", input, ct, headers);
    }

    /// <summary>One case across every system (GET /v1/dsar/requests/:id).</summary>
    public Task<JsonObject> GetCaseAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/dsar/requests/{CookieMunchClient.Enc(id)}", null, ct);

    /// <summary>Cancel a case before executeAfter (POST /v1/dsar/requests/:id/cancel). Every system keeps its data.</summary>
    public Task<JsonObject> CancelCaseAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/dsar/requests/{CookieMunchClient.Enc(id)}/cancel", null, ct);

    /// <summary>The signed (Ed25519) receipt for a case (GET /v1/dsar/requests/:id/receipt).</summary>
    public Task<JsonObject> CaseReceiptAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/dsar/requests/{CookieMunchClient.Enc(id)}/receipt", null, ct);

    /// <summary>Create a DSAR (POST /v1/dsar).</summary>
    public Task<DsarResponse> CreateAsync(DsarCreate input, CancellationToken ct = default) =>
        Client.SendAsync<DsarResponse>(HttpMethod.Post, "/v1/dsar", input, ct);

    /// <summary>Advance a DSAR to a new status (POST /v1/dsar/:id/advance). Use the <see cref="DsarStatus"/> constants.</summary>
    public Task<DsarResponse> AdvanceAsync(string id, string toStatus, CancellationToken ct = default) =>
        Client.SendAsync<DsarResponse>(HttpMethod.Post, $"/v1/dsar/{CookieMunchClient.Enc(id)}/advance", new { toStatus }, ct);

    /// <summary>Advance a DSAR, replacing the line the subject reads in the notice.</summary>
    public Task<DsarResponse> AdvanceAsync(string id, string toStatus, string messageToSubject, CancellationToken ct = default) =>
        Client.SendAsync<DsarResponse>(HttpMethod.Post, $"/v1/dsar/{CookieMunchClient.Enc(id)}/advance", new { toStatus, messageToSubject }, ct);

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

/// <summary>
/// Audit anchoring (/v1/anchors): post the head of your own hash-chained log and keep the signed receipt.
/// <c>seq</c> must strictly increase per stream; the same seq and head again returns the original receipt,
/// a different head for an anchored seq is a 409 (<c>anchor_head_mismatch</c>). Create needs
/// <c>anchors:write</c>; List and Get need <c>anchors:read</c>.
/// </summary>
public sealed class AnchorsResource : ResourceBase
{
    internal AnchorsResource(CookieMunchClient client) : base(client) { }

    /// <summary>Anchor <paramref name="head"/> at <paramref name="seq"/> on <paramref name="stream"/> (POST /v1/anchors). Signed with the key in /v1/partner/jwks.json.</summary>
    public Task<JsonObject> CreateAsync(string stream, string head, long seq, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/anchors", new JsonObject { ["stream"] = stream, ["head"] = head, ["seq"] = seq }, ct);

    /// <summary>A stream's receipts, newest first (GET /v1/anchors/{stream}): <c>{stream, anchors, nextBefore}</c>. Pass <c>nextBefore</c> as <paramref name="before"/> until it is null.</summary>
    public Task<JsonObject> ListAsync(string stream, int? limit = null, long? before = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/anchors/{CookieMunchClient.Enc(stream)}" + CookieMunchClient.BuildQuery(("limit", limit), ("before", before)), null, ct);

    /// <summary>One receipt (GET /v1/anchors/{stream}/{id}).</summary>
    public Task<JsonObject> GetAsync(string stream, string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/anchors/{CookieMunchClient.Enc(stream)}/{CookieMunchClient.Enc(id)}", null, ct);
}

/// <summary>
/// The organisation's sandbox (/v1/sandbox): a separate test organisation with the same people and
/// plan features, never billed. Its keys are test keys and its rights requests are all tests.
/// Create and Delete need a full-access key or <c>orgs:create</c>.
/// </summary>
public sealed class SandboxResource : ResourceBase
{
    internal SandboxResource(CookieMunchClient client) : base(client) { }

    /// <summary>The sandbox, or null (GET /v1/sandbox). From a sandbox key, the sandbox itself.</summary>
    public async Task<JsonObject?> GetAsync(CancellationToken ct = default) =>
        (await Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/sandbox", null, ct).ConfigureAwait(false))?["sandbox"] as JsonObject;

    /// <summary>Make the sandbox (or reuse it, <c>created: false</c>) and a new test key for it, shown once (POST /v1/sandbox).</summary>
    public Task<JsonObject> CreateAsync(string? keyName = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/sandbox", keyName is null ? new JsonObject() : new JsonObject { ["keyName"] = keyName }, ct);

    /// <summary>Erase the sandbox and everything in it (DELETE /v1/sandbox). Cannot be undone; the live organisation is not touched.</summary>
    public Task<JsonObject> DeleteAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, "/v1/sandbox", null, ct);
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

    /// <summary>
    /// Set a key's expiry (epoch ms) or, with null, remove it (PATCH /v1/keys/:prefix with <c>expiresAt</c>).
    /// </summary>
    public Task<JsonObject> SetExpiryAsync(string prefix, long? expiresAt, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Patch, $"/v1/keys/{CookieMunchClient.Enc(prefix)}", new JsonObject { ["expiresAt"] = expiresAt }, ct);

    /// <summary>
    /// Key hygiene (GET /v1/keys/report): keys unused for <paramref name="unusedDays"/> (default 90), keys
    /// with broad access, and keys expiring within <paramref name="expiringDays"/> (default 14), each with a reason.
    /// </summary>
    public Task<JsonObject> ReportAsync(int? unusedDays = null, int? expiringDays = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (unusedDays is not null) q.Add($"unusedDays={unusedDays}");
        if (expiringDays is not null) q.Add($"expiringDays={expiringDays}");
        return Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/keys/report" + (q.Count > 0 ? "?" + string.Join("&", q) : ""), null, ct);
    }

    /// <summary>The suite-standard listing (GET /v1/keys?shape=standard).</summary>
    public Task<JsonObject> ListStandardAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/keys?shape=standard", null, ct);

    /// <summary>The calling key itself, with any key (GET /v1/keys/self).</summary>
    public Task<JsonObject> SelfAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/keys/self", null, ct);

    /// <summary>Re-pin the calling key to another dated API version, any key, for itself (PATCH /v1/keys/self).</summary>
    public Task<JsonObject> UpdateSelfAsync(string apiVersion, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Patch, "/v1/keys/self", new JsonObject { ["apiVersion"] = apiVersion }, ct);

    /// <summary>
    /// The calling key rotates itself (POST /v1/keys/self/roll). The old secret stops at once; set an
    /// <c>Idempotency-Key</c> in <see cref="CookieMunchClient.ExtraHeaders"/> so a lost answer replays.
    /// </summary>
    public Task<JsonObject> RollSelfAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/keys/self/roll", null, ct);

    /// <summary>
    /// A short-lived key, at most 12 hours, within this key's own scopes and properties (POST /v1/keys/vend).
    /// Needs <c>keys:write</c> or a full-access key.
    /// </summary>
    public Task<JsonObject> VendAsync(List<string> scopes, int ttlSeconds, List<string>? cbids = null, string? name = null, CancellationToken ct = default)
    {
        var body = new JsonObject { ["scopes"] = new JsonArray(scopes.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()), ["ttlSeconds"] = ttlSeconds };
        if (cbids is not null) body["cbids"] = new JsonArray(cbids.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray());
        if (name is not null) body["name"] = name;
        return Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/keys/vend", body, ct);
    }

    /// <summary>
    /// Swap an identity provider's token for a short-lived, scoped key (POST /v1/oauth/token, RFC 8693).
    /// Needs no API key: the route never reads one, so a client made with any placeholder key works.
    /// <paramref name="resource"/> is <c>org:&lt;id&gt;</c> or <c>site:&lt;cbid&gt;</c>.
    /// </summary>
    public Task<JsonObject> ExchangeOperatorTokenAsync(string subjectToken, List<string> scope, string resource, string subjectTokenType = "access_token", int? expiresIn = null, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange", ["subject_token"] = subjectToken,
            ["subject_token_type"] = "urn:ietf:params:oauth:token-type:" + subjectTokenType, ["scope"] = string.Join(" ", scope),
            ["resource"] = resource, ["audience"] = "cookiemunch",
        };
        if (expiresIn is not null) body["expires_in"] = expiresIn;
        return Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/oauth/token", body, ct);
    }

    /// <summary>Revoke a published secret (POST /v1/keys/revoke_leaked). Needs no valid key; always answers <c>received</c>.</summary>
    public Task<JsonObject> RevokeLeakedAsync(string key, string? source = null, CancellationToken ct = default)
    {
        var body = new JsonObject { ["key"] = key };
        if (source is not null) body["source"] = source;
        return Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/keys/revoke_leaked", body, ct);
    }
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

    /// <summary>
    /// Rotate a subscription's signing secret to one you chose, 32–256 printable characters
    /// (POST /v1/webhooks/:id/roll). The secret is not echoed; true when it was rolled.
    /// </summary>
    public async Task<bool> RollSecretAsync(string id, string secret, CancellationToken ct = default)
    {
        var body = await Client.SendAsync<JsonObject>(
            HttpMethod.Post, $"/v1/webhooks/{CookieMunchClient.Enc(id)}/roll", new JsonObject { ["secret"] = secret }, ct).ConfigureAwait(false);
        return body["rolled"]?.GetValue<bool>() ?? false;
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

    /// <summary>
    /// Send what a failing or disabled endpoint missed since <paramref name="since"/> (epoch ms or ISO date),
    /// oldest first, stopping at the first failure (POST /v1/webhooks/:id/replay).
    /// </summary>
    public Task<JsonObject> ReplayAsync(string id, string since, int? limit = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/webhooks/{CookieMunchClient.Enc(id)}/replay{CookieMunchClient.BuildQuery(("since", since), ("limit", limit))}", null, ct);

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

    /// <summary>
    /// Your organisation's own data-controller identity (GET /v1/org/controller). Every rights notice
    /// names it, before any site config <c>controller</c> block. <c>set</c> is false when there is none.
    /// </summary>
    public Task<JsonObject> ControllerAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/org/controller", null, ct);

    /// <summary>
    /// Set the organisation's data-controller identity (PUT /v1/org/controller). Keys: <c>legalName</c>
    /// (required), <c>contactEmail</c>, <c>address</c>, <c>dpoContact</c>. Replaces the stored one: a key
    /// left out is removed. Needs an unscoped key or <c>rights:write</c>.
    /// </summary>
    public Task<JsonObject> SetControllerAsync(JsonObject input, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, "/v1/org/controller", input, ct);

    /// <summary>Remove the identity (DELETE /v1/org/controller); notices then name the site config <c>controller</c> block.</summary>
    public Task RemoveControllerAsync(CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, "/v1/org/controller", null, ct);

    /// <summary>
    /// Your own mail provider for rights-request mail (GET /v1/org/mail-provider). Never returns the
    /// credential, only <c>secretLast4</c>; <c>sender</c> says whether verification codes go through it
    /// now (<c>own</c>) or are not mailed at all (<c>none</c>) — there is no shared sender.
    /// </summary>
    public Task<JsonObject> MailProviderAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/org/mail-provider", null, ct);

    /// <summary>
    /// Connect your own mail provider (PUT /v1/org/mail-provider) — recommended. Keys: <c>provider</c>
    /// (brevo, sendgrid, postmark, resend, ses, smtp), <c>fromEmail</c>, <c>fromName</c>, <c>replyTo</c>,
    /// <c>secret</c> (write-only; leave out to keep the stored one, same provider only), <c>ses</c>
    /// (<c>region</c>, <c>accessKeyId</c>) or <c>smtp</c> (<c>host</c>, <c>port</c>, <c>security</c>,
    /// <c>username</c>). Replaces the stored one; it carries verification codes only after
    /// <see cref="TestMailProviderAsync"/> succeeds.
    /// </summary>
    public Task<JsonObject> SetMailProviderAsync(JsonObject input, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, "/v1/org/mail-provider", input, ct);

    /// <summary>
    /// Send a test through your provider (POST /v1/org/mail-provider/test). A null <paramref name="to"/>
    /// sends to the reply-to, else the from address. A refusal comes back as <c>ok: false</c>.
    /// </summary>
    public Task<JsonObject> TestMailProviderAsync(string? to = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/org/mail-provider/test",
            to is null ? new JsonObject() : new JsonObject { ["to"] = to }, ct);

    /// <summary>Remove your provider (DELETE /v1/org/mail-provider); no verification codes are mailed after this.</summary>
    public Task RemoveMailProviderAsync(CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, "/v1/org/mail-provider", null, ct);

    /// <summary>The default captcha for your rights pages, for sites without their own (GET /v1/org/rights-captcha). Never the secret.</summary>
    public Task<JsonObject> RightsCaptchaAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/org/rights-captcha", null, ct);

    /// <summary>Set the default (PUT /v1/org/rights-captcha): own keys (recommended) or off. Same keys as <see cref="RightsCaptchaResource.SetAsync"/>.</summary>
    public Task<JsonObject> SetRightsCaptchaAsync(JsonObject input, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, "/v1/org/rights-captcha", input, ct);

    /// <summary>Remove the default (DELETE /v1/org/rights-captcha): sites without their own setting have no captcha. Returns the setting now in force.</summary>
    public Task<JsonObject> RemoveRightsCaptchaAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, "/v1/org/rights-captcha", null, ct);

    /// <summary>The default sign-in provider rights requests are verified with, for sites without their own (GET /v1/org/rights-identity).</summary>
    public Task<JsonObject> RightsIdentityAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/org/rights-identity", null, ct);

    /// <summary>Set the default (PUT /v1/org/rights-identity). Same keys as <see cref="RightsIdentityResource.SetAsync"/>.</summary>
    public Task<JsonObject> SetRightsIdentityAsync(JsonObject input, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, "/v1/org/rights-identity", input, ct);

    /// <summary>Remove the default (DELETE /v1/org/rights-identity). Returns the setting now in force.</summary>
    public Task<JsonObject> RemoveRightsIdentityAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, "/v1/org/rights-identity", null, ct);
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
