using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace CookieMunch;

/// <summary>The hosted rights page document (/v1/sites/:cbid/rights-page).</summary>
public sealed class RightsPageResource : ResourceBase
{
    internal RightsPageResource(CookieMunchClient client) : base(client) { }

    private static string Path(string cbid, string suffix = "") => $"/v1/sites/{CookieMunchClient.Enc(cbid)}/rights-page{suffix}";

    /// <summary>The draft, with publish warnings (GET).</summary>
    public Task<JsonObject> GetAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, Path(cbid), null, ct);

    /// <summary>Replace the draft (PUT). Nothing is live until PublishAsync. Add "baseUpdatedAt" (the draft's updatedAt as last read; null if it had none) to refuse overwriting a newer save: a mismatch is a 409 draft_conflict.</summary>
    public Task<JsonObject> PutAsync(string cbid, JsonObject page, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, Path(cbid), page, ct);

    /// <summary>Merge into the draft (PATCH): objects merge, arrays replace, null removes.</summary>
    public Task<JsonObject> PatchAsync(string cbid, JsonObject page, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(new HttpMethod("PATCH"), Path(cbid), page, ct);

    /// <summary>Validate a write without saving it (?dryRun=1).</summary>
    public Task<JsonObject> DryRunAsync(string cbid, JsonObject page, bool replace = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(replace ? HttpMethod.Put : new HttpMethod("PATCH"), Path(cbid, "?dryRun=1"), page, ct);

    /// <summary>The live document.</summary>
    public Task<JsonObject> GetPublishedAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, Path(cbid, "/published"), null, ct);

    /// <summary>Publish the draft as the next version; it goes live at once. Prefer the overload taking the base you read.</summary>
    public Task<JsonObject> PublishAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, Path(cbid, "/publish"), new JsonObject(), ct);

    /// <summary>Publish the draft only if it is still the one you read: <paramref name="baseUpdatedAt"/> is its updatedAt (epoch ms or an ISO string; null if it had none). A mismatch is a 409 draft_conflict and nothing goes live.</summary>
    public Task<JsonObject> PublishAsync(string cbid, JsonNode? baseUpdatedAt, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, Path(cbid, "/publish"), new JsonObject { ["baseUpdatedAt"] = baseUpdatedAt }, ct);

    /// <summary>Published versions, newest first.</summary>
    public Task<JsonObject> VersionsAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, Path(cbid, "/versions"), null, ct);

    /// <summary>Publish an earlier version again; it goes live at once and REPLACES the current draft (unpublished edits are lost).</summary>
    public Task<JsonObject> RollbackAsync(string cbid, int version, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, Path(cbid, "/rollback"), new JsonObject { ["version"] = version }, ct);

    /// <summary>As RollbackAsync, refused with a 409 draft_conflict if the draft changed since <paramref name="baseUpdatedAt"/>.</summary>
    public Task<JsonObject> RollbackAsync(string cbid, int version, JsonNode? baseUpdatedAt, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, Path(cbid, "/rollback"), new JsonObject { ["version"] = version, ["baseUpdatedAt"] = baseUpdatedAt }, ct);

    private static string Base(string cbid) => Path(cbid, "/domains");

    /// <summary>Custom hostnames serving the rights page, with the plan allowance: <c>domains</c>, <c>limit</c> (null = unlimited), <c>used</c>, <c>target</c>.</summary>
    public Task<JsonObject> ListDomainsAsync(string cbid, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, Base(cbid), null, ct);

    /// <summary>Add a custom hostname (e.g. privacy.example.com); <c>dns</c> holds the records to create.</summary>
    public Task<JsonObject> AddDomainAsync(string cbid, string hostname, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, Base(cbid), new JsonObject { ["hostname"] = hostname }, ct);

    /// <summary>Check it now: DNS while pending, the HTTPS answer once routed.</summary>
    public Task<JsonObject> VerifyDomainAsync(string cbid, string hostname, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"{Base(cbid)}/{CookieMunchClient.Enc(hostname)}/verify", null, ct);

    /// <summary>Remove it: it stops being served and its certificate is not renewed.</summary>
    public Task RemoveDomainAsync(string cbid, string hostname, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"{Base(cbid)}/{CookieMunchClient.Enc(hostname)}", null, ct);
}
