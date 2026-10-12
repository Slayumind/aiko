using System.Runtime.Versioning;
using System.Windows;
using Aiko.Core;

namespace Aiko.App;

/// --try-shell: starts the real shell and walks it through what a person does with it: the settings
/// window, the tray icon, the card pinned and unpinned, a face, the island and back. The shell runs
/// with changingNothing, so no settings file is written and nothing is asked of the network. The
/// mouse is left alone.
///
/// What it cannot see is what only the real taskbar decides: the icon under the overflow arrow, the
/// right-click menu, and a card opened by a mouse that rests on the icon.
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
            + $"unpinned card opened={opened} and left={left}, face came={faceCame} and went={faceWent}, "
            + $"island={island}, card from the island={cardFromIsland}, back home={back}");

        return oneWindow && settingsGone && icon && pinned && unpinned && quietHover != "False" && opened && left != "False"
            && faceCame && faceWent && island && cardFromIsland && back;
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
