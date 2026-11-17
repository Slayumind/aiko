using System.Text.Json;

namespace Aiko.Core.Tests;

/// What happens to Aiko's own files after a read, from spec/cases/stored-file.
public class StoredFileTests
{
    [Fact]
    public void Every_case_reads_the_same_way()
    {
        foreach (var row in SpecCases.Json("stored-file").GetProperty("cases").EnumerateArray())
        {
            var text = row.GetProperty("text").ValueKind == JsonValueKind.Null ? null : row.GetProperty("text").GetString();
            var state = StoredFile.Of(row.GetProperty("exists").GetBoolean(), text);

            Assert.Equal(row.GetProperty("state").GetString(), state.ToString());
            Assert.Equal(row.GetProperty("mayWrite").GetBoolean(), StoredFile.MayWrite(state));
        }
    }
}
