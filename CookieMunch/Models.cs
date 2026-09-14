using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CookieMunch;

// ─────────────────────────────────────────────────────────────────────────────
// Request/response shapes for the Cookie Munch Developer API (/v1). These mirror
// packages/sdk/src/types.ts and the extra shapes in packages/sdk/src/index.ts.
//
// SiteConfig and other "open, deeply-nested object" payloads are modelled as
// System.Text.Json.Nodes.JsonObject so callers can read/write arbitrary keys, and
// opaque server-owned payloads (signed receipts) as JsonNode.
// ─────────────────────────────────────────────────────────────────────────────

// Open, deeply-nested config objects owned by @cookiemunch/core are modelled as
// System.Text.Json.Nodes.JsonObject directly in method signatures.

/// <summary>Identity / echo for SDK bootstrapping (GET /v1/me).</summary>
public sealed record Identity(
    [property: JsonPropertyName("orgId")] string OrgId,
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("keyPrefix")] string KeyPrefix);

/// <summary>A registered site (property).</summary>
public sealed record Site
{
    [JsonPropertyName("cbid")] public string Cbid { get; init; } = "";
    [JsonPropertyName("orgId")] public string OrgId { get; init; } = "";
    [JsonPropertyName("domain")] public string Domain { get; init; } = "";
    [JsonPropertyName("verified")] public bool Verified { get; init; }
    [JsonPropertyName("verifyToken")] public string? VerifyToken { get; init; }
    [JsonPropertyName("verifyMethod")] public string? VerifyMethod { get; init; }
    [JsonPropertyName("verifiedAt")] public long? VerifiedAt { get; init; }
}

/// <summary>Input for creating a site (cbid auto-generated when omitted).</summary>
public sealed record SiteCreate
{
    [JsonPropertyName("domain")] public required string Domain { get; init; }
    [JsonPropertyName("cbid")] public string? Cbid { get; init; }
}

/// <summary>Aggregated per-day consent stats.</summary>
public sealed record ConsentDay
{
    [JsonPropertyName("Date")] public string Date { get; init; } = "";
    [JsonPropertyName("OptIn")] public int OptIn { get; init; }
    [JsonPropertyName("OptOut")] public int OptOut { get; init; }
    [JsonPropertyName("OptInImplied")] public int OptInImplied { get; init; }
    [JsonPropertyName("OptInStrict")] public int OptInStrict { get; init; }
    [JsonPropertyName("TypeOptInPref")] public int TypeOptInPref { get; init; }
    [JsonPropertyName("TypeOptInStat")] public int TypeOptInStat { get; init; }
    [JsonPropertyName("TypeOptInMark")] public int TypeOptInMark { get; init; }
    [JsonPropertyName("Impressions")] public int Impressions { get; init; }
    [JsonPropertyName("Countries")] public Dictionary<string, int> Countries { get; init; } = new();
}

/// <summary>The three standard consent categories.</summary>
public sealed record ConsentChoices
{
    [JsonPropertyName("preferences")] public bool Preferences { get; init; }
    [JsonPropertyName("statistics")] public bool Statistics { get; init; }
    [JsonPropertyName("marketing")] public bool Marketing { get; init; }
}

/// <summary>A single anonymised consent record (from the log view).</summary>
public sealed record ConsentLogRow
{
    [JsonPropertyName("stamp")] public string Stamp { get; init; } = "";
    [JsonPropertyName("receivedAt")] public long ReceivedAt { get; init; }
    [JsonPropertyName("region")] public string Region { get; init; } = "";
    [JsonPropertyName("method")] public string Method { get; init; } = "";
    [JsonPropertyName("choices")] public ConsentChoices Choices { get; init; } = new();
    [JsonPropertyName("anonIp")] public string AnonIp { get; init; } = "";
    [JsonPropertyName("url")] public string Url { get; init; } = "";
}

/// <summary>Optional { from, to } epoch-ms window for stats/log/export.</summary>
public sealed record RangeQuery
{
    public long? From { get; init; }
    public long? To { get; init; }
}

/// <summary>A range query that also caps the number of returned rows.</summary>
public sealed record LogQuery
{
    public long? From { get; init; }
    public long? To { get; init; }
    public int? Limit { get; init; }
}

/// <summary>DSAR request kinds.</summary>
public static class DsarType
{
    public const string Access = "access";
    public const string Deletion = "deletion";
    public const string Rectification = "rectification";
    public const string Portability = "portability";
    public const string OptOut = "opt-out";
}

/// <summary>Supported regulations.</summary>
public static class Regulation
{
    public const string Gdpr = "gdpr";
    public const string Ccpa = "ccpa";
}

/// <summary>DSAR lifecycle statuses.</summary>
public static class DsarStatus
{
    public const string Received = "received";
    public const string Verifying = "verifying";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Rejected = "rejected";
}

/// <summary>A data-subject access request.</summary>
public sealed record DsarRequest
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("subjectEmail")] public string SubjectEmail { get; init; } = "";
    [JsonPropertyName("regulation")] public string RegulationValue { get; init; } = "";
    [JsonPropertyName("status")] public string Status { get; init; } = "";
    [JsonPropertyName("createdAt")] public long CreatedAt { get; init; }
    [JsonPropertyName("dueAt")] public long DueAt { get; init; }
    [JsonPropertyName("note")] public string? Note { get; init; }
}

/// <summary>Input for creating a DSAR. Use the <see cref="DsarType"/>/<see cref="Regulation"/> constants.</summary>
public sealed record DsarCreate
{
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("subjectEmail")] public required string SubjectEmail { get; init; }
    [JsonPropertyName("regulation")] public required string Regulation { get; init; }
    [JsonPropertyName("note")] public string? Note { get; init; }
}

/// <summary>Wrapper the server returns for single-DSAR responses.</summary>
public sealed record DsarResponse
{
    [JsonPropertyName("request")] public DsarRequest Request { get; init; } = new();
}

/// <summary>Input for creating a vendor.</summary>
public sealed record VendorInput
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("category")] public required string Category { get; init; }
    [JsonPropertyName("dataShared")] public IReadOnlyList<string> DataShared { get; init; } = Array.Empty<string>();
    [JsonPropertyName("dpaSigned")] public bool DpaSigned { get; init; }
    [JsonPropertyName("subprocessors")] public int Subprocessors { get; init; }
    [JsonPropertyName("certifications")] public IReadOnlyList<string> Certifications { get; init; } = Array.Empty<string>();
    [JsonPropertyName("region")] public string Region { get; init; } = "";
}

/// <summary>A computed vendor risk score.</summary>
public sealed record RiskScore
{
    [JsonPropertyName("score")] public double Score { get; init; }
    [JsonPropertyName("band")] public string Band { get; init; } = "";
}

/// <summary>A vendor as listed, with its computed risk flattened in.</summary>
public sealed record ScoredVendor
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("category")] public string Category { get; init; } = "";
    [JsonPropertyName("dataShared")] public IReadOnlyList<string> DataShared { get; init; } = Array.Empty<string>();
    [JsonPropertyName("dpaSigned")] public bool DpaSigned { get; init; }
    [JsonPropertyName("subprocessors")] public int Subprocessors { get; init; }
    [JsonPropertyName("certifications")] public IReadOnlyList<string> Certifications { get; init; } = Array.Empty<string>();
    [JsonPropertyName("region")] public string Region { get; init; } = "";
    [JsonPropertyName("risk")] public RiskScore Risk { get; init; } = new();
}

/// <summary>The wrapped response of POST /v1/vendors.</summary>
public sealed record VendorCreateResult
{
    [JsonPropertyName("vendor")] public JsonObject Vendor { get; init; } = new();
    [JsonPropertyName("risk")] public RiskScore Risk { get; init; } = new();
}

/// <summary>GDPR Article 6 lawful bases.</summary>
public static class LegalBasis
{
    public const string Consent = "consent";
    public const string Contract = "contract";
    public const string LegalObligation = "legal-obligation";
    public const string VitalInterests = "vital-interests";
    public const string PublicTask = "public-task";
    public const string LegitimateInterests = "legitimate-interests";
}

/// <summary>Input for creating a RoPA entry.</summary>
public record RopaInput
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("purpose")] public required string Purpose { get; init; }
    [JsonPropertyName("legalBasis")] public required string LegalBasis { get; init; }
    [JsonPropertyName("dataCategories")] public IReadOnlyList<string> DataCategories { get; init; } = Array.Empty<string>();
    [JsonPropertyName("recipients")] public IReadOnlyList<string> Recipients { get; init; } = Array.Empty<string>();
    [JsonPropertyName("retentionDays")] public int RetentionDays { get; init; }
    [JsonPropertyName("crossBorderTransfer")] public bool CrossBorderTransfer { get; init; }
}

/// <summary>A RoPA entry as stored (RopaInput + id).</summary>
public sealed record RopaEntry : RopaInput
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
}

/// <summary>The wrapped response of POST /v1/ropa.</summary>
public sealed record RopaCreateResult
{
    [JsonPropertyName("entry")] public RopaEntry Entry { get; init; } = new() { Name = "", Purpose = "", LegalBasis = "" };
}

/// <summary>A cookie discovered on a site, classified into a consent category.</summary>
public sealed record SiteCookie
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("domain")] public string Domain { get; init; } = "";
    [JsonPropertyName("category")] public string Category { get; init; } = "";
    [JsonPropertyName("provider")] public string? Provider { get; init; }
    [JsonPropertyName("purpose")] public string? Purpose { get; init; }
    [JsonPropertyName("expiry")] public string? Expiry { get; init; }
    [JsonPropertyName("firstSeen")] public long? FirstSeen { get; init; }
}

/// <summary>The state/result of a site cookie scan.</summary>
public sealed record ScanResult
{
    [JsonPropertyName("scanId")] public string? ScanId { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = "";
    [JsonPropertyName("startedAt")] public long? StartedAt { get; init; }
    [JsonPropertyName("finishedAt")] public long? FinishedAt { get; init; }
    [JsonPropertyName("pagesScanned")] public int? PagesScanned { get; init; }
    [JsonPropertyName("cookiesFound")] public int? CookiesFound { get; init; }
    [JsonPropertyName("lastScannedAt")] public long? LastScannedAt { get; init; }
}

/// <summary>A/B banner experiment results for a single variant.</summary>
public sealed record AbResult
{
    [JsonPropertyName("variant")] public string Variant { get; init; } = "";
    [JsonPropertyName("impressions")] public int Impressions { get; init; }
    [JsonPropertyName("optIn")] public int OptIn { get; init; }
    [JsonPropertyName("optIns")] public int OptIns { get; init; }
    [JsonPropertyName("optOut")] public int OptOut { get; init; }
    [JsonPropertyName("optInRate")] public double OptInRate { get; init; }
}

/// <summary>The response shape of GET /v1/sites/:cbid/snippet.</summary>
public sealed record InstallSnippet
{
    [JsonPropertyName("snippet")] public string Snippet { get; init; } = "";
    [JsonPropertyName("src")] public string Src { get; init; } = "";
    [JsonPropertyName("api")] public string? Api { get; init; }
    [JsonPropertyName("cbid")] public string Cbid { get; init; } = "";
    [JsonPropertyName("blockingMode")] public string BlockingMode { get; init; } = "";
}

/// <summary>Options for the snippet endpoint.</summary>
public sealed record SnippetOptions
{
    /// <summary>Override the blocking mode ('auto' | 'manual' | 'checklist'). Default: 'auto'.</summary>
    public string? BlockingMode { get; init; }

    /// <summary>Language culture override, e.g. "en" or "fr".</summary>
    public string? Culture { get; init; }
}

/// <summary>The result of domain verification.</summary>
public sealed record VerifyResult
{
    [JsonPropertyName("verified")] public bool Verified { get; init; }
    [JsonPropertyName("method")] public string? Method { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
}

/// <summary>Theme tokens extracted from a site homepage ("Match my site").</summary>
public sealed record BrandSuggestion
{
    [JsonPropertyName("background")] public string? Background { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("highlight")] public string? Highlight { get; init; }
    [JsonPropertyName("fontFamily")] public string? FontFamily { get; init; }
    [JsonPropertyName("fontUrl")] public string? FontUrl { get; init; }
    [JsonPropertyName("palette")] public IReadOnlyList<string>? Palette { get; init; }
}

/// <summary>The response shape of POST /v1/sites/:cbid/brand.</summary>
public sealed record BrandExtractionResult
{
    [JsonPropertyName("suggestion")] public BrandSuggestion Suggestion { get; init; } = new();
}

/// <summary>A flow validation/lint finding.</summary>
public sealed record FlowIssue
{
    [JsonPropertyName("code")] public string Code { get; init; } = "";
    [JsonPropertyName("message")] public string Message { get; init; } = "";
    [JsonPropertyName("path")] public string? Path { get; init; }
}

/// <summary>A site's v2 banner flow (views, categories) plus any lint issues.</summary>
public sealed record SiteFlow
{
    [JsonPropertyName("v")] public int V { get; init; }
    [JsonPropertyName("flow")] public JsonObject Flow { get; init; } = new();
    [JsonPropertyName("categories")] public JsonObject Categories { get; init; } = new();
    [JsonPropertyName("customCss")] public string? CustomCss { get; init; }
    [JsonPropertyName("lint")] public IReadOnlyList<FlowIssue> Lint { get; init; } = Array.Empty<FlowIssue>();
}

/// <summary>
/// Result of editFlow/setFlow. On success <see cref="Ok"/> is true and <see cref="Flow"/> is
/// the persisted flow; on a validation/lint failure <see cref="Ok"/> is false, nothing was
/// saved, and <see cref="Issues"/> says why (the HTTP status is still 200).
/// </summary>
public sealed record FlowOpResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("flow")] public JsonObject? Flow { get; init; }
    [JsonPropertyName("failedAt")] public int? FailedAt { get; init; }
    [JsonPropertyName("issues")] public IReadOnlyList<FlowIssue>? Issues { get; init; }
}

/// <summary>A reusable brand kit (colors/logo/typography) for banner theming.</summary>
public sealed record BrandKit
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("orgId")] public string OrgId { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("theme")] public JsonNode? Theme { get; init; }
    [JsonPropertyName("content")] public JsonNode? Content { get; init; }
    [JsonPropertyName("logoUrl")] public string? LogoUrl { get; init; }
    [JsonPropertyName("customCss")] public string? CustomCss { get; init; }
}

/// <summary>Input for creating a brand kit.</summary>
public sealed record BrandKitCreate
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("theme")] public JsonNode? Theme { get; init; }
    [JsonPropertyName("content")] public JsonNode? Content { get; init; }
    [JsonPropertyName("logoUrl")] public string? LogoUrl { get; init; }
    [JsonPropertyName("customCss")] public string? CustomCss { get; init; }
}

/// <summary>The wrapped response of POST /v1/brand-kits.</summary>
public sealed record BrandKitCreateResult
{
    [JsonPropertyName("kit")] public BrandKit Kit { get; init; } = new();
}

/// <summary>A configurable consent preference/purpose item.</summary>
public sealed record PreferenceItem
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("orgId")] public string OrgId { get; init; } = "";
    [JsonPropertyName("cbid")] public string? Cbid { get; init; }
    [JsonPropertyName("label")] public string Label { get; init; } = "";
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("category")] public string? Category { get; init; }
}

/// <summary>An organization member.</summary>
public sealed record Member
{
    [JsonPropertyName("userId")] public string UserId { get; init; } = "";
    [JsonPropertyName("email")] public string Email { get; init; } = "";
    [JsonPropertyName("role")] public string Role { get; init; } = "";
}

/// <summary>The wrapped { member } response for member mutations.</summary>
public sealed record MemberResult
{
    [JsonPropertyName("member")] public Member Member { get; init; } = new();
}

/// <summary>An issued API key. The <see cref="Key"/> is returned ONCE on issue.</summary>
public sealed record ApiKey
{
    [JsonPropertyName("prefix")] public string Prefix { get; init; } = "";
    [JsonPropertyName("createdAt")] public long? CreatedAt { get; init; }
    [JsonPropertyName("key")] public string? Key { get; init; }
}

/// <summary>Org usage/quota summary.</summary>
public sealed record Usage
{
    [JsonPropertyName("domains")] public int Domains { get; init; }
    [JsonPropertyName("seats")] public int Seats { get; init; }
    [JsonPropertyName("monthlyEvents")] public int MonthlyEvents { get; init; }
}

/// <summary>A webhook subscription. <see cref="Secret"/> is present only in the create response.</summary>
public sealed record WebhookSubscription
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("orgId")] public string OrgId { get; init; } = "";
    [JsonPropertyName("url")] public string Url { get; init; } = "";
    [JsonPropertyName("secret")] public string? Secret { get; init; }
    [JsonPropertyName("events")] public IReadOnlyList<string> Events { get; init; } = Array.Empty<string>();
    [JsonPropertyName("cbid")] public string? Cbid { get; init; }
    [JsonPropertyName("active")] public bool Active { get; init; }
    [JsonPropertyName("createdAt")] public long CreatedAt { get; init; }
}

/// <summary>Input for creating a webhook subscription.</summary>
public sealed record WebhookCreate
{
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("events")] public required IReadOnlyList<string> Events { get; init; }
    [JsonPropertyName("cbid")] public string? Cbid { get; init; }
}

/// <summary>A lightweight summary of an account-level banner (GET /v1/banners).</summary>
public sealed record BannerSummary
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("updatedAt")] public long UpdatedAt { get; init; }
    [JsonPropertyName("assignedCbids")] public IReadOnlyList<string> AssignedCbids { get; init; } = Array.Empty<string>();
}

/// <summary>A full account-level banner record, including its config JSON.</summary>
public sealed record BannerRecord
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("orgId")] public string OrgId { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("json")] public JsonObject Json { get; init; } = new();
    [JsonPropertyName("createdAt")] public long CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public long UpdatedAt { get; init; }
}

/// <summary>Input for creating a banner design.</summary>
public sealed record BannerCreate
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("json")] public required JsonObject Json { get; init; }
}

/// <summary>Patch for updating a banner design's name and/or json.</summary>
public sealed record BannerUpdate
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("json")] public JsonObject? Json { get; init; }
}

/// <summary>{ cbids } — the sites a banner design is assigned to.</summary>
public sealed record BannerAssignments
{
    [JsonPropertyName("cbids")] public IReadOnlyList<string> Cbids { get; init; } = Array.Empty<string>();
}

/// <summary>{ publishedCbids } — result of publishing a banner design.</summary>
public sealed record BannerPublishResult
{
    [JsonPropertyName("publishedCbids")] public IReadOnlyList<string> PublishedCbids { get; init; } = Array.Empty<string>();
}

/// <summary>{ erased } — count of consent records crypto-erased for a subject.</summary>
public sealed record EraseResult
{
    [JsonPropertyName("erased")] public int Erased { get; init; }
}

/// <summary>A data subject's exported consent records (GDPR access/portability).</summary>
public sealed record SubjectExport
{
    [JsonPropertyName("cbid")] public string Cbid { get; init; } = "";
    [JsonPropertyName("stamp")] public string Stamp { get; init; } = "";
    [JsonPropertyName("records")] public IReadOnlyList<JsonNode?> Records { get; init; } = Array.Empty<JsonNode?>();
    [JsonPropertyName("count")] public int Count { get; init; }
}

// ── Public consent-log ingest (POST /api/v1/consent) ─────────────────────────

/// <summary>
/// The body of the public consent-log ingest endpoint (POST /api/v1/consent). Mirrors the
/// server's <c>ConsentPayload</c>. Records are anonymised (the IP is truncated server-side)
/// and hash-chained for tamper-evidence. Handy for logging consent from a WPF/WinUI/MAUI
/// desktop app that renders its own consent UI.
/// </summary>
public sealed record ConsentRecordInput
{
    /// <summary>The site identifier the consent belongs to.</summary>
    [JsonPropertyName("cbid")] public required string Cbid { get; init; }

    /// <summary>A stable, per-subject stamp (the consent-receipt id) used for erase/export.</summary>
    [JsonPropertyName("stamp")] public required string Stamp { get; init; }

    /// <summary>The subject's category choices.</summary>
    [JsonPropertyName("choices")] public required ConsentChoices Choices { get; init; }

    /// <summary>How consent was captured: "explicit" or "implied".</summary>
    [JsonPropertyName("method")] public required string Method { get; init; }

    /// <summary>The consent/notice version number.</summary>
    [JsonPropertyName("ver")] public required int Ver { get; init; }

    /// <summary>Epoch ms the consent decision was made.</summary>
    [JsonPropertyName("utc")] public required long Utc { get; init; }

    /// <summary>The URL / screen where consent was captured.</summary>
    [JsonPropertyName("url")] public required string Url { get; init; }

    /// <summary>Optional variant id (A/B).</summary>
    [JsonPropertyName("variant")] public string? Variant { get; init; }

    /// <summary>Optional IAB TCF consent string (TCF mode only).</summary>
    [JsonPropertyName("tcString")] public string? TcString { get; init; }

    /// <summary>Optional GPP string (GPP mode only).</summary>
    [JsonPropertyName("gppString")] public string? GppString { get; init; }

    /// <summary>Optional named-purpose map beyond the standard categories.</summary>
    [JsonPropertyName("purposes")] public Dictionary<string, bool>? Purposes { get; init; }

    /// <summary>Optional opaque digest of the exact notice text shown to the subject.</summary>
    [JsonPropertyName("subjectPolicyHash")] public string? SubjectPolicyHash { get; init; }

    /// <summary>Optional map: named purpose → RoPA entry id.</summary>
    [JsonPropertyName("purposeRopa")] public Dictionary<string, string>? PurposeRopa { get; init; }

    /// <summary>
    /// Optional, app-supplied STABLE cross-surface subject id (e.g. a logged-in account id).
    /// Lets an org correlate one subject's consent across all its sites/surfaces. Opaque —
    /// stored and hashed server-side, never interpreted. Omitted from the wire when null.
    /// </summary>
    [JsonPropertyName("subjectId")] public string? SubjectId { get; init; }
}
