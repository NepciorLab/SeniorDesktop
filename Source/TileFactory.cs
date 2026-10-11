using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SeniorDesktop.Localization;

namespace SeniorDesktop;

public class TileView
{
    public Button Button;
    public TextBlock Label;
    public TileConfig Tile;
}

/// <summary>Builds a tile button; used on the desktop and in the preview in the settings.</summary>
public static class TileFactory
{
    public static TileView Create(TileConfig t, AppConfig c)
    {
        double size = Math.Clamp(t.Size >= 80 ? t.Size : c.TileSize, 80, 700);
        var bg = ColorUtil.Parse(t.Color, Color.FromRgb(0x1F, 0x6F, 0xEB));
        var fg = new SolidColorBrush(ColorUtil.TextOn(bg));
        fg.Freeze();

        double font = Math.Min(c.LabelFontSize, size * 0.14);
        double labelH = font * 1.4 * 2;
        double plate = Math.Clamp(size - 10 - labelH - 26, 40, size * 0.62);

        // The white plate keeps the icon readable on any tile colour.
        var plateBorder = new Border
        {
            Width = plate,
            Height = plate,
            CornerRadius = new CornerRadius(plate * 0.16),
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        UIElement icon = null;
        if (string.IsNullOrWhiteSpace(t.Emoji))
        {
            var src = IconLoader.Load(t, 256);
            if (src != null)
            {
                var img = new Image { Source = src, Margin = new Thickness(plate * 0.08), Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                icon = img;
            }
        }
        icon ??= new TextBlock
        {
            Text = !string.IsNullOrWhiteSpace(t.Emoji) ? t.Emoji
                 : (t.Target ?? "").StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "🌐" : "▶",
            FontFamily = new FontFamily("Segoe UI Emoji"),
            FontSize = plate * 0.6,
            Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        plateBorder.Child = icon;

        var label = new TextBlock
        {
            Text = t.Title,
            FontSize = font,
            FontWeight = FontWeights.Bold,
            Foreground = fg,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = labelH,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var labelHost = new Border { Height = labelH, Margin = new Thickness(6, 0, 6, 6), Child = label };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(plateBorder);
        Grid.SetRow(labelHost, 1);
        grid.Children.Add(labelHost);

        var button = new Button
        {
            Width = size,
            Height = size,
            Margin = new Thickness(Math.Max(0, c.TileSpacing) / 2.0),
            Background = new SolidColorBrush(bg),
            BorderBrush = fg,
            Content = grid,
            Tag = t,
            Style = (Style)Application.Current.FindResource("TileButton"),
        };
        System.Windows.Automation.AutomationProperties.SetName(button, t.Title);
        return new TileView { Button = button, Label = label, Tile = t };
    }
}

/// <summary>A large message window for visually impaired users.</summary>
public static class BigMessage
{
    public static void Show(Window owner, string text, string title = null)
    {
        var w = new Window
        {
            Title = title ?? Loc.T("Common.Warning"),
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Owner = owner,
            Topmost = true,
            ShowInTaskbar = false,
        };
        Theming.Track(w);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = text, FontSize = 30, TextWrapping = TextWrapping.Wrap, MaxWidth = 700, Margin = new Thickness(32) });
        var ok = new Button { Content = Loc.T("Common.OK"), FontSize = 30, Padding = new Thickness(70, 8, 70, 8), Margin = new Thickness(32, 0, 32, 32), IsDefault = true, HorizontalAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => w.Close();
        panel.Children.Add(ok);
        w.Content = panel;
        w.ShowDialog();
    }
}
