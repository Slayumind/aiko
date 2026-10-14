using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Threading;
using Aiko.Core;
using Windows.Win32;

namespace Aiko.App;

/// --try-shell: starts the real shell and walks it through what a person does with it: the settings
/// window, the tray icon and its menu, the card pinned and unpinned, a face, the island and back.
/// The shell runs with changingNothing, so no settings file is written and nothing is asked of the
/// network.
///
/// One step moves the real mouse: it rests on the icon for two seconds, the way a person opens the
/// card, and then goes back where it was. The keys for the menu are posted to Aiko's own window
/// and reach no other program.
///
/// What it cannot see is what only the real taskbar decides: whether the icon sits under the
/// overflow arrow, where a mouse cannot rest on it.
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

        // ---- the settings window: one at a time, and gone when closed ----
        shell.OpenSettingsForCheck();
        await Task.Delay(600);
        var settings = shell.SettingsForCheck;
        shell.OpenSettingsForCheck();
        var oneWindow = settings is { IsVisible: true } && ReferenceEquals(settings, shell.SettingsForCheck);
        settings?.Close();
        var settingsGone = await Until(() => shell.SettingsForCheck is null);

        // ---- the tray: an icon Windows can find, and no island ----
        shell.ShowPlaceForCheck(AikoPlace.Tray);
        await Task.Delay(300);
        var icon = shell.IconRectForCheck is not null && shell.IslandForCheck is null;

        // ---- a click pins the card with a block per environment; a second click closes it ----
        shell.OpenCardForCheck(pinned: true);
        await Task.Delay(500);
        var pinned = shell.CardForCheck is { IsVisible: true, IsPinned: true } && shell.BlocksOnCardForCheck == environments;
        shell.OpenCardForCheck(pinned: true);
        var unpinned = await Until(() => shell.CardForCheck is null);

        // ---- the wait runs out with the mouse somewhere else: no card ----
        shell.StartHoverForCheck();
        await Task.Delay(1900);
        var quietHover = Checked(shell.CardForCheck is null, shell);
        await CloseCard(shell);

        // ---- a card that is not pinned goes when the mouse is not on it ----
        shell.OpenCardForCheck(pinned: false);
        await Task.Delay(300);
        var opened = shell.CardForCheck is not null;
        var left = Checked(await Until(() => shell.CardForCheck is null, 4000), shell);
        await CloseCard(shell);

        // ---- the mouse rests on the icon: the card opens, and leaves when the mouse does ----
        var restingMouse = await RestOnTheIcon(shell);

        // ---- the menu of the icon: "Settings" is picked with the keyboard and opens the window ----
        var menu = await PickSettingsInTheMenu(shell);

        // ---- a face comes onto the icon and goes ----
        shell.ShowFaceForCheck(AikoFace.Done);
        var faceCame = await Until(() => shell.FaceOnIconForCheck);
        shell.ShowFaceForCheck(null);
        var faceWent = await Until(() => !shell.FaceOnIconForCheck, 3000);

        // ---- the island: the icon goes, the card opens from the island, a face does not break it ----
        shell.ShowPlaceForCheck(AikoPlace.Island);
        await Task.Delay(500);
        var island = shell.IslandForCheck is not null && shell.IconRectForCheck is null;
        shell.ShowFaceForCheck(AikoFace.Done);
        shell.OpenCardForCheck(pinned: true);
        await Task.Delay(500);
        var cardFromIsland = shell.CardForCheck is { IsVisible: true, IsPinned: true };
        shell.ShowFaceForCheck(null);
        await CloseCard(shell);

        // ---- and back to where the person keeps Aiko ----
        shell.ShowPlaceForCheck(home);
        await Task.Delay(300);
        var back = (home == AikoPlace.Island) == (shell.IslandForCheck is not null);

        Log.Write($"--try-shell: one settings window={oneWindow}, gone when closed={settingsGone}, icon={icon}, "
            + $"card pinned={pinned}, second click closes={unpinned}, hover with the mouse away opens nothing={quietHover}, "
            + $"unpinned card opened={opened} and left={left}, a resting mouse opens the card={restingMouse}, "
            + $"the menu opens settings={menu}, face came={faceCame} and went={faceWent}, "
            + $"island={island}, card from the island={cardFromIsland}, back home={back}");

        return oneWindow && settingsGone && icon && pinned && unpinned && quietHover != "False" && opened && left != "False"
            && restingMouse != "False" && menu && faceCame && faceWent && island && cardFromIsland && back;
    }

    /// The answer is "not checked" when somebody moved the mouse during the two seconds it had to
    /// rest: the machine has a person at it, and that is not a failure of the shell.
    private static async Task<string> RestOnTheIcon(AikoShell shell)
    {
        if (shell.IconRectForCheck is not { } icon || !PInvoke.GetCursorPos(out var was))
        {
            return "False";
        }

        var rest = new Point(icon.Left + (icon.Width / 2), icon.Top + (icon.Height / 2));
        MouseInput.MoveTo(rest);
        shell.StartHoverForCheck();
        var opened = await Until(() => shell.CardForCheck is { IsPinned: false }, 2500);
        var stayed = PInvoke.GetCursorPos(out var now) && now.X == (int)rest.X && now.Y == (int)rest.Y;

        MouseInput.MoveTo(new Point(was.X, was.Y));
        var left = await Until(() => shell.CardForCheck is null, 4000) || shell.MouseAtHomeForCheck;
        await CloseCard(shell);

        return opened && left ? "True" : stayed ? "False" : "not checked, the mouse was moved";
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

        var opened = await Until(() => shell.SettingsForCheck is { IsVisible: true });
        shell.SettingsForCheck?.Close();
        return opened && await Until(() => shell.SettingsForCheck is null);
    }

    /// A step that depends on the mouse being away. When the pointer happens to rest on the icon,
    /// the island or the card, the step says so in place of failing: the mouse is not ours to move.
    private static string Checked(bool passed, AikoShell shell) =>
        passed ? "True" : shell.MouseAtHomeForCheck ? "not checked, the mouse is there" : "False";

    private static async Task CloseCard(AikoShell shell)
    {
        if (shell.CardForCheck is { } card)
        {
            card.FadeAndClose();
            await Until(() => shell.CardForCheck is null);
        }
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
