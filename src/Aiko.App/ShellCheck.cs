using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Threading;
using Aiko.Core;
using Windows.Win32;

namespace Aiko.App;

/// --try-shell: starts the real shell and walks it through what a person does with it: the settings
/// in the terminal window, the tray icon, its click and its menu, a face, the island and its click,
/// and back.
/// The shell runs with changingNothing, so no settings file is written and nothing is asked of the
/// network.
///
/// The mouse is not moved: the clicks are the ones the icon and the island raise. The keys for the
/// menu are posted to Aiko's own window and reach no other program. The terminal window opens on
/// no sessions, so no program starts.
///
/// What it cannot see is what only the real taskbar decides: whether the icon sits under the
/// overflow arrow.
[SupportedOSPlatform("windows10.0.17763")]
static class ShellCheck
{
    public static int Run()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var shell = new AikoShell(application);
        var result = 1;

        application.Startup += async (_, _) =>
        {
            try
            {
                shell.Show(changingNothing: true);
                await Task.Delay(400);
                result = await Walk(shell) ? 0 : 1;
            }
            catch (Exception failed)
            {
                Log.Write($"--try-shell: {failed.GetType().Name}: {failed.Message}");
            }
            finally
            {
                shell.Dispose();
                application.Shutdown();
            }
        };

        application.Run();
        return result;
    }

    private static async Task<bool> Walk(AikoShell shell)
    {
        var home = SettingsStore.Load().Place;
        var environments = SettingsStore.LoadEnvironments().Environments.Count;

        // ---- the settings: in the terminal window, and gone when closed. Before the first
        // environment the first run stands there instead. ----
        shell.OpenSettingsForCheck();
        await Task.Delay(600);
        shell.OpenSettingsForCheck();
        var oneWindow = shell.SettingsShownForCheck && shell.FirstRunForCheck == (environments == 0);
        shell.CloseSettingsForCheck();
        var settingsGone = await Until(() => !shell.SettingsShownForCheck);

        // ---- the tray: an icon Windows can find, and no island ----
        shell.ShowPlaceForCheck(AikoPlace.Tray);
        await Task.Delay(300);
        var icon = shell.IconRectForCheck is not null && shell.IslandForCheck is null;

        // ---- a click on the icon opens the terminal window (D-372); before the first environment,
        // the first run ----
        shell.ClickIconForCheck();
        var clickOpens = await Until(() => environments == 0 ? shell.SettingsShownForCheck : shell.TerminalShownForCheck);
        await CloseWindows(shell);

        // ---- the menu of the icon: "Settings" is picked with the keyboard and opens the window ----
        var menu = await PickSettingsInTheMenu(shell);

        // ---- a face comes onto the icon and goes ----
        shell.ShowFaceForCheck(AikoFace.Done);
        var faceCame = await Until(() => shell.FaceOnIconForCheck);
        shell.ShowFaceForCheck(null);
        var faceWent = await Until(() => !shell.FaceOnIconForCheck, 3000);

        // ---- the island: the icon goes, and a click on it opens the window, a face does not break it ----
        shell.ShowPlaceForCheck(AikoPlace.Island);
        await Task.Delay(500);
        var island = shell.IslandForCheck is not null && shell.IconRectForCheck is null;
        shell.ShowFaceForCheck(AikoFace.Done);
        shell.ClickIslandForCheck();
        var islandOpens = await Until(() => environments == 0 ? shell.SettingsShownForCheck : shell.TerminalShownForCheck);
        shell.ShowFaceForCheck(null);
        await CloseWindows(shell);

        // ---- and back to where the person keeps Aiko ----
        shell.ShowPlaceForCheck(home);
        await Task.Delay(300);
        var back = (home == AikoPlace.Island) == (shell.IslandForCheck is not null);

        Log.Write($"--try-shell: settings in the terminal window={oneWindow}, gone when closed={settingsGone}, icon={icon}, "
            + $"a click on the icon opens the window={clickOpens}, the menu opens settings={menu}, "
            + $"face came={faceCame} and went={faceWent}, island={island}, a click on the island opens the window={islandOpens}, "
            + $"back home={back}");

        return oneWindow && settingsGone && icon && clickOpens && menu && faceCame && faceWent && island && islandOpens && back;
    }

    /// The menu waits inside Windows until something is picked, so the keys are sent from a timer
    /// that fires while it is open. A second timer closes a menu the keys did not reach, so a
    /// failed step cannot hang the check.
    private static async Task<bool> PickSettingsInTheMenu(AikoShell shell)
    {
        const uint Down = 0x28;
        const uint Enter = 0x0D;

        // Settings come after the terminal. With two environments the menu starts with "show in
        // the ring" and a separator.
        var downs = SettingsStore.LoadEnvironments().Dot is null ? 2 : 3;

        var keys = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        keys.Tick += (_, _) =>
        {
            keys.Stop();
            for (var press = 0; press < downs; press++)
            {
                shell.PressInMenuForCheck(Down);
            }

            shell.PressInMenuForCheck(Enter);
        };

        var giveUp = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        giveUp.Tick += (_, _) =>
        {
            giveUp.Stop();
            shell.CancelMenuForCheck();
        };

        keys.Start();
        giveUp.Start();
        shell.OpenMenuForCheck();
        keys.Stop();
        giveUp.Stop();

        var opened = await Until(() => shell.SettingsShownForCheck);
        shell.CloseSettingsForCheck();
        return opened && await Until(() => !shell.SettingsShownForCheck);
    }

    /// The settings and the terminal window go, so the next step starts from the tray or the island.
    private static async Task CloseWindows(AikoShell shell)
    {
        shell.CloseSettingsForCheck();
        await Until(() => !shell.SettingsShownForCheck && !shell.TerminalShownForCheck);
    }

    private static async Task<bool> Until(Func<bool> met, int milliseconds = 2000)
    {
        for (var waited = 0; waited < milliseconds; waited += 50)
        {
            if (met())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return met();
    }
}
