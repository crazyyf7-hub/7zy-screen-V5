using Microsoft.Win32;
using System.Text.Json;

namespace SevenzyX.Services;

public sealed class StateBackupService
{
    private sealed class SavedValue
    {
        public bool Exists { get; set; }
        public string Kind { get; set; } = nameof(RegistryValueKind.String);
        public string? Value { get; set; }
    }

    private readonly string _path;
    private readonly Dictionary<string, SavedValue> _values;

    public StateBackupService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "7zyX");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "state-backup.json");

        try
        {
            _values = File.Exists(_path)
                ? JsonSerializer.Deserialize<Dictionary<string, SavedValue>>(File.ReadAllText(_path)) ?? new()
                : new();
        }
        catch
        {
            _values = new();
        }
    }

    public void Capture(RegistryKey hive, string hiveName, string path, string name)
    {
        var keyId = Key(hiveName, path, name);
        if (_values.ContainsKey(keyId)) return;

        try
        {
            using var key = hive.OpenSubKey(path);
            var names = key?.GetValueNames() ?? Array.Empty<string>();
            var exists = names.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                _values[keyId] = new SavedValue { Exists = false };
            }
            else
            {
                var kind = key!.GetValueKind(name);
                var value = key.GetValue(name);
                _values[keyId] = new SavedValue
                {
                    Exists = true,
                    Kind = kind.ToString(),
                    Value = value?.ToString()
                };
            }

            Save();
        }
        catch
        {
            // A failed snapshot must not crash the optimizer.
        }
    }

    public bool Restore(RegistryKey hive, string hiveName, string path, string name)
    {
        var keyId = Key(hiveName, path, name);
        if (!_values.TryGetValue(keyId, out var saved)) return false;

        using var key = hive.CreateSubKey(path, true);
        if (!saved.Exists)
        {
            key.DeleteValue(name, false);
            return true;
        }

        if (!Enum.TryParse(saved.Kind, out RegistryValueKind kind))
            kind = RegistryValueKind.String;

        object value = kind switch
        {
            RegistryValueKind.DWord => int.TryParse(saved.Value, out var i) ? i : 0,
            RegistryValueKind.QWord => long.TryParse(saved.Value, out var l) ? l : 0L,
            _ => saved.Value ?? string.Empty
        };

        key.SetValue(name, value, kind);
        return true;
    }

    private void Save()
    {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, true);
    }

    private static string Key(string hive, string path, string name) => $"{hive}|{path}|{name}";
}
