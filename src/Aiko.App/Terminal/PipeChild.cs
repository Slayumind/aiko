using System.Diagnostics;
using System.IO;
using System.Text;

namespace Aiko.App;

/// The other end of --try-pipe: Aiko started as a child of itself, in the place of Claude Code.
///
/// It sends every line back as it came. Three lines mean something else: "spawn" starts a child
/// of its own and answers with both process ids, "variables" answers with two variables of its
/// environment, and the end of the input is ignored, so only being ended from outside stops it.
static class PipeChild
{
    public const string Argument = "--pipe-child";

    /// The child of the child. It does nothing and never leaves on its own.
    public const string Idle = "idle";

    public const string SetVariable = "AIKO_PIPE_SET";

    public const string RemovedVariable = "AIKO_PIPE_REMOVED";

    public static void Run(string[] args)
    {
        if (args is [_, Idle, ..])
        {
            Thread.Sleep(Timeout.Infinite);
        }

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var input = new StreamReader(Console.OpenStandardInput(), utf8);
        using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { NewLine = "\n", AutoFlush = true };

        while (input.ReadLine() is { } line)
        {
            output.WriteLine(line switch
            {
                "spawn" => $"{Environment.ProcessId} {Spawn()}",
                "variables" => $"{Environment.GetEnvironmentVariable(SetVariable)}|{Environment.GetEnvironmentVariable(RemovedVariable)}",
                _ => line,
            });
        }

        Thread.Sleep(Timeout.Infinite);
    }

    private static int Spawn()
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Argument);
        start.ArgumentList.Add(Idle);

        using var child = Process.Start(start);
        return child?.Id ?? 0;
    }
}
