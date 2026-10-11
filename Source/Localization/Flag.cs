using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SeniorDesktop.Localization;

/// <summary>
/// A small vector flag. <c>&lt;loc:Flag Code="gb" Width="30" Height="20"/&gt;</c>.
/// Built-in flags: pl, gb, de. Any other code looks for an image <c>Languages\flag-&lt;code&gt;.png</c>
/// (exe folder or %APPDATA%\SeniorDesktop\Languages) and otherwise shows a neutral tile with the code.
/// </summary>
public sealed class Flag : Border
{
    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(
        nameof(Code), typeof(string), typeof(Flag),
        new PropertyMetadata("", (d, _) => ((Flag)d).Rebuild()));

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public Flag()
    {
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(2);
        SnapsToDevicePixels = true;
    }

    void Rebuild()
    {
        var code = (Code ?? "").Trim().ToLowerInvariant();
        UIElement content = code switch
        {
            "pl" => Pl(),
            "gb" or "uk" => Gb(),
            "de" => De(),
            _ => External(code) ?? Generic(code),
        };
        Child = new Viewbox { Stretch = Stretch.Fill, Child = content };
    }

    // All built-in flags are drawn on a 60 x 40 canvas.
    static Canvas NewCanvas() => new() { Width = 60, Height = 40, ClipToBounds = true };

    static Brush B(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

    static Rectangle Rect(double x, double y, double w, double h, string hex) =>
        new() { Width = w, Height = h, Fill = B(hex), Margin = new Thickness(x, y, 0, 0) };

    static System.Windows.Shapes.Path Line(string data, string hex, double thickness) =>
        new() { Data = Geometry.Parse(data), Stroke = B(hex), StrokeThickness = thickness };

    static UIElement Pl()
    {
        var c = NewCanvas();
        c.Children.Add(Rect(0, 0, 60, 20, "#FFFFFF"));
        c.Children.Add(Rect(0, 20, 60, 20, "#DC143C"));
        return c;
    }

    static UIElement De()
    {
        var c = NewCanvas();
        c.Children.Add(Rect(0, 0, 60, 13.4, "#000000"));
        c.Children.Add(Rect(0, 13.3, 60, 13.4, "#DD0000"));
        c.Children.Add(Rect(0, 26.6, 60, 13.4, "#FFCE00"));
        return c;
    }

    static UIElement Gb()
    {
        var c = NewCanvas();
        c.Children.Add(Rect(0, 0, 60, 40, "#012169"));
        c.Children.Add(Line("M0,0 L60,40 M60,0 L0,40", "#FFFFFF", 8));
        c.Children.Add(Line("M0,0 L60,40 M60,0 L0,40", "#C8102E", 3));
        c.Children.Add(Line("M30,0 L30,40 M0,20 L60,20", "#FFFFFF", 13));
        c.Children.Add(Line("M30,0 L30,40 M0,20 L60,20", "#C8102E", 8));
        return c;
    }

    static UIElement External(string code)
    {
        if (code.Length == 0) return null;
        foreach (var dir in Loc.ExternalFolders())
        {
            var file = System.IO.Path.Combine(dir, $"flag-{code}.png");
            if (!File.Exists(file)) continue;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(file);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return new Image { Source = bmp, Stretch = Stretch.Fill, Width = 60, Height = 40 };
            }
            catch { }
        }
        return null;
    }

    static UIElement Generic(string code)
    {
        var c = NewCanvas();
        c.Children.Add(Rect(0, 0, 60, 40, "#607D8B"));
        c.Children.Add(new TextBlock
        {
            Text = code.ToUpperInvariant(),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 20,
            Width = 60,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 7, 0, 0),
        });
        return c;
    }
}
