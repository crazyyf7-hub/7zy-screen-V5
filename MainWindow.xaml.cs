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
    private readonly List<TweakDefinition> _items;

    public MainWindow()
    {
        InitializeComponent();

        _items = _optimizer.Tweaks.Select(x => new TweakDefinition
        {
            Id = x.Id,
            Title = x.Title,
            Description = x.Description,
            Risk = x.Risk,
            Selected = _optimizer.IsApplied(x.Id)
        }).ToList();

        TweaksList.ItemsSource = _items;
        RefreshPercent();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        LoginButton.Content = "Entrando…";
        LoginStatus.Text = "";

        await Task.Delay(250);
        var result = _auth.Login(UsernameBox.Text, PasswordBox.Password);

        if (!result.Ok)
        {
            LoginStatus.Text = string.IsNullOrWhiteSpace(_auth.LastError) ? "Usuário ou senha inválidos." : _auth.LastError;
            LoginButton.Content = "Entrar";
            LoginButton.IsEnabled = true;
            return;
        }

        UserLabel.Text = result.IsAdmin
            ? $"{UsernameBox.Text.Trim()} • Admin"
            : $"{UsernameBox.Text.Trim()} • Comprador";

        AdminButton.Visibility = result.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        fadeOut.Completed += (_, _) =>
        {
            LoginView.Visibility = Visibility.Collapsed;
            AppView.Opacity = 0;
            AppView.Visibility = Visibility.Visible;
            AppView.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
        };
        LoginView.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void Home_Click(object sender, RoutedEventArgs e) => ShowPanel(HomePanel);
    private void Optimization_Click(object sender, RoutedEventArgs e) => ShowPanel(OptimizationPanel);
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
        HomePanel.Visibility = Visibility.Collapsed;
        OptimizationPanel.Visibility = Visibility.Collapsed;
        AdminPanel.Visibility = Visibility.Collapsed;

        panel.Opacity = 0;
        panel.Visibility = Visibility.Visible;
        panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    private void SetPreset(string preset)
    {
        var ids = _optimizer.Preset(preset).Select(x => x.Id).ToHashSet();
        foreach (var item in _items)
            item.Selected = ids.Contains(item.Id);

        TweaksList.Items.Refresh();
        ActionStatus.Text = $"Preset {preset} selecionado.";
    }

    private void Default_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items)
            item.Selected = false;

        TweaksList.Items.Refresh();
        ActionStatus.Text = "Default selecionado.";
    }

    private void Basic_Click(object sender, RoutedEventArgs e) => SetPreset("Basic");
    private void Optimal_Click(object sender, RoutedEventArgs e) => SetPreset("Optimal");
    private void Maximum_Click(object sender, RoutedEventArgs e) => SetPreset("Maximum");

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        ApplyButton.IsEnabled = false;
        RevertButton.IsEnabled = false;
        ApplyButton.Content = "Aplicando…";
        ActionStatus.Text = "Aplicando alterações…";

        var ok = 0;
        var fail = 0;

        foreach (var item in _items.Where(x => x.Selected))
        {
            try
            {
                _optimizer.Apply(item.Id);
                ok++;
            }
            catch
            {
                fail++;
            }

            await Task.Delay(70);
        }

        RefreshPercent();
        ApplyButton.Content = "✓ Aplicado";
        ActionStatus.Text = fail == 0 ? $"{ok} alterações aplicadas." : $"{ok} aplicadas; {fail} falharam.";

        await Task.Delay(450);
        ApplyButton.Content = "Apply";
        ApplyButton.IsEnabled = true;
        RevertButton.IsEnabled = true;
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
    {
        ApplyButton.IsEnabled = false;
        RevertButton.IsEnabled = false;
        RevertButton.Content = "Revertendo…";

        var ok = 0;
        var fail = 0;

        foreach (var item in _items.Where(x => x.Selected))
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

            await Task.Delay(70);
        }

        RefreshPercent();
        RevertButton.Content = "✓ Revertido";
        ActionStatus.Text = fail == 0 ? $"{ok} alterações revertidas." : $"{ok} revertidas; {fail} falharam.";

        await Task.Delay(450);
        RevertButton.Content = "Reverter selecionados";
        ApplyButton.IsEnabled = true;
        RevertButton.IsEnabled = true;
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

    private void RefreshPercent()
    {
        var value = _optimizer.OptimizationPercent();
        SidebarPercent.Text = $"{value}%";
        HomePercent.Text = $"{value}%";
    }
}