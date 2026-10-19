using System.Net;
using System.Text;
using System.Text.Json;
using Stock.Core;

var catalog = new Catalog();
var target = new Settings().Targets[0];
if (args.Contains("--live"))
{
    using var client = new AppleClient();
    var result = await client.CheckAsync([target], "山东 青岛", CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(new { timestamp = DateTimeOffset.Now, transport = "C# HttpClient", result }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}
int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); passed++; Console.WriteLine("PASS " + name); }
Result Parse(string json) => AppleClient.Parse(Encoding.UTF8.GetBytes(json), [target])[0];
string Envelope(string display, string? innerPart = null) => JsonSerializer.Serialize(new { head = new { status = "200" }, body = new { stores = new[] { new { storeNumber = "R557", partsAvailability = new Dictionary<string, object> { [target.PartNumber] = new { partNumber = innerPart ?? target.PartNumber, pickupDisplay = display } } } } } });
Check(catalog.Products.Any(p => p.PartNumber == target.PartNumber && p.Title == target.ProductName), "preset SKU, capacity and colour match catalog");
Check(catalog.Stores.Single(s => s.Number == target.StoreNumber).Location == "山东 青岛", "Qingdao location and store identity");
Check(Parse(Envelope("available")).Kind == StockKind.InStock, "available");
Check(Parse(Envelope("unavailable")).Kind == StockKind.OutOfStock, "unavailable");
Check(Parse(Envelope("ineligible")).Kind == StockKind.OutOfStock, "ineligible");
Check(Parse(Envelope("future-status")).Kind == StockKind.Unknown, "new status fails closed");
Check(Parse(Envelope("available", "WRONG")).Kind == StockKind.Unknown, "mismatched SKU fails closed");
Check(Parse(Envelope("available").Replace("R557", "R001")).Kind == StockKind.Unknown, "other store cannot satisfy target");
Check(Parse("<html>Blocked</html>").Kind == StockKind.Unknown, "HTML cannot mean no stock");
Check(Parse("{\"head\":{\"status\":500},\"body\":{\"stores\":[]}}").Kind == StockKind.Unknown, "failed envelope");
Check(Parse("{\"body\":{\"errorMessage\":\"error\",\"stores\":[]}}").Kind == StockKind.Unknown, "Apple error");
Check(Parse("{\"head\":{\"status\":200},\"body\":{\"stores\":[]}}").Kind == StockKind.Unknown, "empty stores cannot mean no stock");
Check(Parse(Envelope("available").Replace("partsAvailability", "missing")).Kind == StockKind.Unknown, "missing products");
var tracker = new ArrivalTracker();
Check(tracker.Update(Parse(Envelope("available"))) && !tracker.Update(Parse(Envelope("available"))), "continuous stock only alerts once");
tracker.Update(Parse(Envelope("unavailable")));
Check(tracker.Update(Parse(Envelope("available"))), "restock alerts again");
var dir = Path.Combine(Path.GetTempPath(), "IphoneStockChecks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
var path = Path.Combine(dir, "settings.json"); var store = new SettingsStore(path);
store.Save(new()); Check(store.Load().Targets[0] == target, "configuration roundtrip");
store.Save(new() { Targets = [] }); Check(new SettingsStore(path).Load().Targets.Count == 0, "empty target list survives restart");
File.WriteAllText(path, "{broken"); var broken = new SettingsStore(path); broken.Load();
bool rejected = false; try { broken.Save(new()); } catch (IOException) { rejected = true; }
Check(rejected && File.ReadAllText(path) == "{broken" && Directory.GetFiles(dir, "*.corrupt-*").Length == 1, "corrupt config retained and protected");
var fake = new FakeHandler();
using (var client = new AppleClient(fake))
{
    var result = await client.CheckAsync([target], "山东 青岛", CancellationToken.None);
    Check(result[0].Kind == StockKind.Unknown && client.NextAllowed > DateTimeOffset.UtcNow.AddMinutes(14), "HTTP 541 becomes unknown with 15-minute cooldown");
    await client.CheckAsync([target], "山东 青岛", CancellationToken.None);
    Check(fake.PickupCalls == 1, "manual recheck cannot bypass cooldown");
}
foreach (var code in new[] { 403, 541, 429 })
{
    var warmBlocked = new SequenceHandler(code);
    using var guarded = new AppleClient(warmBlocked);
    var result = await guarded.CheckAsync([target], "山东 青岛", CancellationToken.None);
    await guarded.CheckAsync([target], "山东 青岛", CancellationToken.None);
    Check(result[0].Kind == StockKind.Unknown && warmBlocked.Calls == 1 && guarded.NextAllowed > DateTimeOffset.UtcNow.AddSeconds(110),
        $"warm-up HTTP {code} stops pickup and manual retries");
}
foreach (var raw in new[] { "bad", "1", "180", "9223372036854775807", "Wed, 21 Oct 2037 07:28:00 GMT" })
{
    var limited = new SequenceHandler(429, raw);
    using var guarded = new AppleClient(limited);
    var result = await guarded.CheckAsync([target], "山东 青岛", CancellationToken.None);
    Check(result[0].Kind == StockKind.Unknown && guarded.NextAllowed > DateTimeOffset.UtcNow.AddSeconds(raw == "180" ? 170 : 110),
        $"Retry-After safely respected: {raw}");
}
var cooldownPath = Path.Combine(dir, "request-cooldown.txt");
using (var first = new AppleClient(new SequenceHandler(541), cooldownPath))
    await first.CheckAsync([target], "山东 青岛", CancellationToken.None);
var afterRestart = new SequenceHandler(200);
using (var restored = new AppleClient(afterRestart, cooldownPath))
{
    var result = await restored.CheckAsync([target], "山东 青岛", CancellationToken.None);
    Check(result[0].Kind == StockKind.Unknown && afterRestart.Calls == 0, "restart preserves blocked cooldown without network requests");
}
File.WriteAllText(cooldownPath, "broken");
var corruptRequests = new SequenceHandler(200);
using (var restored = new AppleClient(corruptRequests, cooldownPath))
{
    await restored.CheckAsync([target], "山东 青岛", CancellationToken.None);
    Check(corruptRequests.Calls == 0, "corrupt cooldown record fails closed for 15 minutes");
}
Check(!Directory.GetFiles(dir, "*.tmp").Any(), "atomic save leaves no temporary files");
Console.WriteLine($"{passed} checks passed. Fixtures: {dir}");

sealed class FakeHandler : HttpMessageHandler
{
    public int PickupCalls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var pickup = request.RequestUri!.AbsolutePath.Contains("pickup-message");
        if (pickup) PickupCalls++;
        return Task.FromResult(new HttpResponseMessage(pickup ? (HttpStatusCode)541 : HttpStatusCode.OK) { Content = new StringContent("{}") });
    }
}

sealed class SequenceHandler(int code, string? retry = null) : HttpMessageHandler
{
    public int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        var response = new HttpResponseMessage((HttpStatusCode)code) { Content = new StringContent("<html>blocked</html>") };
        if (retry is not null) response.Headers.TryAddWithoutValidation("Retry-After", retry);
        return Task.FromResult(response);
    }
}
