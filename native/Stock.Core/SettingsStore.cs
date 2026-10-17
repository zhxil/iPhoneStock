using System.Text.Json;

namespace Stock.Core;

public sealed class SettingsStore(string path)
{
    public static string DefaultDirectory => Environment.GetEnvironmentVariable("IPHONE_STOCK_DATA") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QingdaoIphoneStock");
    public string Path { get; } = path;
    public string? Warning { get; private set; }
    private bool canWrite = true;
    private readonly object gate = new();
    public Settings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new();
            if (new FileInfo(Path).Length > 1_048_576) throw new InvalidDataException("配置超过 1 MB");
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path)) ?? throw new InvalidDataException("配置为空");
            Validate(settings);
            return settings;
        }
        catch (Exception ex)
        {
            canWrite = false;
            var backup = Path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            try { File.Copy(Path, backup); Warning = $"配置读取失败，原文件保留并备份至 {backup}。本次不覆盖配置。{ex.Message}"; }
            catch { Warning = $"配置读取失败，原文件保留，本次不覆盖配置。{ex.Message}"; }
            return new();
        }
    }
    public void Save(Settings settings)
    {
        lock (gate)
        {
            if (!canWrite) throw new IOException("配置受保护，请先修复或移走原配置，再重新启动。");
            Validate(settings);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            var temp = Path + $".{Guid.NewGuid():N}.tmp";
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { JsonSerializer.Serialize(file, settings, new JsonSerializerOptions { WriteIndented = true }); file.Flush(true); }
                File.Move(temp, Path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    private static void Validate(Settings s)
    {
        if (s.Targets is null || s.Targets.Count > 100 || s.Targets.Any(t => t is null || string.IsNullOrWhiteSpace(t.PartNumber) || string.IsNullOrWhiteSpace(t.StoreNumber))) throw new InvalidDataException("监控目标无效");
        if (s.IntervalSeconds < 60 || s.IntervalSeconds > 3600) throw new InvalidDataException("查询间隔应为 60–3600 秒");
        ValidatePush(s.BarkUrl, false); ValidatePush(s.FeishuUrl, true);
    }
    public static void ValidatePush(string url, bool feishu)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment)) throw new InvalidDataException("推送地址必须是 HTTPS URL");
        if (feishu && (uri.Host != "open.feishu.cn" || !uri.AbsolutePath.StartsWith("/open-apis/bot/v2/hook/", StringComparison.Ordinal))) throw new InvalidDataException("请填写飞书群机器人 webhook 地址");
    }
}
