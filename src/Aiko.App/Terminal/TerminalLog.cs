using Aiko.Core.Terminal;

namespace Aiko.App;

/// The only place in the terminal that writes to the log of Aiko (D-298).
///
/// A method here takes numbers, yes or no, and the name of an environment. It takes no other
/// text, so a message, a path, an id or a line of Claude Code cannot reach the log through it. A
/// test reads this file and the rest of the folder to keep it so.
sealed class TerminalLog : ITerminalLog
{
    public void Started(string environment, bool resumed) =>
        Log.Write($"terminal: Claude Code started for {environment}, {(resumed ? "a resumed" : "a new")} conversation");

    public void StartFailed(string environment) =>
        Log.Write($"terminal: Claude Code did not start for {environment}");

    public void Exited(string environment, int exitCode, int linesRead, int linesWritten) =>
        Log.Write($"terminal: Claude Code for {environment} exited with {exitCode}, lines read {linesRead}, written {linesWritten}");

    public void Stopped(string environment, int linesRead, int linesWritten) =>
        Log.Write($"terminal: Claude Code for {environment} was stopped, lines read {linesRead}, written {linesWritten}");

    // ---- what the doors report ----

    public static void PipeChecked(bool started, bool letters, bool longLine, bool variables, bool exitSeen, bool treeGone) =>
        Log.Write($"--try-pipe: started={started}, letters={letters}, long line={longLine}, variables={variables}, exit seen={exitSeen}, tree gone={treeGone}");

    public static void ConversationChecked(bool read, bool markSeen, bool logClean, int items, int starts, int written) =>
        Log.Write($"--try-conversation: script read={read}, mark in the conversation={markSeen}, log clean={logClean}, items {items}, starts {starts}, lines written {written}");

    public static void HistoryChecked(bool found, int items, int messages, int answers, int steps, int notices) =>
        Log.Write($"--try-history: file found={found}, items {items}: messages {messages}, answers {answers}, steps {steps}, notices {notices}");

    public static void RealChecked(bool claudeFound, bool answered, bool signedOut, bool failed, int items) =>
        Log.Write($"--try-conversation real: claude found={claudeFound}, answered={answered}, signed out={signedOut}, failed={failed}, items {items}");
}
