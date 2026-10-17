using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Stock.WinUI;

internal static class ProductImages
{
    internal sealed record Entry(string PartNumber, string File, string Color, int Width, int Height, int[] Bounds)
    {
        public string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Products", File);
    }
    private static readonly Dictionary<string, Entry> entries = Load();
    private static Dictionary<string, Entry> Load()
    {
        try
        {
            return (JsonSerializer.Deserialize<Entry[]>(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Products", "index.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? []).ToDictionary(e => e.PartNumber);
        }
        catch (Exception ex) { App.Log(ex); return []; }
    }
    public static Entry? Find(string? partNumber) => partNumber is not null && entries.TryGetValue(partNumber, out var entry) && System.IO.File.Exists(entry.Path) ? entry : null;
    public static FrameworkElement Picture(string? partNumber, string description, double width, double height)
    {
        var host = new Grid { Width = width, Height = height, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(host, description + " 商品图");
        AutomationProperties.SetAutomationId(host, "ProductPicture");
        if (Find(partNumber) is not { } e)
        {
            host.Children.Add(new TextBlock { Text = "暂无图片", FontSize = 12, Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            return host;
        }
        // Fit the visible product, rather than the transparent padding of the original.
        var b = e.Bounds; var scale = Math.Min(width / (b[2] - b[0]), height / (b[3] - b[1]));
        var canvas = new Canvas { Width = width, Height = height, Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, width, height) } };
        var image = new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/Products/" + e.File)), Width = e.Width * scale, Height = e.Height * scale, Stretch = Stretch.Fill };
        Canvas.SetLeft(image, (width - (b[2] - b[0]) * scale) / 2 - b[0] * scale);
        Canvas.SetTop(image, (height - (b[3] - b[1]) * scale) / 2 - b[1] * scale);
        canvas.Children.Add(image); host.Children.Add(canvas); return host;
    }
}
