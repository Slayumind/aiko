using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// Every way out that stops the sessions asks with words of its own (D-351).
public class StopQuestionTests
{
    [Fact]
    public void Every_reason_has_its_own_words()
    {
        var questions = Enum.GetValues<StopReason>().Select(StopQuestion.For).ToList();

        Assert.Equal(questions.Count, questions.Select(question => question.Body).Distinct().Count());
        Assert.All(questions, question => Assert.NotEqual(question.Stop, question.Keep));
        Assert.All(questions, question => Assert.Equal(Strings.TerminalQuitTitle, question.Title));
    }

    [Fact]
    public void Only_removing_one_environment_leaves_the_other_sessions()
    {
        Assert.Equal(
            [StopReason.RemoveEnvironment],
            Enum.GetValues<StopReason>().Where(reason => !StopQuestion.EndsEverySession(reason)));
    }

    [Fact]
    public void Quit_keeps_the_words_it_had()
    {
        var quit = StopQuestion.For(StopReason.Quit);

        Assert.Equal(Strings.TerminalQuitBody, quit.Body);
        Assert.Equal(Strings.TerminalStopAndQuit, quit.Stop);
        Assert.Equal(Strings.TerminalDontQuit, quit.Keep);
    }
}
