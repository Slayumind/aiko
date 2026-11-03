using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The zoom of the terminal window, from spec/cases/window-zoom (D-384).
public class WindowZoomTests
{
    [Fact]
    public void The_steps_and_the_default_are_the_cases()
    {
        var cases = SpecCases.Json("window-zoom");

        Assert.Equal([.. cases.GetProperty("steps").EnumerateArray().Select(step => step.GetInt32())], WindowZoom.Steps);
        // A value that is no step reads as the default, so the default shows through Read.
        Assert.Equal(cases.GetProperty("default").GetInt32(), WindowZoom.Read(0));
    }

    [Fact]
    public void Every_move_case_gives_the_same_step()
    {
        foreach (var row in SpecCases.Json("window-zoom").GetProperty("moves").EnumerateArray())
        {
            var from = row.GetProperty("from").GetInt32();
            Assert.Equal(row.GetProperty("larger").GetInt32(), WindowZoom.Larger(from));
            Assert.Equal(row.GetProperty("smaller").GetInt32(), WindowZoom.Smaller(from));
        }
    }

    [Fact]
    public void Every_stored_value_reads_as_the_same_step()
    {
        foreach (var row in SpecCases.Json("window-zoom").GetProperty("read").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("zoom").GetInt32(), WindowZoom.Read(row.GetProperty("stored").GetInt32()));
        }
    }

    [Fact]
    public void Every_step_gives_the_same_terminal_font()
    {
        foreach (var row in SpecCases.Json("window-zoom").GetProperty("terminalPoints").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("points").GetInt32(), WindowZoom.TerminalPoints(row.GetProperty("zoom").GetInt32()));
        }
    }

    [Fact]
    public void A_new_settings_file_has_the_default_zoom()
    {
        Assert.Equal(115, AppSettings.Default.WindowZoom);
        Assert.Equal(115, AppSettings.FromJson("{}").WindowZoom);
    }

    [Fact]
    public void The_zoom_goes_through_the_settings_file()
    {
        var saved = AppSettings.Default with { WindowZoom = 130 };

        Assert.Equal(130, AppSettings.FromJson(saved.ToJson()).WindowZoom);
    }
}
