using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

public class VoiceTintTests
{
    [Fact]
    public void Every_piece_of_output_is_tinted_the_same()
    {
        foreach (var row in SpecCases.Json("voice-tint").GetProperty("cases").EnumerateArray())
        {
            var tint = new VoiceTint(207, 233, 223);
            var output = string.Concat(row.GetProperty("pieces").EnumerateArray().Select(piece => tint.Apply(piece.GetString()!)));
            Assert.True(row.GetProperty("output").GetString() == output, $"{row.GetProperty("name").GetString()}: {JsonSerializer.Serialize(output)}");
        }
    }
}
