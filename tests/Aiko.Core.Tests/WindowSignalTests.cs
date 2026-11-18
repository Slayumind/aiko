using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// Which state colours the terminal window. Cases: spec/cases/window-signal.
public class WindowSignalTests
{
    [Fact]
    public void Every_case_gives_the_same_signal()
    {
        foreach (var row in SpecCases.Json("window-signal").GetProperty("signals").EnumerateArray())
        {
            var sessions = row.GetProperty("sessions").EnumerateArray()
                .Select(state => Enum.Parse<RowState>(state.GetString()!, ignoreCase: true));
            var limit = Enum.Parse<LimitTone>(row.GetProperty("limit").GetString()!, ignoreCase: true);

            Assert.Equal(
                Enum.Parse<WindowSignal>(row.GetProperty("signal").GetString()!, ignoreCase: true),
                WindowSignals.Of(sessions, limit));
        }
    }
}
