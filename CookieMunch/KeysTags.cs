using System.Text.Json.Nodes;

namespace CookieMunch;

/// <summary>Tags on created resources (type: org, site, webhook, executor, dsar, partner_link).</summary>
public sealed class MetadataResource : ResourceBase
{
    internal MetadataResource(CookieMunchClient client) : base(client) { }

    /// <summary>A resource's tags, externalRef and sandbox flag (GET /v1/metadata/{type}/{id}).</summary>
    public Task<JsonObject> GetAsync(string type, string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/metadata/{CookieMunchClient.Enc(type)}/{CookieMunchClient.Enc(id)}", null, ct);

    /// <summary>
    /// Merge tags (a JSON null removes one) (PATCH /v1/metadata/{type}/{id}). <c>stack_sandbox</c> cannot
    /// be set here. <paramref name="dryRun"/> answers with the diff.
    /// </summary>
    public Task<JsonObject> UpdateAsync(string type, string id, JsonObject metadata, bool dryRun = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(new HttpMethod("PATCH"), $"/v1/metadata/{CookieMunchClient.Enc(type)}/{CookieMunchClient.Enc(id)}{(dryRun ? "?dryRun=1" : "")}",
            new JsonObject { ["metadata"] = JsonNode.Parse(metadata.ToJsonString()) }, ct);
}

/// <summary>Suite-standard partner links (/v1/partner_links).</summary>
public sealed class PartnerLinksResource : ResourceBase
{
    internal PartnerLinksResource(CookieMunchClient client) : base(client) { }

    /// <summary>Every partner link of the organisation (GET /v1/partner_links).</summary>
    public Task<JsonArray> ListAsync(IDictionary<string, string>? metadata = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonArray>(HttpMethod.Get, "/v1/partner_links" + CookieMunchClient.MetaQuery(metadata), null, ct);

    /// <summary>
    /// Make a partner link (POST /v1/partner_links): <c>partner</c>, <c>resource</c> (<c>site:&lt;cbid&gt;</c> or
    /// <c>org:&lt;id&gt;</c>), <c>scopes</c>, and optionally <c>expires_at</c>, <c>externalId</c>, <c>label</c>.
    /// </summary>
    public Task<JsonObject> CreateAsync(JsonObject input, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/partner_links", input, ct);

    /// <summary>Revoke a partner link at once (DELETE /v1/partner_links/{id}).</summary>
    public Task<JsonObject> RevokeAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Delete, $"/v1/partner_links/{CookieMunchClient.Enc(id)}", null, ct);
}
