using System.Text.Json.Nodes;

namespace CookieMunch;

// The privacy platform beyond the consent banner, the reseller API, and the inputs the
// newer site/key calls take. Identity, vault and profile reads are POSTs on purpose: a
// person's identifiers travel in the request body, never in a URL where logs and proxies
// would keep them.
//
// Optional members are nullable and omitted from the request when null. Where null MEANS
// something — clearing a DSAR routing override, widening a webhook to the whole org — the
// call takes a JsonObject instead, because a record's null would be dropped.

/// <summary>One of the ways a person is known, e.g. <c>new Identifier("email_sha256", hex)</c>.</summary>
public sealed record Identifier(string Space, string Value);

/// <summary>One purpose decision recorded against a person.</summary>
public sealed record ConsentDecision(string Purpose, bool Allowed, string? LegalBasis = null, string? Jurisdiction = null, string? Provenance = null, long? CollectedAt = null);

/// <summary>One attribute value with its provenance.</summary>
public sealed record ProfileAttribute(string Value, string? Purpose = null, long? CollectedAt = null);

/// <summary>One entry in the org's subscription topic catalog.</summary>
public sealed record SubscriptionTopic(string Code, List<string> Channels, string? Name = null, Dictionary<string, string>? Downstream = null);

/// <summary>A prompt or response to check against consent and policy.</summary>
public sealed record AiInspectInput(string Prompt, string Purpose, string? Model = null, string? Actor = null, string? Direction = null, Dictionary<string, bool>? Consent = null);

/// <summary>An AI system to declare.</summary>
public sealed record AiSystem(string Id, string Name, string? Provider = null, string? Purpose = null);

/// <summary>One system a request must be carried out in. Operation is locate, export, erase or optOut.</summary>
public sealed record FulfillmentSystem(string System, string Operation);

/// <summary>Tunes a warehouse enforcement plan.</summary>
public sealed record EnforcementOptions(string? PermitsTable = null, string? PolicyPrefix = null);

/// <summary>Provisions a child org. With MintKey, its first API key is returned once as <c>apiKey</c>.</summary>
public sealed record ResellerChildCreate(string Name, string? OwnerEmail = null, JsonObject? Controller = null, JsonObject? WhiteLabel = null, bool? DelegatedAccess = null, bool? MintKey = null, List<string>? KeyScopes = null);

/// <summary>Mints a key for a child org.</summary>
public sealed record ChildKeyInput(string? Name = null, List<string>? Scopes = null, List<string>? Cbids = null);

/// <summary>A least-privilege API key: scopes, a property lock (<c>Cbids</c>), and an expiry of 1–3650 days.</summary>
public sealed record ApiKeyIssueInput(string? Name = null, List<string>? Scopes = null, List<string>? Cbids = null, int? ExpiresInDays = null);

/// <summary>Tunes a generated policy.</summary>
public sealed record PolicyOptions(string? ContactEmail = null, string? EffectiveDate = null, List<string>? Jurisdictions = null);

/// <summary>Turns the personalised-ads choice on or off on a site's banner.</summary>
public sealed record AdPersonalizationInput(bool Enabled, bool? Default = null, string? Label = null);

/// <summary>A captured browsing session. Supply Har or Requests.</summary>
public sealed record SessionAnalysisInput(JsonNode? Har = null, List<JsonNode>? Requests = null, Dictionary<string, bool>? Consent = null, bool? Gpc = null);

/// <summary>One site to create in a bulk call.</summary>
public sealed record BulkSite(string Domain, string? Cbid = null, string? Platform = null);

/// <summary>A person as a cluster of identifiers (/v1/identity).</summary>
public sealed class IdentityResource : ResourceBase
{
    internal IdentityResource(CookieMunchClient client) : base(client) { }

    /// <summary>The subject id for these identifiers, or a null <c>subjectId</c> if unknown.</summary>
    public Task<JsonObject> ResolveAsync(List<Identifier> identifiers, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/identity/resolve", new { identifiers }, ct);

    /// <summary>Stitch identifiers into one subject. A durable merge.</summary>
    public Task<JsonObject> LinkAsync(List<Identifier> identifiers, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/identity/link", new { identifiers }, ct);

    /// <summary>Every identifier stitched to a subject.</summary>
    public Task<JsonObject> ClusterAsync(string subjectId, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/identity/{CookieMunchClient.Enc(subjectId)}", null, ct);
}

/// <summary>A resolved person's consent (/v1/vault).</summary>
public sealed class VaultResource : ResourceBase
{
    internal VaultResource(CookieMunchClient client) : base(client) { }

    public Task<JsonObject> RecordAsync(List<Identifier> identifiers, List<ConsentDecision> decisions, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/vault/record", new { identifiers, decisions }, ct);

    /// <summary>Allow/deny per purpose, across all of the person's identifiers.</summary>
    public Task<JsonObject> CurrentAsync(List<Identifier> identifiers, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/vault/current", new { identifiers }, ct);

    /// <summary>The same decisions in full: legal basis, jurisdiction, provenance, time.</summary>
    public Task<JsonObject> PermitsAsync(List<Identifier> identifiers, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/vault/permits", new { identifiers }, ct);
}

/// <summary>Attributes, with consent enforced when they are used (/v1/profile).</summary>
public sealed class ProfileResource : ResourceBase
{
    internal ProfileResource(CookieMunchClient client) : base(client) { }

    public Task<JsonObject> GetAsync(List<Identifier> identifiers, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/profile/get", new { identifiers }, ct);

    public Task<JsonObject> SetAttributesAsync(List<Identifier> identifiers, Dictionary<string, ProfileAttribute> attributes, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/profile/attributes", new { identifiers, attributes }, ct);

    /// <summary>Attribute values usable for <paramref name="purpose"/> — empty when the person has not consented to it.</summary>
    public Task<JsonObject> ActivateAsync(List<Identifier> identifiers, string purpose, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/profile/activate", new { identifiers, purpose }, ct);
}

/// <summary>Marketing preferences as topics x channels (/v1/subscriptions).</summary>
public sealed class SubscriptionsResource : ResourceBase
{
    internal SubscriptionsResource(CookieMunchClient client) : base(client) { }

    public Task<JsonObject> TopicsAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/subscriptions/topics", null, ct);

    /// <summary>Replace the catalog. It is authored whole; anything omitted is removed.</summary>
    public Task<JsonObject> SetTopicsAsync(List<SubscriptionTopic> topics, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, "/v1/subscriptions/topics", new { topics }, ct);

    public Task<JsonObject> GetAsync(string subjectId, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/subscriptions/{CookieMunchClient.Enc(subjectId)}", null, ct);

    public Task<JsonObject> SetAsync(string subjectId, string topic, string channel, bool optedIn, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, $"/v1/subscriptions/{CookieMunchClient.Enc(subjectId)}", new { topic, channel, optedIn }, ct);

    public Task<JsonObject> UnsubscribeAllAsync(string subjectId, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/subscriptions/{CookieMunchClient.Enc(subjectId)}/unsubscribe-all", null, ct);

    /// <summary>Lift a global unsubscribe, restoring the per-topic choices from before it.</summary>
    public Task<JsonObject> ResubscribeAsync(string subjectId, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/subscriptions/{CookieMunchClient.Enc(subjectId)}/resubscribe", null, ct);

    public Task<JsonObject> ActivationAsync(string subjectId, List<SubscriptionTopic> topics, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/subscriptions/{CookieMunchClient.Enc(subjectId)}/activation", new { topics }, ct);
}

/// <summary>DPIA, PIA, LIA, TIA, AI-impact and vendor assessments (/v1/assessments).</summary>
public sealed class AssessmentsResource : ResourceBase
{
    internal AssessmentsResource(CookieMunchClient client) : base(client) { }

    public Task<JsonObject> TemplatesAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/assessments/templates", null, ct);

    public Task<JsonObject> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/assessments", null, ct);

    public Task<JsonObject> StartAsync(string template, string subject, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/assessments", new { template, subject }, ct);

    public Task<JsonObject> GetAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/assessments/{CookieMunchClient.Enc(id)}", null, ct);

    public Task<JsonObject> AnswerAsync(string id, string questionId, JsonNode value, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/assessments/{CookieMunchClient.Enc(id)}/answer", new { questionId, value }, ct);

    /// <summary>Fill factual answers from the latest data map. Never overwrites a human answer.</summary>
    public Task<JsonObject> AutoPopulateFromMapAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/assessments/{CookieMunchClient.Enc(id)}/autopopulate-from-map", null, ct);

    /// <summary>Fill from evidence you supply (questionId → answer), stamped with <paramref name="source"/>. Never overwrites a human answer.</summary>
    public Task<JsonObject> AutoPopulateAsync(string id, JsonObject evidence, string? source = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/assessments/{CookieMunchClient.Enc(id)}/autopopulate", new { evidence, source }, ct);

    public Task<JsonObject> SubmitAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/assessments/{CookieMunchClient.Enc(id)}/submit", null, ct);

    /// <summary>Record approval. <paramref name="by"/> becomes the approval record — pass the person who approved.</summary>
    public Task<JsonObject> ApproveAsync(string id, string by, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/assessments/{CookieMunchClient.Enc(id)}/approve", new { by }, ct);

    public Task<JsonObject> RejectAsync(string id, string by, string reason, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/assessments/{CookieMunchClient.Enc(id)}/reject", new { by, reason }, ct);
}

/// <summary>The data map from an in-environment scan, metadata only (/v1/discovery).</summary>
public sealed class DiscoveryResource : ResourceBase
{
    internal DiscoveryResource(CookieMunchClient client) : base(client) { }

    public Task<JsonObject> IngestMapAsync(JsonObject map, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/discovery/map", new { map }, ct);

    public Task<JsonObject> GetMapAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/discovery/map", null, ct);

    public Task<JsonObject> RopaDraftsAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/discovery/ropa-drafts", null, ct);

    public Task<JsonObject> EvidenceAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/discovery/evidence", null, ct);

    /// <summary>What changed since the last scan, and where the RoPA disagrees with reality.</summary>
    public Task<JsonObject> DriftAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/discovery/drift", null, ct);

    /// <summary>Plan masking / row-access policy for postgres, mysql or snowflake. Applies nothing.</summary>
    public Task<JsonObject> PlanEnforcementAsync(string dialect, List<JsonObject> rules, EnforcementOptions? options = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/discovery/enforcement",
            new { dialect, rules, permitsTable = options?.PermitsTable, policyPrefix = options?.PolicyPrefix }, ct);
}

/// <summary>AI governance: policy, the inline gateway, inventory and lineage (/v1/ai).</summary>
public sealed class AiResource : ResourceBase
{
    internal AiResource(CookieMunchClient client) : base(client) { }

    public Task<JsonObject> GetPolicyAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/ai/policy", null, ct);

    /// <summary>Replace the AI gateway policy.</summary>
    public Task<JsonObject> SetPolicyAsync(JsonObject policy, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Put, "/v1/ai/policy", new { policy }, ct);

    /// <summary>Enforce consent and policy on a prompt or response. Needs the ai:inspect scope.</summary>
    public Task<JsonObject> InspectAsync(AiInspectInput input, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/ai/inspect", input, ct);

    public Task<JsonObject> InventoryAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/ai/inventory", null, ct);

    public Task<JsonObject> LineageAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/ai/lineage", null, ct);

    public Task<JsonObject> RegisterSystemAsync(AiSystem system, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/ai/systems", system, ct);

    public Task<JsonObject> SystemsAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/ai/systems", null, ct);

    public Task<JsonObject> AuditAsync(int? limit = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/ai/audit" + CookieMunchClient.BuildQuery(("limit", limit)), null, ct);
}

/// <summary>DSAR fulfilment: plan work per system, and the in-environment agent's protocol.</summary>
public sealed class FulfillmentResource : ResourceBase
{
    internal FulfillmentResource(CookieMunchClient client) : base(client) { }

    public Task<JsonObject> SlaAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/dsar/sla", null, ct);

    public Task<JsonObject> PlanAsync(string requestId, List<FulfillmentSystem> systems, bool includeHistorical = false, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/dsar/{CookieMunchClient.Enc(requestId)}/plan",
            new { systems, includeHistorical = includeHistorical ? true : (bool?)null }, ct);

    public Task<JsonObject> StatusAsync(string requestId, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/dsar/{CookieMunchClient.Enc(requestId)}/fulfillment", null, ct);

    /// <summary>Systems connected to run part of a request themselves. Never includes credentials.</summary>
    public Task<List<JsonObject>> ExecutorsAsync(CancellationToken ct = default) =>
        Client.SendAsync<List<JsonObject>>(HttpMethod.Get, "/v1/dsar/executors", null, ct);

    /// <summary>
    /// Connect one. <paramref name="profile"/> describes that system's API — paths, the words
    /// it uses for export and erase, its status vocabulary, how it signs webhooks — so
    /// connecting a new platform needs no code. The secret is stored encrypted and never
    /// returned; the response carries the webhook URL to configure in that system.
    /// </summary>
    public Task<JsonObject> ConnectExecutorAsync(
        string system,
        string baseUrl,
        string secretKey,
        JsonObject profile,
        string? webhookSecret = null,
        bool? auto = null,
        CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["system"] = system,
            ["baseUrl"] = baseUrl,
            ["secretKey"] = secretKey,
            ["profile"] = profile,
        };
        if (webhookSecret is not null) body["webhookSecret"] = webhookSecret;
        if (auto is not null) body["auto"] = auto.Value;
        return Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/dsar/executors", body, ct);
    }

    /// <summary>Disconnect a system; its open sub-tasks stop being driven.</summary>
    public Task DisconnectExecutorAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject?>(HttpMethod.Delete, $"/v1/dsar/executors/{CookieMunchClient.Enc(id)}", null, ct);

    /// <summary>The export bundle a connected system produced, fetched from it on demand.</summary>
    public Task<JsonObject> TaskExportAsync(string requestId, string taskId, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get,
            $"/v1/dsar/{CookieMunchClient.Enc(requestId)}/tasks/{CookieMunchClient.Enc(taskId)}/export", null, ct);

    /// <summary>For the in-environment agent: tasks to execute inside your network.</summary>
    public Task<JsonObject> PendingTasksAsync(int? limit = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/dsar/agent/tasks" + CookieMunchClient.BuildQuery(("limit", limit)), null, ct);

    /// <summary>For the in-environment agent: only the outcome crosses the boundary.</summary>
    public Task<JsonObject> ReportTaskAsync(string taskId, bool ok, string? error = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, $"/v1/dsar/agent/tasks/{CookieMunchClient.Enc(taskId)}/result", new { ok, error }, ct);
}

/// <summary>The curated privacy-law dataset (/v1/regulatory).</summary>
public sealed class RegulatoryResource : ResourceBase
{
    internal RegulatoryResource(CookieMunchClient client) : base(client) { }

    public Task<JsonObject> FeedAsync(List<string>? jurisdictions = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/regulatory/feed" + CookieMunchClient.BuildQuery(
            ("jurisdictions", jurisdictions is null ? null : string.Join(",", jurisdictions))), null, ct);

    public Task<JsonObject> UpcomingAsync(int? days = null, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/regulatory/upcoming" + CookieMunchClient.BuildQuery(("days", days)), null, ct);
}

/// <summary>Provision and manage child orgs (/v1/reseller). Needs the reseller:* scopes.</summary>
public sealed class ResellerResource : ResourceBase
{
    internal ResellerResource(CookieMunchClient client) : base(client) { }

    public Task<JsonObject> ListAsync(CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, "/v1/reseller/customers", null, ct);

    public Task<JsonObject> CreateAsync(ResellerChildCreate input, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Post, "/v1/reseller/customers", input, ct);

    public Task<JsonObject> GetAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/reseller/customers/{CookieMunchClient.Enc(id)}", null, ct);

    /// <summary>Update a child org. <c>["dsarRouting"] = null</c> clears its override; omitting the key leaves it alone.</summary>
    public Task<JsonObject> UpdateAsync(string id, JsonObject patch, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Patch, $"/v1/reseller/customers/{CookieMunchClient.Enc(id)}", patch, ct);

    /// <summary>Suspend a child org (reversible). With <paramref name="purge"/> it is deleted with its data — irreversibly.</summary>
    public Task DeprovisionAsync(string id, bool purge = false, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/reseller/customers/{CookieMunchClient.Enc(id)}" + CookieMunchClient.BuildQuery(("purge", purge ? "true" : null)), null, ct);

    public Task<List<ApiKey>> ListKeysAsync(string id, CancellationToken ct = default) =>
        Client.SendAsync<List<ApiKey>>(HttpMethod.Get, $"/v1/reseller/customers/{CookieMunchClient.Enc(id)}/keys", null, ct);

    /// <summary>Mint an API key for a child org; the secret is returned once.</summary>
    public Task<ApiKey> MintKeyAsync(string id, ChildKeyInput? input = null, CancellationToken ct = default) =>
        Client.SendAsync<ApiKey>(HttpMethod.Post, $"/v1/reseller/customers/{CookieMunchClient.Enc(id)}/keys", (object?)input ?? new { }, ct);

    public Task RevokeKeyAsync(string id, string prefix, CancellationToken ct = default) =>
        Client.SendAsync(HttpMethod.Delete, $"/v1/reseller/customers/{CookieMunchClient.Enc(id)}/keys/{CookieMunchClient.Enc(prefix)}", null, ct);
}

/// <summary>One person's consent across every site in the org, by the subject id your apps attach (/v1/subjects).</summary>
public sealed class SubjectsResource : ResourceBase
{
    internal SubjectsResource(CookieMunchClient client) : base(client) { }

    /// <summary>Needs consent:read; not available to property-locked keys.</summary>
    public Task<JsonObject> ConsentAsync(string subjectId, CancellationToken ct = default) =>
        Client.SendAsync<JsonObject>(HttpMethod.Get, $"/v1/subjects/{CookieMunchClient.Enc(subjectId)}/consent", null, ct);
}
