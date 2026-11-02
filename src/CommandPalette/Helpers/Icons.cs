using System.IO;
using Ludex.Core;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Ludex;

internal static class Icons
{
    public static readonly IconInfo App = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    public static readonly IconInfo Play = new("");
    public static readonly IconInfo Folder = new("");
    public static readonly IconInfo Config = new("");
    public static readonly IconInfo Install = new("");
    public static readonly IconInfo Missing = new("");
    public static readonly IconInfo Refresh = new("");
    public static readonly IconInfo Download = new("");
    public static readonly IconInfo Search = new("");

    /// <summary>The store's own artwork when the detector found any, otherwise the app icon.</summary>
    public static IconInfo ForGame(string? iconPath, bool matched)
    {
        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath)) return new IconInfo(iconPath);
        return matched ? App : Missing;
    }

    public static IconInfo ForFolder(FolderEntry entry)
    {
        if (!entry.Exists) return Missing;
        if (entry.IsInstall) return Install;
        return entry.Tags == PathTags.Config ? Config : Folder;
    }
}
