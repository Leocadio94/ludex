using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.GameSavesManager;

/// <summary>
/// Settings UI, built in code rather than XAML so the plugin has no compiled resources to
/// ship or keep in sync with the settings model.
/// </summary>
public sealed class SettingsPanel : UserControl
{
    private readonly Settings _settings;
    private readonly IPublicAPI _api;

    public SettingsPanel(Settings settings, IPublicAPI api)
    {
        _settings = settings;
        _api = api;

        var stack = new StackPanel { Margin = new Thickness(10) };

        stack.Children.Add(Header("Stores to scan"));
        stack.Children.Add(Toggle("Steam", _settings.DetectSteam, v => _settings.DetectSteam = v));
        stack.Children.Add(Toggle("GOG", _settings.DetectGog, v => _settings.DetectGog = v));
        stack.Children.Add(Toggle("Epic Games", _settings.DetectEpic, v => _settings.DetectEpic = v));
        stack.Children.Add(Toggle("Xbox / Game Pass", _settings.DetectXbox, v => _settings.DetectXbox = v));

        stack.Children.Add(Header("Display"));
        stack.Children.Add(Choice(
            "Default order for \"gs\"",
            new[]
            {
                ("Most used first", GameSort.Usage),
                ("Alphabetical (A-Z)", GameSort.Alphabetical)
            },
            _settings.DefaultSort,
            v => _settings.DefaultSort = v));
        stack.Children.Add(Toggle(
            "Show games with no manifest entry (install folder only)",
            _settings.ShowUnmatchedGames,
            v => _settings.ShowUnmatchedGames = v));
        stack.Children.Add(Number("Maximum results", _settings.MaxResults, 5, 500, v => _settings.MaxResults = v));

        stack.Children.Add(Header("Caching"));
        stack.Children.Add(Number("Game database refresh interval (days)", _settings.ManifestTtlDays, 1, 90,
            v => _settings.ManifestTtlDays = v));
        stack.Children.Add(Number("Installed games cache lifetime (minutes)", _settings.InstalledGamesTtlMinutes, 1, 1440,
            v => _settings.InstalledGamesTtlMinutes = v));

        stack.Children.Add(new TextBlock
        {
            Text = "Use \"gs !refresh\" and \"gs !games\" to refresh immediately.",
            Opacity = 0.7,
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private void Save() => _api.SaveSettingJsonStorage<Settings>();

    private static TextBlock Header(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 14, 0, 6)
    };

    private CheckBox Toggle(string label, bool initial, Action<bool> set)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = initial,
            Margin = new Thickness(0, 4, 0, 4)
        };

        box.Checked += (_, _) => { set(true); Save(); };
        box.Unchecked += (_, _) => { set(false); Save(); };
        return box;
    }

    private Panel Choice<T>(string label, IReadOnlyList<(string Label, T Value)> options, T initial,
        Action<T> set)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4), LastChildFill = false };

        var combo = new ComboBox { Width = 200, VerticalContentAlignment = VerticalAlignment.Center };
        foreach (var option in options) combo.Items.Add(option.Label);
        combo.SelectedIndex = Math.Max(0, options.ToList().FindIndex(o => Equals(o.Value, initial)));

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
            {
                set(options[combo.SelectedIndex].Value);
                Save();
            }
        };

        DockPanel.SetDock(combo, Dock.Right);
        panel.Children.Add(combo);
        panel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });

        return panel;
    }

    private Panel Number(string label, int initial, int min, int max, Action<int> set)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 4, 0, 4), LastChildFill = false };
        var current = initial;

        var text = new TextBox
        {
            Text = initial.ToString(),
            Width = 70,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        text.LostFocus += (_, _) =>
        {
            if (int.TryParse(text.Text, out var value) && value >= min && value <= max)
            {
                current = value;
                set(value);
                Save();
            }
            else
            {
                // Reject out-of-range input instead of silently persisting nonsense.
                text.Text = current.ToString();
            }
        };

        DockPanel.SetDock(text, Dock.Right);
        panel.Children.Add(text);
        panel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });

        return panel;
    }
}
