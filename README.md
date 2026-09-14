# CookieMunch .NET SDK

A typed, dependency-free .NET client for the [Cookie Munch](https://cookiemunch.net) Developer
API — a self-hosted Consent Management Platform. It covers the full `/v1` surface (sites,
consent, DSAR, vendors, RoPA, brand kits, preferences, members, keys, webhooks, banners) plus
the public **consent-log ingest** endpoint, which is handy for logging consent from desktop
apps (WPF / WinUI / MAUI) that render their own consent UI.

- **.NET 8+**, `nullable` enabled, `async` throughout.
- No third-party dependencies — just `System.Net.Http` + `System.Text.Json`.
- Namespace `CookieMunch`.

## Install

```bash
dotnet add package CookieMunch
```

## Quick start

```csharp
using CookieMunch;

// The org is derived server-side from the key — you never pass an orgId.
using var client = new CookieMunchClient("fck_live_your_key_here");
// Self-hosted? Pass your API origin:
// using var client = new CookieMunchClient("fck_…", "https://cmp.example.com");

Identity me = await client.MeAsync();
Console.WriteLine($"{me.OrgId} / {me.Plan}");

// Sites
List<Site> sites = await client.Sites.ListAsync();
Site site = await client.Sites.CreateAsync(new SiteCreate { Domain = "example.com" });
InstallSnippet snip = await client.Sites.SnippetAsync(site.Cbid);

// Consent analytics + audit
List<ConsentDay> stats = await client.Consent.StatsAsync(site.Cbid, new RangeQuery { From = from, To = to });
List<ConsentLogRow> log = await client.Consent.LogAsync(site.Cbid, new LogQuery { Limit = 100 });
string csv = await client.Consent.ExportAsync(site.Cbid);
System.Text.Json.Nodes.JsonNode receipt = await client.Consent.ReceiptAsync(site.Cbid, stamp);

// DSAR
var dsar = await client.Dsar.CreateAsync(new DsarCreate
{
    Type = DsarType.Access,
    SubjectEmail = "user@example.com",
    Regulation = Regulation.Gdpr,
});
await client.Dsar.AdvanceAsync(dsar.Request.Id, DsarStatus.InProgress);
```

## Logging consent from a desktop app

If your WPF / WinUI / MAUI app renders its own consent dialog, log the decision to the public,
unauthenticated ingest endpoint. Records are anonymised (the IP is truncated) and hash-chained
for tamper-evidence server-side.

```csharp
await client.Consent.RecordAsync(new ConsentRecordInput
{
    Cbid    = "your-site-cbid",
    Stamp   = Guid.NewGuid().ToString(),          // stable per-subject id (for later erase/export)
    Choices = new ConsentChoices { Preferences = true, Statistics = false, Marketing = true },
    Method  = "explicit",                          // or "implied"
    Ver     = 1,
    Utc     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    Url     = "app://settings/privacy",
});
```

Later, honour a data-subject request by stamp:

```csharp
SubjectExport export = await client.Consent.ExportSubjectAsync(cbid, stamp);   // GDPR access/portability
EraseResult erased  = await client.Consent.EraseSubjectAsync(cbid, stamp);     // crypto-erase (irreversible)
```

## Error handling

Every non-2xx response throws `CookieMunchApiException`, carrying `StatusCode`, the raw `Body`,
and — when the server returns a JSON error envelope — the parsed `Error` and `Code`.

```csharp
try
{
    await client.Sites.GetAsync("missing");
}
catch (CookieMunchApiException ex)
{
    Console.WriteLine($"{ex.StatusCode}: {ex.Error} (code={ex.Code})");
}
```

## Notes

- The API key is sent as both `Authorization: Bearer <key>` and `X-API-Key: <key>`.
- The `/v1` prefix is appended automatically to developer-API calls; the public consent
  ingest lives at `/api/v1/consent`.
- `CookieMunchClient` is thread-safe and owns a single `HttpClient` — construct once and reuse.
  You can also pass your own `HttpClient` (e.g. from `IHttpClientFactory`); when you do, the SDK
  will not dispose it.
- Open, server-owned JSON payloads (site config, banner design JSON, signed receipts) are
  surfaced as `System.Text.Json.Nodes.JsonObject` / `JsonNode` so you can read and write
  arbitrary keys.

## License

MIT
