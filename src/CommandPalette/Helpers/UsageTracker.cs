using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ludex;

[JsonSerializable(typeof(Dictionary<string, int>))]
internal sealed partial class UsageJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Counts how often each game is launched or has a folder opened. Command Palette has no
/// usage ranking for items inside an extension page, so the "most used first" order is kept here.
/// </summary>
internal sealed class UsageTracker
{
    private readonly string _file;
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _counts;

    public UsageTracker(string file)
    {
        _file = file;
        _counts = Load();
    }

    public event Action? Changed;

    public int CountFor(string slug)
    {
        lock (_gate) return _counts.GetValueOrDefault(slug);
    }

    public void Record(string slug)
    {
        lock (_gate)
        {
            _counts[slug] = _counts.GetValueOrDefault(slug) + 1;
            Save();
        }

        Changed?.Invoke();
    }

    private Dictionary<string, int> Load()
    {
        try
        {
            if (!File.Exists(_file)) return new();

            using var stream = File.OpenRead(_file);
            return JsonSerializer.Deserialize(stream, UsageJsonContext.Default.DictionaryStringInt32) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            using var stream = File.Create(_file);
            JsonSerializer.Serialize(stream, _counts, UsageJsonContext.Default.DictionaryStringInt32);
        }
        catch
        {
            // Losing a usage count is not worth surfacing to the user.
        }
    }
}
