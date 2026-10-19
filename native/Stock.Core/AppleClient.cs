using System.Net;
using System.Text.Json;

namespace Stock.Core;

public sealed class AppleClient : IDisposable
{
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate = new(1);
    private bool warmed;
    private readonly string? cooldownPath;
    private DateTimeOffset nextAllowed;
    public DateTimeOffset NextAllowed
    {
        get => nextAllowed;
        private set
        {
            nextAllowed = value;
            if (cooldownPath is null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cooldownPath))!);
            var temporary = cooldownPath + ".tmp";
            File.WriteAllText(temporary, value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            File.Move(temporary, cooldownPath, true);
        }
    }
    public AppleClient(HttpMessageHandler? handler = null, string? cooldownPath = null)
    {
        this.cooldownPath = cooldownPath;
        if (cooldownPath is not null && File.Exists(cooldownPath))
        {
            try
            {
                if (new FileInfo(cooldownPath).Length > 128) throw new InvalidDataException("冷却记录无效");
                nextAllowed = DateTimeOffset.Parse(File.ReadAllText(cooldownPath), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            { nextAllowed = DateTimeOffset.UtcNow.AddMinutes(15); }
        }
        http = new(handler ?? new HttpClientHandler { CookieContainer = new CookieContainer(), AutomaticDecompression = DecompressionMethods.All });
        http.Timeout = TimeSpan.FromSeconds(35);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9");
    }
    public async Task<IReadOnlyList<Result>> CheckAsync(IReadOnlyList<Target> targets, string location, CancellationToken token)
    {
        var callerToken = token;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        token = deadline.Token;
        await gate.WaitAsync(token);
        try
        {
            if (DateTimeOffset.UtcNow < NextAllowed) return Unknown(targets, $"请求冷却中，下次可查询：{NextAllowed.ToLocalTime():HH:mm:ss}");
            NextAllowed = DateTimeOffset.UtcNow.AddSeconds(60);
            if (!warmed)
            {
                warmed = true;
                try
                {
                    using var warm = await http.GetAsync("https://www.apple.com.cn/shop/bag", HttpCompletionOption.ResponseHeadersRead, token);
                    var warmFailure = ApplyThrottle(warm);
                    if (warmFailure is not null) return Unknown(targets, warmFailure);
                }
                catch (HttpRequestException) { /* Warm-up is auxiliary, not a stock result. */ }
                catch (TaskCanceledException) when (!token.IsCancellationRequested) { }
                await Task.Delay(1500, token);
            }
            var query = new List<string> { "pl=true", "mts.0=regular", $"location={Uri.EscapeDataString(location)}" };
            query.AddRange(targets.Select(t => t.PartNumber).Distinct().Select((p, i) => $"parts.{i}={Uri.EscapeDataString(p)}"));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.apple.com.cn/shop/retail/pickup-message?" + string.Join('&', query));
            request.Headers.Referrer = new Uri("https://www.apple.com.cn/shop/buy-iphone/iphone-duo");
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            var status = (int)response.StatusCode;
            var throttle = ApplyThrottle(response);
            if (throttle is not null) return Unknown(targets, throttle);
            if (!response.IsSuccessStatusCode) return Unknown(targets, $"Apple 查询失败（HTTP {status}）");
            await using var input = await response.Content.ReadAsStreamAsync(token);
            using var buffer = new MemoryStream();
            var chunk = new byte[16384];
            int count;
            while ((count = await input.ReadAsync(chunk, token)) > 0)
            {
                if (buffer.Length + count > 4 * 1024 * 1024) return Unknown(targets, "响应过大，未作库存判断");
                buffer.Write(chunk, 0, count);
            }
            return Parse(buffer.ToArray(), targets);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Unknown(targets, "查询超时，请检查网络"); }
        catch (HttpRequestException ex) { return Unknown(targets, $"网络连接失败：{ex.HttpRequestError}"); }
        catch (IOException) { return Unknown(targets, "响应或请求冷却记录读写失败，未作库存判断"); }
        catch (UnauthorizedAccessException) { return Unknown(targets, "请求冷却记录无法保存，已停止本次查询"); }
        finally { gate.Release(); }
    }
    private string? ApplyThrottle(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (status is 403 or 541)
        {
            NextAllowed = DateTimeOffset.UtcNow.AddMinutes(15);
            return $"Apple 拦截请求（HTTP {status}），冷却 15 分钟；不能据此判断无货。";
        }
        if (status != 429) return null;
        var now = DateTimeOffset.UtcNow;
        var next = now.AddMinutes(2);
        // Parse raw headers: malformed Retry-After must not crash the monitor.
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var raw = values.FirstOrDefault()?.Trim();
            if (long.TryParse(raw, out var seconds) && seconds >= 0)
            {
                var remaining = (DateTimeOffset.MaxValue - now).TotalSeconds;
                next = seconds >= remaining ? DateTimeOffset.MaxValue : now.AddSeconds(Math.Max(120, seconds));
            }
            else if (DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var date) && date > next) next = date;
        }
        NextAllowed = next;
        return "Apple 限流（HTTP 429），已延后查询。";
    }
    public static IReadOnlyList<Result> Parse(byte[] raw, IReadOnlyList<Target> targets)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("head", out var head) && head.TryGetProperty("status", out var status) && status.ValueKind != JsonValueKind.Null && status.ToString() != "200") return Unknown(targets, "Apple 返回非成功状态：" + status);
            var body = root.GetProperty("body");
            if (body.TryGetProperty("errorMessage", out var error) && !string.IsNullOrWhiteSpace(error.ToString())) return Unknown(targets, "Apple 返回错误，请核对官网与商品状态");
            JsonElement stores;
            if (!body.TryGetProperty("stores", out stores) || stores.ValueKind != JsonValueKind.Array || stores.GetArrayLength() == 0)
                stores = body.GetProperty("content").GetProperty("pickupMessage").GetProperty("stores");
            if (stores.GetArrayLength() == 0) return Unknown(targets, "Apple 未返回门店，商品可能尚未开售或暂不可购买");
            var output = new List<Result>();
            foreach (var target in targets)
            {
                var matches = stores.EnumerateArray().Where(s => s.TryGetProperty("storeNumber", out var id) && id.GetString() == target.StoreNumber).ToArray();
                if (matches.Length != 1) { output.Add(New(target, StockKind.Unknown, "响应缺失目标门店或门店重复")); continue; }
                if (!matches[0].TryGetProperty("partsAvailability", out var parts) || !parts.TryGetProperty(target.PartNumber, out var part)) { output.Add(New(target, StockKind.Unknown, "响应缺少目标商品")); continue; }
                if (part.TryGetProperty("partNumber", out var number) && !string.IsNullOrWhiteSpace(number.GetString()) && number.GetString() != target.PartNumber) { output.Add(New(target, StockKind.Unknown, "响应商品编号不一致")); continue; }
                var display = part.TryGetProperty("pickupDisplay", out var d) ? d.GetString()?.Trim().ToLowerInvariant() : null;
                output.Add(display switch
                {
                    "available" => New(target, StockKind.InStock, "Apple 确认可到店取货，请前往官网确认并购买"),
                    "unavailable" => New(target, StockKind.OutOfStock, "当前不可到店取货"),
                    "ineligible" => New(target, StockKind.OutOfStock, "当前商品在此门店不支持到店取货"),
                    _ => New(target, StockKind.Unknown, "无法识别库存字段，未作无货判断")
                });
            }
            return output;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { return Unknown(targets, "响应结构变化或不是有效 JSON，未作库存判断"); }
    }
    private static Result New(Target t, StockKind kind, string detail) => new(t, kind, detail, DateTimeOffset.Now);
    public static IReadOnlyList<Result> Unknown(IReadOnlyList<Target> targets, string detail) => targets.Select(t => New(t, StockKind.Unknown, detail)).ToArray();
    public void Dispose() { http.Dispose(); gate.Dispose(); }
}
