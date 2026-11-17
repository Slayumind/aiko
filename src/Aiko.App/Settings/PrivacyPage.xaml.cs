using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Aiko.Core;

namespace Aiko.App;

public partial class PrivacyPage : UserControl
{
    private const string PrivacyDoc = "https://github.com/Slayumind/aiko/blob/main/PRIVACY.md";

    private AppSettings _settings = SettingsStore.Load();

    /// Filling the controls raises their events, and those events save. See GeneralPage.
    private bool _filling;

    public PrivacyPage()
    {
        InitializeComponent();
        Fill();
    }

    /// Raised after a change was saved, so the navigation can redraw its line under the item.
    public event Action? Saved;

    private void Fill()
    {
        _filling = true;

        CheckUpdates.IsChecked = _settings.CheckUpdates;
        SendStats.IsChecked = _settings.SendStats;
        ShowLedger();

        _filling = false;
    }

    /// The list of fields is always there. Off, it is dimmed and followed by a line saying that
    /// none of it is sent: a person deciding needs to see the list before they decide, and a list
    /// that only appears after the answer is a list nobody read.
    private void ShowLedger()
    {
        var on = SendStats.IsChecked == true;

        Ledger.Opacity = on ? 1.0 : 0.45;
        NothingSent.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        ResetId.IsEnabled = on;
    }

    /// The change goes to what the file holds now, not to the copy the page read when it opened:
    /// the terminal window may have changed its zoom or its header since.
    private void Save(Func<AppSettings, AppSettings> change)
    {
        if (_filling)
        {
            return;
        }

        _settings = SettingsStore.Change(change);
        Saved?.Invoke();
    }

    private void OnUpdatesChanged(object sender, RoutedEventArgs e) =>
        Save(settings => settings with { CheckUpdates = CheckUpdates.IsChecked == true });

    /// Answering here counts as answering the wizard's question: somebody who found the switch
    /// themselves should not be asked about it again.
    private void OnStatsChanged(object sender, RoutedEventArgs e)
    {
        ShowLedger();
        ResetLine.Visibility = Visibility.Collapsed;
        Save(settings => settings with { SendStats = SendStats.IsChecked == true, PrivacyAsked = true });
    }

    private void OnResetId(object sender, RoutedEventArgs e)
    {
        UpdateRun.ForgetIdentity();
        ResetLine.Visibility = Visibility.Visible;
    }

    private void OnOpenPrivacyDoc(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(PrivacyDoc) { UseShellExecute = true });
}
