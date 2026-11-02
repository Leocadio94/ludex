using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Ludex.Core.Texts;

namespace Ludex;

/// <summary>
/// Runs a refresh and shows its progress in the list itself. Status messages and toasts raised
/// through ExtensionHost are not shown when the command runs from the home page, so the page
/// is the one place feedback is guaranteed to be visible.
/// </summary>
internal sealed partial class RefreshPage : ListPage
{
    private enum RefreshState
    {
        Idle,
        Running,
        Done,
    }

    private readonly LudexApp _app;
    private readonly bool _manifest;
    private readonly string _caption;
    private readonly object _gate = new();

    private RefreshState _state;
    private int _percent;

    public RefreshPage(LudexApp app, bool manifest)
    {
        _app = app;
        _manifest = manifest;
        _caption = T(manifest ? "Downloading the game database" : "Re-detecting installed games");

        Icon = manifest ? Icons.Download : Icons.Refresh;
        Title = T(manifest ? "Refresh game database" : "Refresh installed games");
        Name = T("Open");
    }

    public override IListItem[] GetItems()
    {
        var catalog = _app.Catalog;
        IsLoading = _state == RefreshState.Running;

        switch (_state)
        {
            case RefreshState.Running:
                return [new ListItem(new NoOpCommand())
                {
                    Title = _caption + "... " + _percent.ToString(CultureInfo.InvariantCulture) + "%",
                    Subtitle = catalog.Status,
                    Icon = Icon,
                }];

            case RefreshState.Done:
                return [new ListItem(StartCommand())
                {
                    Title = catalog.Error is null
                        ? F("Ready: {0} games", catalog.Games.Count)
                        : T("Refresh failed"),
                    Subtitle = catalog.Error ?? T("Press Enter to refresh again"),
                    Icon = catalog.Error is null ? Icon : Icons.Missing,
                }];

            default:
                return [new ListItem(StartCommand())
                {
                    Title = T(_manifest ? "Download the game database now" : "Re-scan the installed games now"),
                    Subtitle = T(_manifest
                        ? "Re-downloads the Ludusavi manifest (~16 MB), then re-detects games"
                        : "Looks for newly installed or removed games in Steam, GOG, Epic and Xbox"),
                    Icon = Icon,
                }];
        }
    }

    private AnonymousCommand StartCommand() => new(Start)
    {
        Name = T("Refresh now"),
        Icon = Icon,
        Result = CommandResult.KeepOpen(),
    };

    private void Start()
    {
        lock (_gate)
        {
            if (_state == RefreshState.Running) return;
            _state = RefreshState.Running;
            _percent = 0;
        }

        _app.Catalog.Begin(_caption);
        RaiseItemsChanged();
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            await _app.Catalog.StartLoad(_manifest, true, OnProgress).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            ExtensionHost.LogMessage($"[Ludex] Refresh failed: {e}");
        }

        _state = RefreshState.Done;
        RaiseItemsChanged();
    }

    private void OnProgress(double percent)
    {
        var rounded = (int)percent;
        if (rounded == _percent) return;

        _percent = rounded;
        RaiseItemsChanged();
    }
}
