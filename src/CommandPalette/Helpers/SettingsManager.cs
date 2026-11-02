// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Ludex.Core.Texts;
using CoreSettings = Ludex.Core.Settings;
using GameSort = Ludex.Core.GameSort;

namespace Ludex;

internal sealed partial class SettingsManager : JsonSettingsManager
{
    private const string Namespace = "Ludex";
    private const string SortUsage = "usage";
    private const string SortAlphabetical = "alpha";

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private readonly ChoiceSetSetting _defaultSort = new(
        Namespaced("DefaultSort"),
        T("Default order"),
        T("Most used first floats the games you open often to the top. Alphabetical is a fixed A-Z list."),
        [
            new ChoiceSetSetting.Choice(T("Most used first"), SortUsage),
            new ChoiceSetSetting.Choice(T("Alphabetical (A-Z)"), SortAlphabetical),
        ]);

    private readonly ToggleSetting _detectSteam = new(Namespaced("DetectSteam"), T("Scan Steam"), T("Applies on the next refresh."), true);
    private readonly ToggleSetting _detectGog = new(Namespaced("DetectGog"), T("Scan GOG"), T("Applies on the next refresh."), true);
    private readonly ToggleSetting _detectEpic = new(Namespaced("DetectEpic"), T("Scan Epic Games"), T("Applies on the next refresh."), true);
    private readonly ToggleSetting _detectXbox = new(Namespaced("DetectXbox"), T("Scan Xbox / Game Pass"), T("Applies on the next refresh."), true);

    private readonly ToggleSetting _showUnmatched = new(
        Namespaced("ShowUnmatchedGames"),
        T("Show games with no manifest entry"),
        T("List installed games the database does not know about, with just their install folder."),
        true);

    private readonly TextSetting _manifestTtlDays = new(
        Namespaced("ManifestTtlDays"),
        T("Game database refresh interval (days)"),
        T("How often the Ludusavi manifest is re-downloaded automatically."),
        "7");

    private readonly TextSetting _installedTtlMinutes = new(
        Namespaced("InstalledGamesTtlMinutes"),
        T("Installed games cache lifetime (minutes)"),
        T("How long the detected-games list is reused before re-scanning the stores."),
        "60");

    private readonly TextSetting _maxResults = new(
        Namespaced("MaxResults"),
        T("Maximum search results"),
        T("Cap on how many games \"Search all games\" shows at once."),
        "50");

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Ludex");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        _defaultSort.Value = SortUsage;

        Settings.Add(_defaultSort);
        Settings.Add(_detectSteam);
        Settings.Add(_detectGog);
        Settings.Add(_detectEpic);
        Settings.Add(_detectXbox);
        Settings.Add(_showUnmatched);
        Settings.Add(_manifestTtlDays);
        Settings.Add(_installedTtlMinutes);
        Settings.Add(_maxResults);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    /// <summary>Copies the current values into the settings object Ludex.Core reads.</summary>
    public void ApplyTo(CoreSettings core)
    {
        core.DefaultSort = _defaultSort.Value == SortAlphabetical ? GameSort.Alphabetical : GameSort.Usage;
        core.DetectSteam = _detectSteam.Value;
        core.DetectGog = _detectGog.Value;
        core.DetectEpic = _detectEpic.Value;
        core.DetectXbox = _detectXbox.Value;
        core.ShowUnmatchedGames = _showUnmatched.Value;
        core.ManifestTtlDays = ParseInt(_manifestTtlDays.Value, 7, 1, 365);
        core.InstalledGamesTtlMinutes = ParseInt(_installedTtlMinutes.Value, 60, 1, 10080);
        core.MaxResults = ParseInt(_maxResults.Value, 50, 5, 500);
    }

    private static int ParseInt(string? text, int fallback, int min, int max) =>
        int.TryParse(text, out var value) ? Math.Clamp(value, min, max) : fallback;
}
