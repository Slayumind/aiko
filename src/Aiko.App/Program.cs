using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Aiko.Core;
using Velopack;

namespace Aiko.App;

/// The start of Aiko. It knows a plain start, a start at sign-in, the hooks of the installer and two
/// commands for the setup of environments. Checks and snapshots are another program, Aiko.Checks
/// (D-420): a person should never start one by chance.
[SupportedOSPlatform("windows10.0.17763")]
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var start = AppStarts.Of(args);

        // The installer's hooks and the commands never open a window.
        if (start != AppStart.Other)
        {
            GraphicsWarmUp.Start();
        }

        // First thing, before anything is drawn: the installer starts Aiko with its own arguments
        // for installing, updating and removing, and this handles them and exits.
        //
        // The hook before uninstalling is the reason this is here at all: it is where the status
        // line goes back into the Claude Code settings, as promised on the first run. The hook has
        // no window to ask in, so the memory is kept (null: nobody was asked).
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => Uninstall.Cleanup(keepMemory: null))
            .Run();

        // One copy of Aiko (D-419): a second start by the person opens the window of the running
        // copy, a second start at sign-in only exits.
        if (!SingleCopy.Claim(start))
        {
            return;
        }

        // Puts the saved environments into effect outside Aiko, the way the wizard's Finish does:
        // status lines and the reminder hook, the command folder in PATH, profile functions off.
        if (args is [AppStarts.ApplyEnvironments, ..])
        {
            Environment.Exit(EnvironmentSetup.ApplySaved() ? 0 : 1);
            return;
        }

        // Takes back the command folder, the PATH entry and the profile changes.
        if (args is [AppStarts.UndoEnvironmentSetup, ..])
        {
            EnvironmentSetup.Undo();
            Environment.Exit(0);
            return;
        }

        // Software rendering: the spike measured 33 MB against 92 MB with the DirectX stack, and
        // Aiko draws a small card a few times an hour. Nobody needs the GPU for that.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        // Before any window is made: the words are picked when a control is created, so a late
        // change would leave half a window in the other language.
        LanguageChoice.Apply(SettingsStore.Load().Language);

        var application = Tips.Install(new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown });
        AikoShell? shell = null;
        CrashGuard.Watch(application, () => shell?.CloseWindows());

        application.Startup += (_, _) =>
        {
            shell = new AikoShell(application);
            shell.Show(withWindows: start == AppStart.AtSignIn);
            SingleCopy.Listen(application.Dispatcher, shell.OpenAskedByAnotherStart);
        };
        application.Exit += (_, _) => shell?.Dispose();

        application.Run();
    }
}
