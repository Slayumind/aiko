namespace Aiko.Core.Tests;

/// The letter of an environment on the open island (D-421).
public class EnvironmentInitialTests
{
    [Theory]
    [InlineData("Aiko", "A")]
    [InlineData("work · Kodland", "W")]
    [InlineData("  личное", "Л")]
    [InlineData("2nd", "2")]
    [InlineData("· ·", "")]
    [InlineData("", "")]
    public void An_environment_shows_its_first_letter(string name, string letter) =>
        Assert.Equal(letter, EnvironmentInitial.Of(name));
}
