using System.Diagnostics;

namespace SevenzyX.Services;

public sealed class UtilityService
{
    public async Task<string> InstallWingetAsync(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return "Pacote inválido.";

        var wingetPath = await ResolveWingetAsync();
        if (string.IsNullOrWhiteSpace(wingetPath))
            return "Winget não foi encontrado. Abra a Microsoft Store, atualize o App Installer e tente novamente.";

        var winget = await RunCaptureWithCodeAsync(wingetPath, "--version");
        if (winget.ExitCode != 0)
            return $"Winget foi encontrado, mas não iniciou corretamente. {winget.Message}";

        var installed = await RunCaptureWithCodeAsync(
            wingetPath,
            $"list --id {packageId} -e --accept-source-agreements --disable-interactivity");

        if (installed.ExitCode == 0 &&
            installed.Message.Contains(packageId, StringComparison.OrdinalIgnoreCase))
            return "Esse aplicativo já está instalado.";

        var silentArgs =
            $"install --id {packageId} -e --source winget --silent " +
            "--accept-source-agreements --accept-package-agreements --disable-interactivity";

        var first = await RunCaptureWithCodeAsync(wingetPath, silentArgs);
        if (first.ExitCode == 0)
            return string.IsNullOrWhiteSpace(first.Message) ? "Instalação concluída." : first.Message;

        await RunCaptureWithCodeAsync(wingetPath, "source reset --force");
        await RunCaptureWithCodeAsync(wingetPath, "source update --disable-interactivity");

        var interactiveArgs =
            $"install --id {packageId} -e --source winget " +
            "--accept-source-agreements --accept-package-agreements --disable-interactivity";

        var retry = await RunCaptureWithCodeAsync(wingetPath, interactiveArgs);

        return retry.ExitCode == 0
            ? (string.IsNullOrWhiteSpace(retry.Message) ? "Instalação concluída." : retry.Message)
            : $"Falha ao instalar. {retry.Message}";
    }

    public async Task<string> CopyBundledPackageToDownloadsAsync(
        string bundledFileName,
        IProgress<double>? progress = null)
    {
        var source = ResolveCustomPackage(bundledFileName);
        if (source is null)
            return $"Arquivo {bundledFileName} não foi encontrado. Baixe o pacote personalizado e deixe o .RAR em Downloads ou ao lado do 7zyX.exe.";

        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(downloads);

        var destination = Path.Combine(downloads, bundledFileName);
        var temp = destination + ".7zyx-part";

        try
        {
            if (string.Equals(
                Path.GetFullPath(source),
                Path.GetFullPath(destination),
                StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(1);
                return $"Pronto. {bundledFileName} já está em Downloads.";
            }
            await using var input = new FileStream(
                source, FileMode.Open, FileAccess.Read, FileShare.Read,
                1024 * 1024, useAsync: true);
            await using var output = new FileStream(
                temp, FileMode.Create, FileAccess.Write, FileShare.None,
                1024 * 1024, useAsync: true);

            var buffer = new byte[1024 * 1024];
            long copied = 0;
            int read;

            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read));
                copied += read;
                progress?.Report(input.Length == 0 ? 1 : copied / (double)input.Length);
            }

            await output.FlushAsync();
            output.Close();

            File.Move(temp, destination, true);
            progress?.Report(1);

            return $"Pronto. {bundledFileName} foi salvo em Downloads.";
        }
        catch (Exception ex)
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            return "Falha ao copiar para Downloads: " + ex.Message;
        }
    }

    private static string? ResolveCustomPackage(string fileName)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        var aliases = fileName switch
        {
            "OBS_MODIFICADO_BN.rar" => new[] { "OBS_MODIFICADO_BN.rar", "OBS MODIFICADO (1).rar", "OBS MODIFICADO.rar" },
            "SPOTFY_LITE.rar" => new[] { "SPOTFY_LITE.rar", "SPOTFY LITE.rar", "SPOTIFY LITE.rar" },
            "7ZY_TikTok_Chat_v3.rar" => new[] { "7ZY_TikTok_Chat_v3.rar", "7ZY TikTok Chat v3.rar" },
            _ => new[] { fileName }
        };

        var folders = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "CustomPackages"),
            AppContext.BaseDirectory,
            downloads,
            desktop
        };

        foreach (var folder in folders)
        {
            foreach (var alias in aliases)
            {
                var candidate = Path.Combine(folder, alias);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static async Task<string?> ResolveWingetAsync()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var alias = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");

        foreach (var candidate in new[] { alias, "winget.exe" })
        {
            if (!string.Equals(candidate, "winget.exe", StringComparison.OrdinalIgnoreCase) &&
                !File.Exists(candidate))
                continue;

            var probe = await RunCaptureWithCodeAsync(candidate, "--version");
            if (probe.ExitCode == 0)
                return candidate;
        }

        try
        {
            const string script =
                "$p=(Get-AppxPackage Microsoft.DesktopAppInstaller | Sort-Object Version -Descending | Select-Object -First 1 -ExpandProperty InstallLocation); " +
                "if($p){Join-Path $p 'winget.exe'}";

            var ps = await RunCaptureWithCodeAsync(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"");

            if (ps.ExitCode == 0)
            {
                var candidate = ps.Message.Trim();
                if (candidate.EndsWith("winget.exe", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(candidate))
                    return candidate;
            }
        }
        catch { }

        return null;
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
        var result = await RunCaptureWithCodeAsync(file, arguments);
        return result.ExitCode == 0 ? result.Message : $"Código {result.ExitCode}: {result.Message}";
    }

    private static async Task<(int ExitCode, string Message)> RunCaptureWithCodeAsync(
        string file,
        string arguments)
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

            if (process is null)
                return (-1, "Não foi possível iniciar o comando.");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var message = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            message = message.Replace("\r", " ").Replace("\n", " ").Trim();
            if (message.Length > 500) message = message[..500] + "…";

            return (process.ExitCode, message);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
