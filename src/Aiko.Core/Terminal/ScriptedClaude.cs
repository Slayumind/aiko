using System.Text.Json;

namespace Aiko.Core.Terminal;

/// One answer of a scripted Claude Code: the lines it writes after something was written to it.
///
/// After names what it waits for: "start" (the process was started), "user" (a message),
/// "response" (an answer to a request), "interrupt" or "mode". With Fails the start does not
/// happen. With Exit the process ends after its lines.
public sealed record ScriptedReply(string After, IReadOnlyList<string> Lines, int? Exit, bool Fails);

/// Claude Code played from a list, for the checks that must not start the real one. It keeps its
/// place in the list across processes, so a script can end a process and go on in the next.
public sealed class ScriptedClaude(IReadOnlyList<ScriptedReply> script)
{
    private int _next;

    /// Every start that was asked for, also the ones that failed.
    public List<ProcessStart> Starts { get; } = [];

    /// Every line written to a process that was alive.
    public List<string> Written { get; } = [];

    public IConversationProcess NewProcess() => new Process(this);

    private ScriptedReply? Awaited(string kind) =>
        _next < script.Count && script[_next].After == kind ? script[_next++] : null;

    private static string KindOf(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            return Word(root, "type") switch
            {
                "user" => "user",
                "control_response" => "response",
                "control_request" when root.TryGetProperty("request", out var request) =>
                    Word(request, "subtype") == "interrupt" ? "interrupt" : "mode",
                _ => "other",
            };
        }
        catch (JsonException)
        {
            return "other";
        }
    }

    private static string Word(JsonElement node, string key) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private sealed class Process(ScriptedClaude claude) : IConversationProcess
    {
        private Action<string>? _onLine;
        private Action<int>? _onExit;
        private bool _alive;

        public bool Start(ProcessStart start, Action<string> onLine, Action<int> onExit)
        {
            claude.Starts.Add(start);

            var reply = claude.Awaited("start");
            if (reply is { Fails: true })
            {
                return false;
            }

            _onLine = onLine;
            _onExit = onExit;
            _alive = true;
            Play(reply);
            return true;
        }

        public bool Write(string line)
        {
            if (!_alive)
            {
                return false;
            }

            claude.Written.Add(line);
            Play(claude.Awaited(KindOf(line)));
            return true;
        }

        public void Stop() => _alive = false;

        private void Play(ScriptedReply? reply)
        {
            if (reply is null)
            {
                return;
            }

            foreach (var line in reply.Lines)
            {
                _onLine?.Invoke(line);
            }

            if (reply.Exit is { } code)
            {
                _alive = false;
                _onExit?.Invoke(code);
            }
        }
    }
}
