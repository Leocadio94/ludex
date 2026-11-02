using System.Diagnostics;
using System.IO;

namespace Ludex.Core;

/// <summary>
/// Starts a game from whatever handle its store gave us — a protocol URI for Steam and Epic,
/// a shell app-folder path for Xbox, or a plain executable for GOG.
/// </summary>
public static class GameLauncher
{
    public static void Launch(string target, string? installPath)
    {
        // shell:appsFolder is understood by Explorer, not by ShellExecute directly.
        if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = true })?.Dispose();
            return;
        }

        var info = new ProcessStartInfo(target) { UseShellExecute = true };

        // Games launched by path usually expect their own folder as the working directory.
        if (!target.Contains("://", StringComparison.Ordinal))
        {
            var workingDirectory = installPath ?? Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
                info.WorkingDirectory = workingDirectory;
        }

        Process.Start(info)?.Dispose();
    }

    /// <summary>Short description of how the game will start, for the result subtitle.</summary>
    public static string Describe(GameStore store, string target) => store switch
    {
        GameStore.Steam => Texts.T("Launch through Steam"),
        GameStore.Epic => Texts.T("Launch through the Epic Games Launcher"),
        GameStore.Xbox => Texts.T("Launch through the Xbox app"),
        GameStore.Gog => target,
        _ => target
    };
}
