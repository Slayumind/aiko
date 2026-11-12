namespace Aiko.Core.Tests;

/// The form of a word after a number: 1 строка, 2 строки, 5 строк.
public class CountFormTests
{
    [Theory]
    [InlineData(1, CountForm.One)]
    [InlineData(21, CountForm.One)]
    [InlineData(101, CountForm.One)]
    [InlineData(2, CountForm.Few)]
    [InlineData(24, CountForm.Few)]
    [InlineData(5, CountForm.Many)]
    [InlineData(11, CountForm.Many)]
    [InlineData(12, CountForm.Many)]
    [InlineData(14, CountForm.Many)]
    [InlineData(111, CountForm.Many)]
    [InlineData(212, CountForm.Many)]
    [InlineData(0, CountForm.Many)]
    public void The_last_digits_choose_the_form(int count, CountForm form) =>
        Assert.Equal(form, CountForms.Of(count));
}
