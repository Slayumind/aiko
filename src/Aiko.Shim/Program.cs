using System.Diagnostics;
using Aiko.Core;

// Somebody typed claude or aiko-work and is waiting for Claude Code. Two rules hold above the rest:
// start the real Claude Code whatever happens here, and never start another shim.
var platform = PlatformConventions.Windows;
var self = Environment.ProcessPath ?? RealClaude.ExecutableName(platform);
var selfFolder = Path.GetDirectoryName(self) ?? "";

var seen = RealClaude.ParseSeen(platform, Environment.GetEnvironmentVariable(RealClaude.SeenVariable))
    .Append(selfFolder)
    .ToList();

// For the release check: the file starts and its runtime is complete, with no Claude Code needed.
if (Environment.GetEnvironmentVariable("AIKO_SHIM_SELF_TEST") == "1")
{
    Console.WriteLine("aiko shim ok");
    return 0;
}

var real = RealClaude.Find(platform, Environment.GetEnvironmentVariable("PATH"), seen, File.Exists);
if (real is null)
{
    Console.Error.WriteLine(
        "Aiko: the real claude.exe isn't in PATH. Install Claude Code, or open a new terminal if you just did.");
    return 9009;
}

var start = new ProcessStartInfo(real) { UseShellExecute = false };
foreach (var argument in args)
{
    start.ArgumentList.Add(argument);
}

start.Environment[RealClaude.SeenVariable] = RealClaude.FormatSeen(platform, seen);
Apply(PlanFor(self), start);

// Ctrl+C reaches every process in the console. Claude Code handles it; the shim must not quit
// first and leave the terminal waiting on nothing.
Console.CancelKeyPress += (_, e) => e.Cancel = true;

using var claude = Process.Start(start);
if (claude is null)
{
    return 1;
}

claude.WaitForExit();
return claude.ExitCode;

/// Any failure here, a broken environments file included, means Claude Code starts the way it
/// would without Aiko.
static ShimPlan PlanFor(string self)
{
    try
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var file = AikoFolders.Windows(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).EnvironmentsFile;
        var settings = File.Exists(file) ? EnvironmentSettings.FromJson(File.ReadAllText(file)) : EnvironmentSettings.Empty;

        return ShimLaunch.Decide(self, Directory.GetCurrentDirectory(), settings, home);
    }
    catch (Exception)
    {
        return ShimPlan.PassThrough;
    }
}

/// The core decides what changes; here it is done.
static void Apply(ShimPlan plan, ProcessStartInfo start)
{
    foreach (var (name, value) in plan.VariableChanges)
    {
        if (value is null)
        {
            start.Environment.Remove(name);
        }
        else
        {
            start.Environment[name] = value;
        }
    }
}
