using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VolumeMixer;

/// <summary>
/// A simple JSON-backed settings store.
/// The key is the process name (e.g. "spotify"), so settings survive
/// an application restart.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _path;
    private readonly Dictionary<string, AppSettings> _items;

    public SettingsStore(string path)
    {
        _path = path;
        _items = Load(path);
    }

    public static string GetDefaultPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VolumeMixer",
            "settings.json");
    }

    public bool? GetMuted(string key)
    {
        return _items.TryGetValue(key, out AppSettings? s) ? s.Muted : null;
    }

    public string? GetCustomName(string key)
    {
        return _items.TryGetValue(key, out AppSettings? s) ? s.CustomName : null;
    }

    public void SetMuted(string key, bool muted)
    {
        GetOrCreate(key).Muted = muted;
        Save();
    }

    public void SetCustomName(string key, string name)
    {
        GetOrCreate(key).CustomName = name;
        Save();
    }

    public void ClearCustomName(string key)
    {
        if (_items.TryGetValue(key, out AppSettings? s))
        {
            s.CustomName = null;
            Save();
        }
    }

    private AppSettings GetOrCreate(string key)
    {
        if (!_items.TryGetValue(key, out AppSettings? s))
        {
            s = new AppSettings();
            _items[key] = s;
        }

        return s;
    }

    private static Dictionary<string, AppSettings> Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Dictionary<string, AppSettings>>(json)
                       ?? new Dictionary<string, AppSettings>();
            }
        }
        catch
        {
            // corrupted settings file — start from a clean state
        }

        return new Dictionary<string, AppSettings>();
    }

    private void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(
                _items,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // a failed save must not crash the application
        }
    }

    public sealed class AppSettings
    {
        public string? CustomName { get; set; }
        public bool? Muted { get; set; }
    }
}
