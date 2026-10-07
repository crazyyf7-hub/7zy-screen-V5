using Microsoft.Win32;
using SevenzyX.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SevenzyX.Services;

public sealed class SystemScanService
{
    public Task<SystemScanResult> ScanAsync() => Task.Run(Scan);

    public SystemScanResult Scan()
    {
        var checks = new List<ScanCheckResult>();

        Add(checks, "Mouse acceleration", "Input", 5, BoolScore(ReadString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed") == "0"),
            "Aceleração do mouse desativada para resposta consistente.");

        Add(checks, "Game Bar / Captura", "Gaming", 7, BoolScore(
            ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") == 0),
            "Captura em segundo plano do Windows.");

        Add(checks, "Aplicativos em segundo plano", "Background", 8, BoolScore(
            ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled") == 1),
            "Execução de apps UWP em segundo plano.");

        Add(checks, "Notificações", "Background", 4, BoolScore(
            ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled") == 0),
            "Notificações toast do usuário atual.");

        Add(checks, "Indexação", "Services", 5, BoolScore(
            ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WSearch", "Start") is 3 or 4),
            "Peso menor: a busca do Windows pode ser útil.");

        Add(checks, "SysMain", "Services", 4, ServiceScore("SysMain"),
            "Serviço de pré-carregamento; o resultado depende do estado atual.");

        Add(checks, "DiagTrack", "Privacy", 5, ServiceScore("DiagTrack"),
            "Telemetria de experiências conectadas.");

        Add(checks, "Power throttling", "Power", 8, BoolScore(
            ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff") == 1),
            "Limitação de potência para processos em segundo plano.");

        Add(checks, "Plano de energia", "Power", 12, PowerPlanScore(),
            "Avalia se o plano ativo é voltado a desempenho.");

        var systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        var freeRatio = systemDrive.TotalSize > 0 ? systemDrive.AvailableFreeSpace / (double)systemDrive.TotalSize : 0;
        var storageScore = freeRatio >= .20 ? 1.0 : freeRatio >= .10 ? .6 : freeRatio >= .05 ? .25 : 0;
        Add(checks, "Espaço livre no sistema", "Storage", 10, storageScore,
            $"{freeRatio:P0} livre no disco do Windows.");

        var startupCount = CountStartupEntries();
        var startupScore = startupCount <= 5 ? 1.0 : startupCount <= 10 ? .75 : startupCount <= 18 ? .45 : .2;
        Add(checks, "Programas de inicialização", "Startup", 10, startupScore,
            $"{startupCount} entradas encontradas.");

        var tempBytes = EstimateTempSizeBytes();
        var tempGb = tempBytes / 1024d / 1024d / 1024d;
        var tempScore = tempGb < .5 ? 1.0 : tempGb < 2 ? .8 : tempGb < 5 ? .5 : .2;
        Add(checks, "Arquivos temporários", "Cleanup", 7, tempScore,
            $"Aproximadamente {tempGb:0.0} GB em temporários do usuário.");

        Add(checks, "Atualização de mapas", "Background", 4, BoolScore(
            ReadDword(Registry.LocalMachine, @"SYSTEM\Maps", "AutoUpdateEnabled") == 0),
            "Atualização automática de mapas.");

        Add(checks, "Apps da Store", "Background", 5, BoolScore(
            ReadDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate", "AutoDownload") == 2),
            "Atualizações automáticas de apps da Store.");

        var totalWeight = checks.Sum(x => x.Weight);
        var weighted = checks.Sum(x => x.Score * x.Weight);
        var percent = totalWeight <= 0 ? 0 : (int)Math.Round(weighted / totalWeight * 100);

        return new SystemScanResult
        {
            Percent = Math.Clamp(percent, 0, 100),
            Checks = checks,
            ScannedAt = DateTime.Now
        };
    }

    private static void Add(List<ScanCheckResult> list, string name, string category, double weight, double score, string detail) =>
        list.Add(new ScanCheckResult { Name = name, Category = category, Weight = weight, Score = Math.Clamp(score, 0, 1), Detail = detail });

    private static double BoolScore(bool value) => value ? 1 : 0;

    private static double ServiceScore(string serviceName)
    {
        try
        {
            var start = ReadDword(Registry.LocalMachine, $@"SYSTEM\CurrentControlSet\Services\{serviceName}", "Start");
            return start switch
            {
                4 => 1.0,
                3 => .75,
                2 => .45,
                _ => .5
            };
        }
        catch { return .5; }
    }

    private static double PowerPlanScore()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/getactivescheme",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            var text = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(3000);

            if (text.Contains("e9a42b02-d5df-448d-aa00-03f14749eb61", StringComparison.OrdinalIgnoreCase)) return 1.0; // Ultimate
            if (text.Contains("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase)) return .95; // High performance
            if (text.Contains("381b4222-f694-41f0-9685-ff5bb260df2e", StringComparison.OrdinalIgnoreCase)) return .75; // Balanced
            return .6;
        }
        catch { return .6; }
    }

    private static int CountStartupEntries()
    {
        var total = 0;
        total += CountValues(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run");
        total += CountValues(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run");
        return total;
    }

    private static int CountValues(RegistryKey hive, string path)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            return key?.GetValueNames().Length ?? 0;
        }
        catch { return 0; }
    }

    private static long EstimateTempSizeBytes()
    {
        try
        {
            var temp = Path.GetTempPath();
            long total = 0;
            var files = Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories).Take(5000);
            foreach (var file in files)
            {
                try { total += new FileInfo(file).Length; } catch { }
            }
            return total;
        }
        catch { return 0; }
    }

    private static int? ReadDword(RegistryKey hive, string path, string name)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            var value = key?.GetValue(name);
            return value is int i ? i : null;
        }
        catch { return null; }
    }

    private static string? ReadString(RegistryKey hive, string path, string name)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            return key?.GetValue(name)?.ToString();
        }
        catch { return null; }
    }
}
