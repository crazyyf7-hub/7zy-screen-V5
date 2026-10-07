using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using SevenzyX.Models;
using SevenzyX.Services;

namespace SevenzyX;

public partial class MainWindow : Window
{
    private readonly AuthService _auth = new();
    private readonly OptimizationService _optimizer = new();
    private readonly SystemScanService _scanner = new();
    private readonly UtilityService _utility = new();
    private readonly AutorunService _autoruns = new();
    private readonly List<TweakDefinition> _items;

    public MainWindow()
    {
        InitializeComponent();

        _items = _optimizer.Tweaks.Select(x => new TweakDefinition
        {
            Id = x.Id,
            Category = x.Category,
            Title = x.Title,
            Description = x.Description,
            Risk = x.Risk,
            RequiresRestart = x.RequiresRestart,
            Selected = _optimizer.IsApplied(x.Id)
        }).ToList();

        TweakCountText.Text = _items.Count.ToString();

        CategoryFilter.Items.Add("All");
        CategoryFilter.Items.Add("My tweaks");
        foreach (var category in _optimizer.Categories)
            CategoryFilter.Items.Add(category);
        CategoryFilter.SelectedIndex = 0;

        RefreshTweakView();
        RefreshAutoruns();
        SystemSummaryText.Text = _utility.SystemSummary();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        LoginButton.Content = "Entrando…";
        LoginStatus.Text = "";

        await Task.Delay(180);
        var result = _auth.Login(UsernameBox.Text, PasswordBox.Password);

        if (!result.Ok)
        {
            LoginStatus.Text = string.IsNullOrWhiteSpace(_auth.LastError)
                ? "Usuário ou senha inválidos."
                : _auth.LastError;
            LoginButton.Content = "Entrar";
            LoginButton.IsEnabled = true;
            return;
        }

        UserLabel.Text = result.IsAdmin
            ? $"{UsernameBox.Text.Trim()} • Admin"
            : $"{UsernameBox.Text.Trim()} • Comprador";

        AdminButton.Visibility = result.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(170));
        fadeOut.Completed += async (_, _) =>
        {
            LoginView.Visibility = Visibility.Collapsed;
            AppView.Opacity = 0;
            AppView.Visibility = Visibility.Visible;
            ShowPanel(HomePanel);
            AppView.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
            await RunScanAsync();
        };
        LoginView.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void Home_Click(object sender, RoutedEventArgs e) => ShowPanel(HomePanel);
    private void Optimization_Click(object sender, RoutedEventArgs e) => ShowPanel(OptimizationPanel);
    private void QuickInstall_Click(object sender, RoutedEventArgs e) => ShowPanel(QuickInstallPanel);

    private void Tools_Click(object sender, RoutedEventArgs e)
    {
        RefreshAutoruns();
        ShowPanel(ToolsPanel);
    }

    private void Internet_Click(object sender, RoutedEventArgs e) => ShowPanel(InternetPanel);

    private void Extra_Click(object sender, RoutedEventArgs e)
    {
        SystemSummaryText.Text = _utility.SystemSummary();
        ShowPanel(ExtraPanel);
    }

    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        RefreshAdminUsers();
        ShowPanel(AdminPanel);
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        AppView.Visibility = Visibility.Collapsed;
        LoginView.Visibility = Visibility.Visible;
        LoginView.Opacity = 1;
        PasswordBox.Password = "";
        LoginButton.Content = "Entrar";
        LoginButton.IsEnabled = true;
    }

    private void ShowPanel(Grid panel)
    {
        foreach (var p in new[]
        {
            HomePanel, OptimizationPanel, QuickInstallPanel,
            ToolsPanel, InternetPanel, ExtraPanel, AdminPanel
        })
        {
            p.Visibility = Visibility.Collapsed;
        }

        panel.Opacity = 0;
        panel.Visibility = Visibility.Visible;
        panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
    }

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshTweakView();
    }

    private void RefreshTweakView()
    {
        if (TweaksList is null || CategoryFilter is null || _items is null) return;

        var selected = CategoryFilter.SelectedItem?.ToString() ?? "All";
        IEnumerable<TweakDefinition> view = _items;

        if (selected == "My tweaks")
            view = view.Where(x => x.Selected);
        else if (selected != "All")
            view = view.Where(x => x.Category == selected);

        TweaksList.ItemsSource = view.ToList();
    }

    private void SetPreset(string preset)
    {
        var ids = _optimizer.Preset(preset).Select(x => x.Id).ToHashSet();
        foreach (var item in _items)
            item.Selected = ids.Contains(item.Id);

        RefreshTweakView();
        ActionStatus.Text = $"Preset {preset} selecionado. Clique em Apply para executar.";
    }

    private void Default_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items)
            item.Selected = false;

        RefreshTweakView();
        ActionStatus.Text = "Seleção limpa.";
    }

    private void Basic_Click(object sender, RoutedEventArgs e) => SetPreset("Basic");
    private void Optimal_Click(object sender, RoutedEventArgs e) => SetPreset("Optimal");
    private void Maximum_Click(object sender, RoutedEventArgs e) => SetPreset("Maximum");

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(x => x.Selected).ToList();
        if (selected.Count == 0)
        {
            ActionStatus.Text = "Nenhum ajuste selecionado.";
            return;
        }

        if (selected.Any(x => x.Risk == TweakRisk.Aggressive))
        {
            var answer = MessageBox.Show(
                "Há ajustes agressivos selecionados. Eles podem desativar recursos como indexação, SysMain ou hibernação. O 7zy X fará backup do estado suportado antes de alterar. Continuar?",
                "7zy X • Maximum PRO",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes) return;
        }

        ApplyButton.IsEnabled = false;
        RevertButton.IsEnabled = false;
        ApplyButton.Content = "Aplicando…";
        HomeStateText.Text = "APPLYING";

        var ok = 0;
        var fail = 0;
        var failedNames = new List<string>();

        foreach (var item in selected)
        {
            try
            {
                _optimizer.Apply(item.Id);
                ok++;
            }
            catch
            {
                fail++;
                failedNames.Add(item.Title);
            }

            await Task.Delay(25);
        }

        foreach (var item in _items)
            item.Selected = _optimizer.IsApplied(item.Id);

        RefreshTweakView();
        await RunScanAsync();

        ApplyButton.Content = "✓ Aplicado";
        HomeStateText.Text = "READY";
        ActionStatus.Text = fail == 0
            ? $"{ok} alterações aplicadas."
            : $"{ok} aplicadas; {fail} falharam: {string.Join(", ", failedNames.Take(3))}.";

        await Task.Delay(450);
        ApplyButton.Content = "Apply";
        ApplyButton.IsEnabled = true;
        RevertButton.IsEnabled = true;
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(x => x.Selected).ToList();
        if (selected.Count == 0)
        {
            ActionStatus.Text = "Nenhum ajuste selecionado.";
            return;
        }

        ApplyButton.IsEnabled = false;
        RevertButton.IsEnabled = false;
        RevertButton.Content = "Revertendo…";

        var ok = 0;
        var fail = 0;

        foreach (var item in selected)
        {
            try
            {
                _optimizer.Revert(item.Id);
                ok++;
            }
            catch
            {
                fail++;
            }

            await Task.Delay(25);
        }

        foreach (var item in _items)
            item.Selected = _optimizer.IsApplied(item.Id);

        RefreshTweakView();
        await RunScanAsync();

        RevertButton.Content = "✓ Revertido";
        ActionStatus.Text = fail == 0
            ? $"{ok} alterações restauradas."
            : $"{ok} restauradas; {fail} falharam.";

        await Task.Delay(450);
        RevertButton.Content = "Reverter selecionados";
        ApplyButton.IsEnabled = true;
        RevertButton.IsEnabled = true;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        await RunScanAsync("Escaneando PC", true);
    }

    private async void ExtraScan_Click(object sender, RoutedEventArgs e)
    {
        await RunScanAsync("Executando novo scan completo", true);
    }

    private async Task RunScanAsync(string overlayTitle = "Escaneando PC", bool showOverlay = false)
    {
        SidebarScanLabel.Text = "Escaneando…";
        HomeStateText.Text = "SCANNING";

        var startedAt = DateTime.UtcNow;

        if (showOverlay)
        {
            ScanOverlayTitle.Text = overlayTitle;
            ScanOverlayStep.Text = "Preparando análise do sistema…";
            ScanOverlay.Opacity = 0;
            ScanOverlay.Visibility = Visibility.Visible;
            ScanOverlay.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        }

        var progress = new Progress<string>(message =>
        {
            SidebarScanLabel.Text = message.TrimEnd('…');
            if (showOverlay)
                ScanOverlayStep.Text = message;
        });

        try
        {
            var result = await _scanner.ScanAsync(progress);

            if (showOverlay)
            {
                var elapsed = DateTime.UtcNow - startedAt;
                var minimum = TimeSpan.FromMilliseconds(950);
                if (elapsed < minimum)
                    await Task.Delay(minimum - elapsed);

                ScanOverlayStep.Text = "Scan concluído. Atualizando resultados…";
                await Task.Delay(180);
            }

            SidebarPercent.Text = $"{result.Percent}%";
            HomePercent.Text = $"{result.Percent}%";
            SidebarScanLabel.Text = result.ScannedAt.ToString("dd/MM HH:mm");
            HomeStateText.Text = result.Percent >= 80 ? "GREAT" : result.Percent >= 60 ? "GOOD" : "TUNE";
            ScanChecksList.ItemsSource = result.Checks
                .OrderBy(x => x.Score)
                .ThenByDescending(x => x.Weight)
                .Take(12)
                .ToList();

            var weak = result.Checks.Count(x => x.Score < .75);
            ScanSummaryText.Text =
                $"Score calculado em {result.Checks.Count} verificações ponderadas. " +
                $"{weak} pontos têm espaço para melhoria. Essa porcentagem é o 7zy X Score e não tenta copiar a fórmula proprietária de outro programa.";
        }
        catch (Exception ex)
        {
            SidebarScanLabel.Text = "Scan falhou";
            HomeStateText.Text = "ERROR";
            ScanSummaryText.Text = ex.Message;

            if (showOverlay)
            {
                ScanOverlayTitle.Text = "Falha no scan";
                ScanOverlayStep.Text = ex.Message;
                await Task.Delay(900);
            }
        }
        finally
        {
            if (showOverlay)
            {
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                fade.Completed += (_, _) =>
                {
                    ScanOverlay.Visibility = Visibility.Collapsed;
                    ScanOverlay.Opacity = 1;
                };
                ScanOverlay.BeginAnimation(OpacityProperty, fade);
            }
        }
    }

    private async void InstallPackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string packageId) return;

        var old = button.Content;
        button.IsEnabled = false;
        button.Content = "Instalando…";
        InstallStatus.Text = $"Instalando {old}…";

        InstallStatus.Text = await _utility.InstallWingetAsync(packageId);

        button.Content = old;
        button.IsEnabled = true;
    }

    private async void CleanTemp_Click(object sender, RoutedEventArgs e)
    {
        ToolStatus.Text = "Limpando temporários…";
        ToolStatus.Text = await _utility.CleanUserTempAsync();
        await RunScanAsync();
    }

    private async void Recycle_Click(object sender, RoutedEventArgs e)
    {
        ToolStatus.Text = "Esvaziando lixeira…";
        ToolStatus.Text = await _utility.EmptyRecycleBinAsync();
    }

    private async void RestorePoint_Click(object sender, RoutedEventArgs e)
    {
        ActionStatus.Text = "Criando ponto de restauração…";
        ToolStatus.Text = "Criando ponto de restauração…";
        var result = await _utility.CreateRestorePointAsync();
        ActionStatus.Text = result;
        ToolStatus.Text = result;
    }

    private async void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        ToolStatus.Text = await _utility.RestartExplorerAsync();
    }

    private void RefreshAutoruns_Click(object sender, RoutedEventArgs e) => RefreshAutoruns();

    private void RefreshAutoruns()
    {
        AutorunsGrid.ItemsSource = _autoruns.GetEntries();
        AutorunsGrid.Items.Refresh();
    }

    private void DisableAutorun_Click(object sender, RoutedEventArgs e)
    {
        if (AutorunsGrid.SelectedItem is not AutorunEntry entry)
        {
            ToolStatus.Text = "Selecione uma entrada de inicialização.";
            return;
        }

        if (_autoruns.Disable(entry.Id, out var error))
        {
            ToolStatus.Text = $"{entry.Name} desativado e salvo para restauração.";
            RefreshAutoruns();
        }
        else
        {
            ToolStatus.Text = error;
        }
    }

    private void RestoreAutorun_Click(object sender, RoutedEventArgs e)
    {
        if (AutorunsGrid.SelectedItem is not AutorunEntry entry)
        {
            ToolStatus.Text = "Selecione uma entrada de inicialização.";
            return;
        }

        if (_autoruns.Restore(entry.Id, out var error))
        {
            ToolStatus.Text = $"{entry.Name} restaurado.";
            RefreshAutoruns();
        }
        else
        {
            ToolStatus.Text = error;
        }
    }

    private async void NetworkRecommended_Click(object sender, RoutedEventArgs e)
    {
        NetworkStatus.Text = "Aplicando perfil recomendado…";
        NetworkStatus.Text = await _utility.ApplyRecommendedNetworkAsync();
    }

    private async void FlushDns_Click(object sender, RoutedEventArgs e)
    {
        NetworkStatus.Text = await _utility.FlushDnsAsync();
    }

    private async void ResetWinsock_Click(object sender, RoutedEventArgs e)
    {
        NetworkStatus.Text = await _utility.ResetWinsockAsync();
        NetworkStatus.Text += " Reinicie o Windows para concluir.";
    }

    private void CreateBuyer_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(AdminValidityBox.Text, out var days))
        {
            AdminStatus.Text = "Informe uma validade em dias.";
            return;
        }

        if (_auth.CreateBuyer(AdminUsernameBox.Text, AdminPasswordBox.Password, days, out var error))
        {
            AdminStatus.Text = "Comprador criado com sucesso.";
            AdminUsernameBox.Text = "";
            AdminPasswordBox.Password = "";
            RefreshAdminUsers();
        }
        else
        {
            AdminStatus.Text = error;
        }
    }

    private void ToggleBuyer_Click(object sender, RoutedEventArgs e)
    {
        if (AdminUsersGrid.SelectedItem is not UserAccountInfo user)
        {
            AdminStatus.Text = "Selecione uma conta.";
            return;
        }

        if (_auth.ToggleBlocked(user.Username, out var error))
        {
            AdminStatus.Text = "Status da conta atualizado.";
            RefreshAdminUsers();
        }
        else
        {
            AdminStatus.Text = error;
        }
    }

    private void ExtendBuyer_Click(object sender, RoutedEventArgs e)
    {
        if (AdminUsersGrid.SelectedItem is not UserAccountInfo user)
        {
            AdminStatus.Text = "Selecione uma conta.";
            return;
        }

        if (!int.TryParse(AdminValidityBox.Text, out var days))
        {
            AdminStatus.Text = "Informe a nova validade em dias.";
            return;
        }

        if (_auth.SetValidityDays(user.Username, days, out var error))
        {
            AdminStatus.Text = "Validade renovada.";
            RefreshAdminUsers();
        }
        else
        {
            AdminStatus.Text = error;
        }
    }

    private void DeleteBuyer_Click(object sender, RoutedEventArgs e)
    {
        if (AdminUsersGrid.SelectedItem is not UserAccountInfo user)
        {
            AdminStatus.Text = "Selecione uma conta.";
            return;
        }

        if (_auth.DeleteBuyer(user.Username, out var error))
        {
            AdminStatus.Text = "Conta removida.";
            RefreshAdminUsers();
        }
        else
        {
            AdminStatus.Text = error;
        }
    }

    private void RefreshAdminUsers()
    {
        AdminUsersGrid.ItemsSource = _auth.GetUsers();
        AdminUsersGrid.Items.Refresh();
    }
}
