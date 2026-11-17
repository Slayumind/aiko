namespace Aiko.Core.Tests;

/// Which starts of Aiko keep one copy and which open the window of the running one (D-419).
public class AppStartTests
{
    [Theory]
    [InlineData(new string[0], AppStart.Plain, true, true)]
    [InlineData(new[] { "--autostart" }, AppStart.AtSignIn, true, false)]
    [InlineData(new[] { "--apply-environments" }, AppStart.Other, false, false)]
    [InlineData(new[] { "--undo-environment-setup" }, AppStart.Other, false, false)]
    [InlineData(new[] { "--veloapp-install", "1.0.0" }, AppStart.Other, false, false)]
    [InlineData(new[] { "--try-shell" }, AppStart.Plain, true, true)]
    [InlineData(new[] { "Install" }, AppStart.Plain, true, true)]
    [InlineData(new[] { @"C:\Users\me\project" }, AppStart.Plain, true, true)]
    [InlineData(new[] { "--autostart", "--try-shell" }, AppStart.Plain, true, true)]
    public void A_start_is_told_by_its_arguments(string[] args, AppStart start, bool oneCopy, bool opens)
    {
        Assert.Equal(start, AppStarts.Of(args));
        Assert.Equal(oneCopy, AppStarts.KeepsOneCopy(start));
        Assert.Equal(opens, AppStarts.OpensRunningWindow(start));
    }
}
