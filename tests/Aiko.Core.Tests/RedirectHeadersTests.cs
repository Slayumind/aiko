namespace Aiko.Core.Tests;

/// Which headers go on after a redirect: a token stays with its host (D-420).
public class RedirectHeadersTests
{
    [Fact]
    public void A_secret_header_stays_with_its_host()
    {
        foreach (var row in SpecCases.Json("redirect-headers").GetProperty("rows").EnumerateArray())
        {
            var header = row.GetProperty("header").GetString()!;
            var from = new Uri(row.GetProperty("from").GetString()!);
            var to = new Uri(row.GetProperty("to").GetString()!);

            Assert.True(
                row.GetProperty("keeps").GetBoolean() == RedirectHeaders.Keeps(header, from, to),
                $"{header}: {from} -> {to}");
        }
    }
}
