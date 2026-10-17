using System.Net.Http.Json;
using System.Text.Json;

namespace Stock.Core;

public sealed class PushService : IDisposable
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };
    public async Task<IReadOnlyList<string>> SendAsync(Settings settings, string title, string body)
    {
        var errors = new List<string>();
        foreach (var (url, feishu) in new[] { (settings.BarkUrl, false), (settings.FeishuUrl, true) })
        {
            if (string.IsNullOrWhiteSpace(url)) continue;
            try
            {
                SettingsStore.ValidatePush(url, feishu);
                object payload = feishu ? new { msg_type = "text", content = new { text = title + "\n" + body } } : new { title, body, group = "iPhone 库存", url = "https://www.apple.com.cn/shop/buy-iphone/iphone-duo" };
                using var response = await http.PostAsJsonAsync(url.Trim(), payload);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(data);
                var code = doc.RootElement.GetProperty(feishu ? "code" : "code");
                if (code.GetInt32() != (feishu ? 0 : 200)) errors.Add(feishu ? "飞书拒绝推送，请核对机器人设置" : "Bark 拒绝推送，请核对地址");
            }
            catch { errors.Add(feishu ? "飞书推送失败，请检查网络或地址" : "Bark 推送失败，请检查网络或地址"); }
        }
        return errors;
    }
    public void Dispose() => http.Dispose();
}
