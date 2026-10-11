using System.Windows.Data;
using System.Windows.Markup;

namespace SeniorDesktop.Localization;

/// <summary>
/// XAML markup extension: <c>Text="{loc:Loc Settings.Title}"</c>.
/// Binds to <see cref="Loc"/> so the text updates immediately when the language changes.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; }

    public LocExtension() { }
    public LocExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
