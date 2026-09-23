using System.IO;
using System.Text.RegularExpressions;
using Aiko.Core;

namespace Aiko.Core.Tests;

/// The list of addresses Aiko may open, and the promise that nothing else can be opened by
/// accident (D-005).
public class AllowedHostsTests
{
    [Theory]
    [InlineData("https://api.anthropic.com/api/oauth/usage")]
    [InlineData("https://slayumind.org/api/v1/aiko/version")]
    [InlineData("https://SLAYUMIND.ORG/api/v1/aiko/version")]
    [InlineData("https://api.github.com/repos/Slayumind/aiko/releases")]
    [InlineData("https://github.com/Slayumind/aiko/releases/download/v0.3.0/SHA256SUMS.txt")]
    [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/1")]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/1")]
    public void The_hosts_of_an_update_and_of_the_limits_are_allowed(string address)
    {
        Assert.True(AllowedHosts.Allows(new Uri(address)));
    }

    /// GitHub is here since 0.3, when Aiko began downloading the files of an update itself. The
    /// list is what makes "and nothing else" true, so a host GitHub does not use is still refused.
    [Theory]
    [InlineData("https://githubusercontent.com/file")]
    [InlineData("https://raw.githubusercontent.com/Slayumind/aiko/main/README.md")]
    [InlineData("https://codeload.github.com/Slayumind/aiko/zip/main")]
    public void A_github_host_that_is_not_on_the_list_is_refused(string address)
    {
        Assert.False(AllowedHosts.Allows(new Uri(address)));
    }

    /// The suffix trick is how such a list is usually defeated: a host that merely ends with an
    /// allowed name belongs to whoever registered it.
    [Theory]
    [InlineData("https://api.anthropic.com.example.net/usage")]
    [InlineData("https://slayumind.org.attacker.tld/api")]
    [InlineData("https://notslayumind.org/api")]
    [InlineData("https://evil.example/slayumind.org")]
    public void A_host_that_only_looks_like_one_of_them_is_refused(string address)
    {
        Assert.False(AllowedHosts.Allows(new Uri(address)));
    }

    [Theory]
    [InlineData("http://slayumind.org/api")]
    [InlineData("ftp://slayumind.org/api")]
    public void Anything_but_https_is_refused(string address)
    {
        // The access token travels on one of these connections in direct mode.
        Assert.False(AllowedHosts.Allows(new Uri(address)));
    }

    [Fact]
    public void Nothing_and_a_relative_address_are_refused()
    {
        Assert.False(AllowedHosts.Allows(null));
        Assert.False(AllowedHosts.Allows(new Uri("/api/v1/aiko/version", UriKind.Relative)));
    }

    [Fact]
    public void A_subdomain_of_an_allowed_host_is_still_not_that_host()
    {
        Assert.False(AllowedHosts.Allows(new Uri("https://cdn.slayumind.org/file")));
    }

    /// D-005 promised that unwanted hosts are cut off, not merely unused. A handler alone would
    /// not do it: `new HttpClient()` is one line, and the next client would be written that way —
    /// which is exactly how both of them were written until 0.2.1. This test is what makes the
    /// promise hold, so it reads the app's own source.
    [Fact]
    public void Every_http_client_in_the_app_is_made_by_the_factory()
    {
        var made = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(Repository(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            if (Regex.IsMatch(File.ReadAllText(file), @"new\s+HttpClient\s*\(")
                && Path.GetFileName(file) != "AikoHttp.cs")
            {
                made.Add(Path.GetFileName(file));
            }
        }

        Assert.True(
            made.Count == 0,
            $"HttpClient is made outside AikoHttp in: {string.Join(", ", made)}. Use AikoHttp.Client so the host guard applies.");
    }

    private static string Repository()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Aiko.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("the repository root was not found");
    }
}
