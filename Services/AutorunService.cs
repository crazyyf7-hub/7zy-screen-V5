using Microsoft.Win32;
using SevenzyX.Models;
using System.Text.Json;

namespace SevenzyX.Services;

public sealed class AutorunService
{
    private sealed class DisabledRecord
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public string Location { get; set; } = "";
    }

    private readonly string _storePath;
    private List<DisabledRecord> _disabled = new();

    public AutorunService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "7zyX");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "autoruns-disabled.json");
        Load();
    }

    public IReadOnlyList<AutorunEntry> GetEntries()
    {
        var list = new List<AutorunEntry>();
        ReadRegistry(list, Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run");
        ReadRegistry(list, Registry.LocalMachine, "HKLM", @"Software\Microsoft\Windows\CurrentVersion\Run");

        foreach (var item in _disabled)
        {
            if (list.All(x => !string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new AutorunEntry
                {
                    Id = item.Id,
                    Name = item.Name,
                    Command = item.Command,
                    Location = item.Location,
                    IsEnabled = false
                });
            }
        }

        return list.OrderByDescending(x => x.IsEnabled).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool Disable(string id, out string error)
    {
        error = "";
        var entry = GetEntries().FirstOrDefault(x => x.Id == id);
        if (entry is null) { error = "Entrada não encontrada."; return false; }
        if (!entry.IsEnabled) return true;

        if (!TryParseLocation(entry.Location, out var hive, out var path, out var hiveName))
        {
            error = "Local de inicialização não suportado.";
            return false;
        }

        try
        {
            using var key = hive.OpenSubKey(path, writable: true);
            if (key is null) { error = "Chave de inicialização não encontrada."; return false; }

            var value = key.GetValue(entry.Name)?.ToString();
            if (value is null) { error = "Entrada não encontrada."; return false; }

            _disabled.RemoveAll(x => x.Id == entry.Id);
            _disabled.Add(new DisabledRecord
            {
                Id = entry.Id,
                Name = entry.Name,
                Command = value,
                Location = $"{hiveName}\\{path}"
            });

            key.DeleteValue(entry.Name, false);
            Save();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool Restore(string id, out string error)
    {
        error = "";
        var item = _disabled.FirstOrDefault(x => x.Id == id);
        if (item is null) { error = "Backup dessa entrada não foi encontrado."; return false; }

        if (!TryParseLocation(item.Location, out var hive, out var path, out _))
        {
            error = "Local de inicialização não suportado.";
            return false;
        }

        try
        {
            using var key = hive.CreateSubKey(path, true);
            key.SetValue(item.Name, item.Command, RegistryValueKind.String);
            _disabled.Remove(item);
            Save();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void ReadRegistry(List<AutorunEntry> list, RegistryKey hive, string hiveName, string path)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            if (key is null) return;

            foreach (var name in key.GetValueNames())
            {
                var command = key.GetValue(name)?.ToString() ?? "";
                var location = $"{hiveName}\\{path}";
                list.Add(new AutorunEntry
                {
                    Id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(location + "|" + name)))[..16],
                    Name = name,
                    Command = command,
                    Location = location,
                    IsEnabled = true
                });
            }
        }
        catch { }
    }

    private static bool TryParseLocation(string location, out RegistryKey hive, out string path, out string hiveName)
    {
        if (location.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = Registry.CurrentUser;
            hiveName = "HKCU";
            path = location[5..];
            return true;
        }

        if (location.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = Registry.LocalMachine;
            hiveName = "HKLM";
            path = location[5..];
            return true;
        }

        hive = Registry.CurrentUser;
        hiveName = "";
        path = "";
        return false;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_storePath))
                _disabled = JsonSerializer.Deserialize<List<DisabledRecord>>(File.ReadAllText(_storePath)) ?? new();
        }
        catch
        {
            _disabled = new();
        }
    }

    private void Save()
    {
        var temp = _storePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_disabled, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _storePath, true);
    }
}
