using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SeniorDesktop;

public partial class StartMenuPickerWindow : Window
{
    readonly List<StartMenuItem> _all;
    public StartMenuItem Selected { get; private set; }

    public StartMenuPickerWindow()
    {
        InitializeComponent();
        _all = StartMenuScanner.Scan();
        List.ItemsSource = _all;
        Loaded += (_, _) => SearchBox.Focus();
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = SearchBox.Text.Trim();
        List.ItemsSource = q.Length == 0
            ? _all
            : _all.Where(i => i.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
        if (List.Items.Count > 0) List.SelectedIndex = 0;
    }

    void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (List.SelectedItem != null) Ok_Click(sender, e);
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        Selected = List.SelectedItem as StartMenuItem;
        if (Selected != null) DialogResult = true;
    }
}
