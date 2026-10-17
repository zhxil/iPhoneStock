using System.Text.Json;
using System.Text.RegularExpressions;

namespace Stock.Core;

public enum StockKind { Pending, InStock, OutOfStock, Unknown }
public record Target(string StoreNumber, string StoreTitle, string PartNumber, string ProductName);
public record Result(Target Target, StockKind Kind, string Detail, DateTimeOffset CheckedAt);
public record Product(string PartNumber, string Family, string Capacity, string Color, string Title)
{
    public override string ToString() => Title;
}
public record Store(string Number, string City, string Name, string Location)
{
    public string Title => $"{City} · {Name}";
    public override string ToString() => Title;
}
public record Settings
{
    public List<Target> Targets { get; init; } = [new("R557", "青岛 · 青岛万象城", "MK2P4CH/A", "iPhone Duo 512GB 星光白色")];
    public int IntervalSeconds { get; init; } = 60;
    public bool SoundEnabled { get; init; } = true;
    public bool TaskbarEnabled { get; init; } = true;
    public bool StartupEnabled { get; init; }
    public string BarkUrl { get; init; } = "";
    public string FeishuUrl { get; init; } = "";
}

public sealed class Catalog
{
    public IReadOnlyList<Product> Products { get; }
    public IReadOnlyList<Store> Stores { get; }
    public Catalog()
    {
        using var products = Read("products.json");
        var list = new List<Product>();
        foreach (var family in products.RootElement.EnumerateArray().Where(f => f.GetProperty("category").GetString() == "iphone"))
        {
            var data = family.GetProperty("data");
            var colors = data.GetProperty("displayValues").GetProperty("dimensionColor");
            foreach (var p in data.GetProperty("products").EnumerateArray())
            {
                var type = p.GetProperty("familyType").GetString()!;
                var name = type switch { "iphoneduo" => "iPhone Duo", "iphone18pro" => "iPhone 18 Pro", "iphone18promax" => "iPhone 18 Pro Max", "iphoneair" => "iPhone Air", _ => Regex.Replace(type, "^iphone", "iPhone ") };
                var colorKey = p.GetProperty("dimensionColor").GetString()!;
                var color = colors.TryGetProperty(colorKey, out var c) ? c.GetProperty("value").GetString()! : colorKey;
                color = System.Net.WebUtility.HtmlDecode(Regex.Replace(color, "<[^>]+>", ""));
                var capacity = p.GetProperty("dimensionCapacity").GetString()!.ToUpperInvariant();
                list.Add(new(p.GetProperty("partNumber").GetString()!, name, capacity, color, $"{name} {capacity} {color}"));
            }
        }
        Products = list.DistinctBy(p => p.PartNumber).ToArray();
        using var stores = Read("stores.json");
        Stores = stores.RootElement.EnumerateArray().First(r => r.GetProperty("locale").GetString() == "zh_CN")
            .GetProperty("state").EnumerateArray().SelectMany(s => s.GetProperty("store").EnumerateArray()).Select(s =>
            {
                var address = s.GetProperty("address");
                return new Store(s.GetProperty("id").GetString()!, address.GetProperty("city").GetString()!, s.GetProperty("name").GetString()!, $"{address.GetProperty("stateName").GetString()} {address.GetProperty("city").GetString()}");
            }).DistinctBy(s => s.Number).OrderBy(s => s.City).ToArray();
    }
    private static JsonDocument Read(string name) => JsonDocument.Parse(typeof(Catalog).Assembly.GetManifestResourceStream($"Stock.Core.Data.{name}")!);
}
