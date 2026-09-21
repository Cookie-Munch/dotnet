using System.Collections;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace CookieMunch.Tests;

/// <summary>
/// Every operation the Developer API documents is reachable from this SDK.
///
/// The list lives in <c>sdks/operations.json</c>, generated from the server's OpenAPI
/// document and shared by all six server-side SDKs. Every public async method of every
/// resource is called through a recording handler — arguments built from their reflected
/// types — and what reached the wire is compared in both directions.
/// </summary>
public class ParityTests
{
    private const string Placeholder = "x1";

    [Fact]
    public async Task EveryDocumentedOperationIsReachable()
    {
        var fixture = JsonDocument.Parse(File.ReadAllText(FindOperationsJson()));
        var operations = fixture.RootElement.GetProperty("operations").EnumerateArray().Select(e => e.GetString()!).ToList();

        var seen = new SortedSet<string>();
        var handler = new MockHttpMessageHandler((req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.StartsWith("/v1/"))
            {
                var segs = path.Split('/').Select(s => s == Placeholder ? "{}" : s);
                lock (seen) seen.Add($"{req.Method.Method} {string.Join("/", segs)}");
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        });
        using var client = new CookieMunchClient("fck_test", "https://api.example.test", new HttpClient(handler));

        var targets = new List<object> { client };
        foreach (var prop in typeof(CookieMunchClient).GetProperties())
        {
            if (prop.PropertyType.IsSubclassOf(typeof(ResourceBase))) targets.Add(prop.GetValue(client)!);
        }

        foreach (var target in targets)
        {
            foreach (var m in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!typeof(Task).IsAssignableFrom(m.ReturnType)) continue;
                var args = m.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : Build(p.ParameterType)).ToArray();
                try
                {
                    await (Task)m.Invoke(target, args)!;
                }
                catch
                {
                    // only what reached the wire matters here
                }
            }
        }

        var missing = operations.Where(o => !seen.Contains(o)).ToList();
        Assert.True(missing.Count == 0, $"{missing.Count} documented operations are unreachable:\n  {string.Join("\n  ", missing)}");
        var extra = seen.Where(o => !operations.Contains(o)).ToList();
        Assert.True(extra.Count == 0, $"calls the API does not document:\n  {string.Join("\n  ", extra)}");
    }

    private static object? Build(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        if (u == typeof(string)) return Placeholder;
        if (u == typeof(bool)) return true;
        if (u == typeof(int)) return 1;
        if (u == typeof(long)) return 1L;
        if (u == typeof(CancellationToken)) return CancellationToken.None;
        if (u == typeof(JsonObject)) return new JsonObject { ["a"] = Placeholder };
        if (u == typeof(JsonNode)) return JsonValue.Create(Placeholder);
        if (u == typeof(object)) return new JsonObject { ["a"] = Placeholder };
        if (u.IsGenericType && typeof(IEnumerable).IsAssignableFrom(u))
        {
            var args = u.GetGenericArguments();
            if (args.Length == 2)
            {
                var dict = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args))!;
                dict["a"] = Build(args[1]);
                return dict;
            }
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(args[0]))!;
            list.Add(Build(args[0]));
            return list;
        }
        if (u.IsClass || (u.IsValueType && !u.IsPrimitive))
        {
            var ctor = u.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (ctor is null) return Activator.CreateInstance(u);
            return ctor.Invoke(ctor.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : Build(p.ParameterType)).ToArray());
        }
        return null;
    }

    private static string FindOperationsJson()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "operations.json"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new FileNotFoundException("sdks/operations.json"), "operations.json");
    }
}
