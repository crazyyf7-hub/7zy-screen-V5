using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;
using SevenzyX.Models;

namespace SevenzyX.Services;

public sealed class OptimizationService
{
    private readonly StateBackupService _backup = new();
    private readonly string _powerBackupPath;

    public OptimizationService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "7zyX");
        Directory.CreateDirectory(dir);
        _powerBackupPath = Path.Combine(dir, "power-plan.txt");
    }

    public IReadOnlyList<TweakDefinition> Tweaks { get; } = new List<TweakDefinition>
    {
        // Basic settings
        T("mouse_acceleration","Basic","Mouse acceleration","Desativa aceleração do mouse para resposta mais consistente.",TweakRisk.Safe),
        T("driver_updates","Basic","Automatic driver updates","Impede a busca automática de drivers pelo Windows.",TweakRisk.Moderate),
        T("notifications","Basic","Windows 11 Notifications","Desativa notificações toast do usuário atual.",TweakRisk.Safe),
        T("uwp_background","Basic","UWP applications running in the background","Reduz execução de aplicativos UWP em segundo plano.",TweakRisk.Moderate),
        T("map_updates","Basic","Automatic map updates","Desativa atualização automática de mapas offline.",TweakRisk.Safe),
        T("store_updates","Basic","Automatic updates for Store apps","Reduz atualizações automáticas de apps da Microsoft Store.",TweakRisk.Moderate),
        T("fso","Basic","Global Fullscreen Optimizations (FSO)","Aplica preferência global de fullscreen para jogos.",TweakRisk.Moderate),
        T("game_bar","Basic","Game Bar","Desativa Game Bar e captura em segundo plano.",TweakRisk.Safe),
        T("indexing","Basic","Indexing","Desativa o serviço Windows Search. Pode afetar pesquisas.",TweakRisk.Aggressive),

        // Security
        T("remote_assistance","Security","Remote Assistance","Desativa convites de Assistência Remota.",TweakRisk.Safe),
        T("remote_registry","Security","Remote Registry","Desativa o serviço de Registro Remoto.",TweakRisk.Safe),

        // Customization
        T("visual_effects","Customization","Visual effects for performance","Prioriza desempenho nas animações e efeitos do Windows.",TweakRisk.Safe),
        T("transparency","Customization","Transparency effects","Desativa transparência da interface.",TweakRisk.Safe),
        T("menu_delay","Customization","Menu show delay","Reduz atraso de abertura dos menus.",TweakRisk.Safe),
        T("taskbar_animations","Customization","Taskbar animations","Desativa animações da barra de tarefas.",TweakRisk.Safe),
        T("startup_delay","Customization","Startup app delay","Remove atraso artificial de aplicativos na inicialização.",TweakRisk.Safe),

        // Power management
        T("power_throttling","Power management","Power throttling","Desativa power throttling para priorizar desempenho.",TweakRisk.Moderate),
        T("high_performance","Power management","High performance power plan","Ativa o plano Alto Desempenho do Windows.",TweakRisk.Moderate),
        T("hibernate","Power management","Hibernation","Desativa hibernação e libera o arquivo hiberfil.sys.",TweakRisk.Aggressive,true),

        // Debloat
        T("consumer_features","Debloat","Windows consumer features","Desativa instalação e sugestões automáticas de conteúdo promocional.",TweakRisk.Safe),
        T("suggested_content","Debloat","Suggested content","Desativa sugestões e conteúdo recomendado.",TweakRisk.Safe),
        T("widgets","Debloat","Widgets / News interests","Desativa Widgets/News via política do Windows.",TweakRisk.Moderate,true),

        // Privacy
        T("advertising_id","Privacy","Advertising ID","Desativa ID de publicidade do usuário.",TweakRisk.Safe),
        T("tailored_experiences","Privacy","Tailored experiences","Desativa experiências personalizadas com dados de diagnóstico.",TweakRisk.Safe),
        T("activity_history","Privacy","Activity history","Desativa publicação e upload do histórico de atividades.",TweakRisk.Safe),
        T("feedback","Privacy","Feedback requests","Reduz solicitações de feedback do Windows.",TweakRisk.Safe),
        T("diagtrack","Privacy","Connected User Experiences / Telemetry","Desativa o serviço DiagTrack.",TweakRisk.Moderate),

        // Tweaks
        T("network_throttling","Tweaks","Network throttling index","Remove limitação multimídia de rede.",TweakRisk.Moderate),
        T("system_responsiveness","Tweaks","System responsiveness","Prioriza tarefas interativas no perfil multimídia.",TweakRisk.Moderate),
        T("delivery_optimization","Tweaks","Delivery Optimization P2P","Desativa download P2P do Windows Delivery Optimization.",TweakRisk.Safe),
        T("game_priority","Tweaks","Games scheduling priority","Ajusta prioridade multimídia para jogos.",TweakRisk.Moderate),

        // Services
        T("sysmain","Services","SysMain","Desativa pré-carregamento SysMain.",TweakRisk.Aggressive),
        T("mapsbroker","Services","Downloaded Maps Manager","Desativa o serviço MapsBroker.",TweakRisk.Moderate),
        T("fax","Services","Fax service","Desativa o serviço de Fax.",TweakRisk.Safe),
        T("xblgamesave","Services","Xbox Live Game Save","Desativa serviço Xbox Live Game Save.",TweakRisk.Moderate),
        T("xboxnetapi","Services","Xbox networking service","Desativa serviço Xbox networking.",TweakRisk.Moderate),
        T("wmpnetwork","Services","Windows Media Player sharing","Desativa compartilhamento de rede do Windows Media Player.",TweakRisk.Safe)
    };

    public IReadOnlyList<string> Categories => Tweaks.Select(x => x.Category).Distinct().ToList();

    public IEnumerable<TweakDefinition> Preset(string preset) => preset switch
    {
        "Basic" => Tweaks.Where(x => x.Risk == TweakRisk.Safe),
        "Optimal" => Tweaks.Where(x => x.Risk != TweakRisk.Aggressive),
        "Maximum" => Tweaks,
        _ => Enumerable.Empty<TweakDefinition>()
    };

    public bool IsApplied(string id)
    {
        try
        {
            return id switch
            {
                "mouse_acceleration" => S(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed") == "0",
                "driver_updates" => D(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig") == 0,
                "notifications" => D(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled") == 0,
                "uwp_background" => D(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled") == 1,
                "map_updates" => D(Registry.LocalMachine, @"SYSTEM\Maps", "AutoUpdateEnabled") == 0,
                "store_updates" => D(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate", "AutoDownload") == 2,
                "fso" => D(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode") == 2,
                "game_bar" => D(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") == 0,
                "indexing" => ServiceDisabled("WSearch"),
                "remote_assistance" => D(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp") == 0,
                "remote_registry" => ServiceDisabled("RemoteRegistry"),
                "visual_effects" => D(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting") == 2,
                "transparency" => D(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency") == 0,
                "menu_delay" => S(Registry.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay") == "0",
                "taskbar_animations" => D(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations") == 0,
                "startup_delay" => D(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec") == 0,
                "power_throttling" => D(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff") == 1,
                "high_performance" => ActivePowerPlanContains("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
                "hibernate" => D(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled") == 0,
                "consumer_features" => D(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures") == 1,
                "suggested_content" => D(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled") == 0,
                "widgets" => D(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests") == 0,
                "advertising_id" => D(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled") == 0,
                "tailored_experiences" => D(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled") == 0,
                "activity_history" => D(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed") == 0,
                "feedback" => D(Registry.CurrentUser, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod") == 0,
                "diagtrack" => ServiceDisabled("DiagTrack"),
                "network_throttling" => D(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex") == -1,
                "system_responsiveness" => D(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness") == 0,
                "delivery_optimization" => D(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode") == 0,
                "game_priority" => D(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority") == 8,
                "sysmain" => ServiceDisabled("SysMain"),
                "mapsbroker" => ServiceDisabled("MapsBroker"),
                "fax" => ServiceDisabled("Fax"),
                "xblgamesave" => ServiceDisabled("XblGameSave"),
                "xboxnetapi" => ServiceDisabled("XboxNetApiSvc"),
                "wmpnetwork" => ServiceDisabled("WMPNetworkSvc"),
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
                WS(Registry.CurrentUser, "HKCU", @"Control Panel\Mouse", "MouseSpeed", "0");
                WS(Registry.CurrentUser, "HKCU", @"Control Panel\Mouse", "MouseThreshold1", "0");
                WS(Registry.CurrentUser, "HKCU", @"Control Panel\Mouse", "MouseThreshold2", "0");
                break;
            case "driver_updates": WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig", 0); break;
            case "notifications": WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 0); break;
            case "uwp_background": WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1); break;
            case "map_updates": WD(Registry.LocalMachine, "HKLM", @"SYSTEM\Maps", "AutoUpdateEnabled", 0); break;
            case "store_updates": WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate", "AutoDownload", 2); break;
            case "fso":
                WD(Registry.CurrentUser, "HKCU", @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2);
                WD(Registry.CurrentUser, "HKCU", @"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 1);
                break;
            case "game_bar":
                WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
                WD(Registry.CurrentUser, "HKCU", @"System\GameConfigStore", "GameDVR_Enabled", 0);
                break;
            case "indexing": DisableService("WSearch"); break;
            case "remote_assistance": WD(Registry.LocalMachine, "HKLM", @"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp", 0); break;
            case "remote_registry": DisableService("RemoteRegistry"); break;
            case "visual_effects": WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2); break;
            case "transparency": WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0); break;
            case "menu_delay": WS(Registry.CurrentUser, "HKCU", @"Control Panel\Desktop", "MenuShowDelay", "0"); break;
            case "taskbar_animations": WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0); break;
            case "startup_delay": WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0); break;
            case "power_throttling": WD(Registry.LocalMachine, "HKLM", @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1); break;
            case "high_performance":
                CapturePowerPlan();
                Run("powercfg.exe", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                break;
            case "hibernate":
                _backup.Capture(Registry.LocalMachine, "HKLM", @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled");
                Run("powercfg.exe", "/hibernate off");
                break;
            case "consumer_features": WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1); break;
            case "suggested_content":
                WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0);
                WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0);
                WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0);
                break;
            case "widgets": WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0); break;
            case "advertising_id": WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0); break;
            case "tailored_experiences": WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0); break;
            case "activity_history":
                WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0);
                WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0);
                WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0);
                break;
            case "feedback": WD(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0); break;
            case "diagtrack": DisableService("DiagTrack"); break;
            case "network_throttling": WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", -1); break;
            case "system_responsiveness": WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0); break;
            case "delivery_optimization": WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0); break;
            case "game_priority":
                WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8);
                WD(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 6);
                WS(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", "High");
                break;
            case "sysmain": DisableService("SysMain"); break;
            case "mapsbroker": DisableService("MapsBroker"); break;
            case "fax": DisableService("Fax"); break;
            case "xblgamesave": DisableService("XblGameSave"); break;
            case "xboxnetapi": DisableService("XboxNetApiSvc"); break;
            case "wmpnetwork": DisableService("WMPNetworkSvc"); break;
            default: throw new InvalidOperationException("Ajuste desconhecido.");
        }
    }

    public void Revert(string id)
    {
        switch (id)
        {
            case "mouse_acceleration":
                Restore(Registry.CurrentUser, "HKCU", @"Control Panel\Mouse", "MouseSpeed");
                Restore(Registry.CurrentUser, "HKCU", @"Control Panel\Mouse", "MouseThreshold1");
                Restore(Registry.CurrentUser, "HKCU", @"Control Panel\Mouse", "MouseThreshold2");
                break;
            case "driver_updates": Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", "SearchOrderConfig"); break;
            case "notifications": Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled"); break;
            case "uwp_background": Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled"); break;
            case "map_updates": Restore(Registry.LocalMachine, "HKLM", @"SYSTEM\Maps", "AutoUpdateEnabled"); break;
            case "store_updates": Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate", "AutoDownload"); break;
            case "fso":
                Restore(Registry.CurrentUser, "HKCU", @"System\GameConfigStore", "GameDVR_FSEBehaviorMode");
                Restore(Registry.CurrentUser, "HKCU", @"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode");
                break;
            case "game_bar":
                Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled");
                Restore(Registry.CurrentUser, "HKCU", @"System\GameConfigStore", "GameDVR_Enabled");
                break;
            case "indexing": RestoreService("WSearch"); break;
            case "remote_assistance": Restore(Registry.LocalMachine, "HKLM", @"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp"); break;
            case "remote_registry": RestoreService("RemoteRegistry"); break;
            case "visual_effects": Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting"); break;
            case "transparency": Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency"); break;
            case "menu_delay": Restore(Registry.CurrentUser, "HKCU", @"Control Panel\Desktop", "MenuShowDelay"); break;
            case "taskbar_animations": Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations"); break;
            case "startup_delay": Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec"); break;
            case "power_throttling": Restore(Registry.LocalMachine, "HKLM", @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff"); break;
            case "high_performance": RestorePowerPlan(); break;
            case "hibernate":
                if (_backup.Restore(Registry.LocalMachine, "HKLM", @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled"))
                    Run("powercfg.exe", D(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled") == 0 ? "/hibernate off" : "/hibernate on");
                break;
            case "consumer_features": Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures"); break;
            case "suggested_content":
                Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled");
                Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled");
                Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled");
                break;
            case "widgets": Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests"); break;
            case "advertising_id": Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled"); break;
            case "tailored_experiences": Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled"); break;
            case "activity_history":
                Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed");
                Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities");
                Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities");
                break;
            case "feedback": Restore(Registry.CurrentUser, "HKCU", @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod"); break;
            case "diagtrack": RestoreService("DiagTrack"); break;
            case "network_throttling": Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex"); break;
            case "system_responsiveness": Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness"); break;
            case "delivery_optimization": Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode"); break;
            case "game_priority":
                Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority");
                Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority");
                Restore(Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category");
                break;
            case "sysmain": RestoreService("SysMain"); break;
            case "mapsbroker": RestoreService("MapsBroker"); break;
            case "fax": RestoreService("Fax"); break;
            case "xblgamesave": RestoreService("XblGameSave"); break;
            case "xboxnetapi": RestoreService("XboxNetApiSvc"); break;
            case "wmpnetwork": RestoreService("WMPNetworkSvc"); break;
        }
    }

    private static TweakDefinition T(string id, string category, string title, string description, TweakRisk risk, bool restart = false) =>
        new() { Id = id, Category = category, Title = title, Description = description, Risk = risk, RequiresRestart = restart };

    private static int? D(RegistryKey hive, string path, string name)
    {
        using var key = hive.OpenSubKey(path);
        var value = key?.GetValue(name);
        return value switch
        {
            int i => i,
            uint u => unchecked((int)u),
            _ => null
        };
    }

    private static string? S(RegistryKey hive, string path, string name)
    {
        using var key = hive.OpenSubKey(path);
        return key?.GetValue(name)?.ToString();
    }

    private void WD(RegistryKey hive, string hiveName, string path, string name, int value)
    {
        _backup.Capture(hive, hiveName, path, name);
        using var key = hive.CreateSubKey(path, true);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    private void WS(RegistryKey hive, string hiveName, string path, string name, string value)
    {
        _backup.Capture(hive, hiveName, path, name);
        using var key = hive.CreateSubKey(path, true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    private void Restore(RegistryKey hive, string hiveName, string path, string name)
    {
        _backup.Restore(hive, hiveName, path, name);
    }

    private static bool ServiceDisabled(string service) =>
        D(Registry.LocalMachine, $@"SYSTEM\CurrentControlSet\Services\{service}", "Start") == 4;

    private void DisableService(string service)
    {
        var path = $@"SYSTEM\CurrentControlSet\Services\{service}";
        _backup.Capture(Registry.LocalMachine, "HKLM", path, "Start");
        Run("sc.exe", $"stop {service}");
        Run("sc.exe", $"config {service} start= disabled");
    }

    private void RestoreService(string service)
    {
        var path = $@"SYSTEM\CurrentControlSet\Services\{service}";
        if (!_backup.Restore(Registry.LocalMachine, "HKLM", path, "Start")) return;

        var start = D(Registry.LocalMachine, path, "Start");
        if (start == 2)
            Run("sc.exe", $"start {service}", false);
    }

    private void CapturePowerPlan()
    {
        if (File.Exists(_powerBackupPath)) return;
        var guid = ActivePowerPlanGuid();
        if (!string.IsNullOrWhiteSpace(guid))
            File.WriteAllText(_powerBackupPath, guid);
    }

    private void RestorePowerPlan()
    {
        if (!File.Exists(_powerBackupPath)) return;
        var guid = File.ReadAllText(_powerBackupPath).Trim();
        if (!string.IsNullOrWhiteSpace(guid))
            Run("powercfg.exe", $"/setactive {guid}");
    }

    private static string? ActivePowerPlanGuid()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/getactivescheme",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            var output = process?.StandardOutput.ReadToEnd() ?? "";
            process?.WaitForExit(3000);
            return Regex.Match(output, "[0-9a-fA-F-]{36}").Value;
        }
        catch { return null; }
    }

    private static bool ActivePowerPlanContains(string guid) =>
        string.Equals(ActivePowerPlanGuid(), guid, StringComparison.OrdinalIgnoreCase);

    private static void Run(string file, string arguments, bool throwOnFailure = true)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = file,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        process?.WaitForExit(10000);
        if (throwOnFailure && process is not null && process.HasExited && process.ExitCode != 0)
            throw new InvalidOperationException($"{file} retornou código {process.ExitCode}.");
    }
}
