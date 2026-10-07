using System.Diagnostics;

namespace SevenzyX.Services;

public sealed class UtilityService
{
    public async Task<string> InstallWingetAsync(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return "Pacote inválido.";
        return await RunCaptureAsync("winget.exe", $"install --id {packageId} -e --accept-source-agreements --accept-package-agreements");
    }

    public async Task<string> CleanUserTempAsync()
    {
        var temp = Path.GetTempPath();
        long freed = 0;
        var failed = 0;

        await Task.Run(() =>
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories).Take(20000).ToList(); }
            catch { files = Array.Empty<string>(); }

            foreach (var file in files)
            {
                try
                {
                    var size = new FileInfo(file).Length;
                    File.Delete(file);
                    freed += size;
                }
                catch { failed++; }
            }

            IEnumerable<string> dirs;
            try { dirs = Directory.EnumerateDirectories(temp, "*", SearchOption.AllDirectories).Take(5000).OrderByDescending(x => x.Length).ToList(); }
            catch { dirs = Array.Empty<string>(); }

            foreach (var dir in dirs)
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        });

        return $"Limpeza concluída: {freed / 1024d / 1024d:0.0} MB liberados. {failed} arquivos em uso foram ignorados.";
    }

    public Task<string> EmptyRecycleBinAsync()
    {
        const string command = "Clear-RecycleBin -Force -ErrorAction SilentlyContinue; Write-Output 'Lixeira limpa.'";
        return RunCaptureAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"");
    }

    public Task<string> FlushDnsAsync() =>
        RunCaptureAsync("ipconfig.exe", "/flushdns");

    public async Task<string> ApplyRecommendedNetworkAsync()
    {
        var commands = new[]
        {
            ("netsh.exe", "int tcp set global autotuninglevel=normal"),
            ("netsh.exe", "int tcp set global rss=enabled"),
            ("netsh.exe", "int tcp set global ecncapability=disabled"),
            ("ipconfig.exe", "/flushdns")
        };

        foreach (var (file, args) in commands)
            await RunCaptureAsync(file, args);

        return "Perfil de rede recomendado aplicado.";
    }

    public Task<string> ResetWinsockAsync() =>
        RunCaptureAsync("netsh.exe", "winsock reset");

    public Task<string> CreateRestorePointAsync()
    {
        const string command =
            "Enable-ComputerRestore -Drive ($env:SystemDrive + '\\') -ErrorAction SilentlyContinue; " +
            "Checkpoint-Computer -Description '7zy X before optimization' -RestorePointType 'MODIFY_SETTINGS'; " +
            "Write-Output 'Ponto de restauração solicitado.'";

        return RunCaptureAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"");
    }

    public Task<string> RestartExplorerAsync() =>
        Task.Run(() =>
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("explorer"))
                {
                    try { p.Kill(); } catch { }
                }

                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
                return "Explorer reiniciado.";
            }
            catch (Exception ex)
            {
                return "Falha: " + ex.Message;
            }
        });

    public string SystemSummary()
    {
        var os = Environment.OSVersion.VersionString;
        var cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "CPU desconhecida";
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        var free = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
        var total = drive.TotalSize / 1024d / 1024d / 1024d;

        return $"Windows: {os}\nCPU: {cpu}\nDisco do sistema: {free:0.0} GB livres de {total:0.0} GB";
    }

    private static async Task<string> RunCaptureAsync(string file, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = file,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null) return "Não foi possível iniciar o comando.";

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var message = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            message = message.Replace("\r", " ").Replace("\n", " ").Trim();
            if (message.Length > 320) message = message[..320] + "…";

            return process.ExitCode == 0 ? message : $"Código {process.ExitCode}: {message}";
        }
        catch (Exception ex)
        {
            return "Falha: " + ex.Message;
        }
    }
}
