using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludex.Core;
using Microsoft.CommandPalette.Extensions.Toolkit;
using CoreSettings = Ludex.Core.Settings;

namespace Ludex;

/// <summary>Everything the pages share: settings, the loaded catalog and the usage counts.</summary>
internal sealed class LudexApp
{
    private static readonly HttpClient Http = new();

    private readonly string _ticksFile;

    public LudexApp()
    {
        var dataDir = Utilities.BaseSettingsPath("Ludex");
        var cacheDir = Path.Combine(dataDir, "cache");
        Directory.CreateDirectory(cacheDir);

        _ticksFile = Path.Combine(cacheDir, "manifest-updated.txt");

        SettingsManager = new SettingsManager();
        SettingsManager.ApplyTo(Core);
        Core.ManifestUpdatedUtcTicks = ReadTicks();

        SettingsManager.Settings.SettingsChanged += (_, _) =>
        {
            SettingsManager.ApplyTo(Core);
            SettingsApplied?.Invoke();
        };

        var manifest = new ManifestStore(cacheDir, Core, DownloadAsync, WriteTicks);
        var detector = new GameDetector(cacheDir, Core, Log);

        Catalog = new GameCatalog(manifest, detector, Core, Log);
        Usage = new UsageTracker(Path.Combine(dataDir, "usage.json"));

        Catalog.StartLoad(false, false);
    }

    public SettingsManager SettingsManager { get; }
    public CoreSettings Core { get; } = new();
    public GameCatalog Catalog { get; }
    public UsageTracker Usage { get; }

    /// <summary>Raised after the user edits a setting and <see cref="Core"/> has been updated.</summary>
    public event Action? SettingsApplied;

    private static void Log(string message, Exception e) =>
        ExtensionHost.LogMessage($"[Ludex] {message}: {e}");

    private long ReadTicks() =>
        long.TryParse(File.Exists(_ticksFile) ? File.ReadAllText(_ticksFile) : null, out var ticks) ? ticks : 0;

    private void WriteTicks() => File.WriteAllText(_ticksFile, Core.ManifestUpdatedUtcTicks.ToString(CultureInfo.InvariantCulture));

    private static async Task DownloadAsync(string url, string destination, Action<double>? report,
        CancellationToken token)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var target = File.Create(destination);

        var buffer = new byte[81920];
        long received = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            received += read;
            if (total > 0) report?.Invoke(received * 100.0 / total.Value);
        }
    }
}
