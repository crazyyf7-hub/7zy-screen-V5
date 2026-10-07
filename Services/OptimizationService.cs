using Microsoft.Win32;
using System.Diagnostics;
using SevenzyX.Models;

namespace SevenzyX.Services;

public sealed class OptimizationService
{
    public IReadOnlyList<TweakDefinition> Tweaks { get; } = new List<TweakDefinition>
    {
        new() { Id="mouse_acceleration", Title="Mouse acceleration", Description="Desativa aceleração do mouse para resposta mais consistente.", Risk=TweakRisk.Safe },
        new() { Id="driver_updates", Title="Automatic driver updates", Description="Impede a busca automática de drivers pelo Windows.", Risk=TweakRisk.Moderate },
        new() { Id="notifications", Title="Windows notifications", Description="Desativa notificações toast do usuário atual.", Risk=TweakRisk.Safe },
        new() { Id="uwp_background", Title="UWP apps in background", Description="Reduz execução de aplicativos UWP em segundo plano.", Risk=TweakRisk.Moderate },
        new() { Id="fso", Title="Fullscreen Optimizations (FSO)", Description="Aplica preferência de fullscreen pelo GameConfigStore.", Risk=TweakRisk.Moderate },
        new() { Id="game_bar", Title="Game Bar", Description="Desativa Game Bar e captura em segundo plano.", Risk=TweakRisk.Safe },
        new() { Id="indexing", Title="Windows Search Indexing", Description="Desativa o serviço WSearch. Pode afetar a busca do Windows.", Risk=TweakRisk.Aggressive }
    };

    public IEnumerable<TweakDefinition> Preset(string preset) => preset switch
    {
        "Basic" => Tweaks.Where(x => x.Risk == TweakRisk.Safe),
        "Optimal" => Tweaks.Where(x => x.Risk != TweakRisk.Aggressive),
        "Maximum" => Tweaks,
        _ => Enumerable.Empty<TweakDefinition>()
    };

    public int OptimizationPercent()
    {
        if (Tweaks.Count == 0) return 0;
        var applied = Tweaks.Count(x => IsApplied(x.Id));
        return (int)Math.Round(applied * 100.0 / Tweaks.Count);
    }

    public bool IsApplied(string id)
    {
        try
        {
            return id switch
            {
                "mouse_acceleration" => ReadString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed") == "0",
                "driver_updates" => ReadDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig") == 0,
                "notifications" => ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled") == 0,
                "uwp_background" => ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled") == 1,
                "fso" => ReadDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode") == 2,
                "game_bar" => ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") == 0,
                "indexing" => ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WSearch", "Start") == 4,
                _ => false
            };
        }
        catch { return false; }
    }

    public void Apply(string id)
    {
        switch (id)
        {
            case "mouse_acceleration":
                WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0");
                WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "0");
                WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "0");
                break;
            case "driver_updates":
                WriteDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig", 0);
                break;
            case "notifications":
                WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 0);
                break;
            case "uwp_background":
                WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1);
                break;
            case "fso":
                WriteDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2);
                WriteDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 1);
                break;
            case "game_bar":
                WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
                WriteDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0);
                break;
            case "indexing":
                Run("sc.exe", "stop WSearch");
                Run("sc.exe", "config WSearch start= disabled");
                break;
            default:
                throw new InvalidOperationException("Ajuste desconhecido.");
        }
    }

    public void Revert(string id)
    {
        switch (id)
        {
            case "mouse_acceleration":
                WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1");
                WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "6");
                WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "10");
                break;
            case "driver_updates":
                WriteDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig", 1);
                break;
            case "notifications":
                WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 1);
                break;
            case "uwp_background":
                WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0);
                break;
            case "fso":
                WriteDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 0);
                WriteDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 0);
                break;
            case "game_bar":
                WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1);
                WriteDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 1);
                break;
            case "indexing":
                Run("sc.exe", "config WSearch start= delayed-auto");
                Run("sc.exe", "start WSearch");
                break;
        }
    }

    private static int? ReadDword(RegistryKey hive, string path, string name)
    {
        using var key = hive.OpenSubKey(path);
        return key?.GetValue(name) as int?;
    }

    private static string? ReadString(RegistryKey hive, string path, string name)
    {
        using var key = hive.OpenSubKey(path);
        return key?.GetValue(name)?.ToString();
    }

    private static void WriteDword(RegistryKey hive, string path, string name, int value)
    {
        using var key = hive.CreateSubKey(path, true);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static void WriteString(RegistryKey hive, string path, string name, string value)
    {
        using var key = hive.CreateSubKey(path, true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    private static void Run(string file, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = file,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit(5000);
    }
}