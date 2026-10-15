using System.Text.Json;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// Voice input read from spec/cases, the same tables the Swift tests read.
public class VoiceCaseTests
{
    [Fact]
    public void Every_press_and_cue_leads_where_the_table_says()
    {
        foreach (var row in SpecCases.Json("voice-input").GetProperty("next").EnumerateArray())
        {
            var next = VoiceInput.Next(
                State(row.GetProperty("from")),
                Enum.Parse<VoiceCue>(row.GetProperty("cue").GetString()!),
                Enum.Parse<VoiceReadiness>(row.GetProperty("ready").GetString()!));

            Assert.True(State(row.GetProperty("to")) == next, row.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void Every_phase_shows_the_line_and_button_of_the_table()
    {
        foreach (var row in SpecCases.Json("voice-input").GetProperty("lines").EnumerateArray())
        {
            var line = VoiceInput.Line(
                new VoiceState(Enum.Parse<VoicePhase>(row.GetProperty("phase").GetString()!), VoiceNotice.None),
                TimeSpan.FromSeconds(row.GetProperty("seconds").GetDouble()),
                Enum.Parse<VoiceEngine>(row.GetProperty("engine").GetString()!));

            var where = row.GetProperty("where").GetString();
            Assert.Equal(
                new VoiceLine(
                    Enum.Parse<VoiceWord>(row.GetProperty("word").GetString()!),
                    row.GetProperty("time").GetString(),
                    where is null ? null : Enum.Parse<VoiceEngine>(where),
                    row.GetProperty("cancel").GetBoolean(),
                    Enum.Parse<VoiceButton>(row.GetProperty("button").GetString()!),
                    row.GetProperty("ring").GetBoolean()),
                line);
        }
    }

    [Fact]
    public void The_clock_counts_minutes_and_seconds()
    {
        foreach (var row in SpecCases.Json("voice-input").GetProperty("clock").EnumerateArray())
        {
            Assert.Equal(
                row.GetProperty("text").GetString(),
                VoiceInput.Clock(TimeSpan.FromSeconds(row.GetProperty("seconds").GetDouble())));
        }
    }

    [Fact]
    public void Confirmed_words_grow_and_never_change()
    {
        foreach (var row in SpecCases.Json("dictation").GetProperty("phrases").EnumerateArray())
        {
            var phrases = DictationText.Phrases(row.GetProperty("done").GetString()!, row.GetProperty("reading").GetString()!);
            Assert.Equal(row.GetProperty("confirmed").GetString(), phrases.ConfirmedText);
            Assert.Equal(row.GetProperty("tail").GetString(), phrases.TailText);
        }

        foreach (var run in SpecCases.Json("dictation").GetProperty("runs").EnumerateArray())
        {
            var name = run.GetProperty("name").GetString();
            var text = DictationText.Empty;
            var steps = run.GetProperty("steps").EnumerateArray().ToList();
            var passes = run.GetProperty("passes").EnumerateArray().ToList();
            Assert.Equal(passes.Count, steps.Count);

            for (var i = 0; i < passes.Count; i++)
            {
                text = text.Pass(passes[i].GetString()!);
                Assert.True(steps[i].GetProperty("confirmed").GetString() == text.ConfirmedText, $"{name}, pass {i + 1}: {text.ConfirmedText}");
                Assert.True(steps[i].GetProperty("tail").GetString() == text.TailText, $"{name}, pass {i + 1}: {text.TailText}");
            }

            if (run.GetProperty("final").GetString() is { } final)
            {
                text = text.Final(final);
                Assert.Equal("", text.TailText);
            }

            Assert.True(run.GetProperty("text").GetString() == text.Text, $"{name}: {text.Text}");
        }
    }

    [Fact]
    public void The_hint_names_the_folder_branch_files_and_keys()
    {
        var cases = SpecCases.Json("voice-hint");
        foreach (var row in cases.GetProperty("hints").EnumerateArray())
        {
            var keys = row.GetProperty("keys");
            var hint = VoiceHint.Build(
                row.GetProperty("folder").GetString(),
                row.GetProperty("branch").GetString(),
                row.GetProperty("files").EnumerateArray().Select(f => f.GetString()!),
                keys.ValueKind == JsonValueKind.Null ? null : keys.EnumerateArray().Select(k => k.GetString()!),
                row.GetProperty("max").GetInt32());

            Assert.True(row.GetProperty("hint").GetString() == hint, $"{row.GetProperty("name").GetString()}: {hint}");
        }

        foreach (var row in cases.GetProperty("echoes").EnumerateArray())
        {
            Assert.True(
                row.GetProperty("echo").GetBoolean() == VoiceHint.IsEcho(row.GetProperty("phrase").GetString()!, row.GetProperty("hint").GetString()!),
                row.GetProperty("name").GetString());
        }

        foreach (var row in cases.GetProperty("branches").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("branch").GetString(), VoiceHint.Branch(row.GetProperty("head").GetString()));
        }

        foreach (var row in cases.GetProperty("gitFolders").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("folder").GetString(), VoiceHint.GitFolder(row.GetProperty("file").GetString()));
        }

        foreach (var row in cases.GetProperty("languages").EnumerateArray())
        {
            Assert.Equal(
                row.GetProperty("language").GetString(),
                VoiceHint.Language(
                    Enum.Parse<AikoLanguage>(row.GetProperty("setting").GetString()!),
                    row.GetProperty("system").GetString()!));
        }
    }

    [Fact]
    public void The_model_and_the_key_follow_the_table()
    {
        var cases = SpecCases.Json("voice-model");
        Assert.Equal(VoiceModel.Url, cases.GetProperty("url").GetString());
        Assert.Equal(VoiceModel.Bytes, cases.GetProperty("bytes").GetInt64());
        Assert.Equal(VoiceModel.Sha256, cases.GetProperty("sha256").GetString());
        Assert.Equal(VoiceModel.CloudModel, cases.GetProperty("cloudModel").GetString());

        foreach (var row in cases.GetProperty("megabytes").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("megabytes").GetInt64(), VoiceModel.Megabytes(row.GetProperty("bytes").GetInt64()));
        }

        foreach (var row in cases.GetProperty("readiness").EnumerateArray())
        {
            var bytes = row.GetProperty("modelBytes");
            Assert.Equal(
                Enum.Parse<VoiceReadiness>(row.GetProperty("ready").GetString()!),
                VoiceModel.Readiness(
                    Enum.Parse<VoiceEngine>(row.GetProperty("engine").GetString()!),
                    bytes.ValueKind == JsonValueKind.Null ? null : bytes.GetInt64(),
                    row.GetProperty("keyStored").GetBoolean()));
        }

        foreach (var row in cases.GetProperty("keys").EnumerateArray())
        {
            var key = VoiceModel.CleanKey(row.GetProperty("pasted").GetString());
            Assert.Equal(row.GetProperty("key").GetString(), key);
            Assert.Equal(row.GetProperty("mask").GetString(), key is null ? null : VoiceModel.MaskKey(key));
        }
    }

    /// The files of a conversation for the hint: read or changed, newest first, once each. A
    /// search, a command and a picture the person attached are not files the person names.
    [Fact]
    public void The_hint_takes_the_files_a_conversation_read_or_changed()
    {
        static FeedItem.Step Step(int id, StepKind kind, string subject, bool hidden = false) =>
            new(id, $"t{id}", new StepLine(kind, "", subject, 0, 0, hidden), StepState.Done, "", [], "");

        List<FeedItem> items =
        [
            new FeedItem.OwnerMessage(1, "Look at the field.", false, []),
            Step(2, StepKind.Read, "TalkInput.cs"),
            Step(3, StepKind.Search, "VoiceLine"),
            Step(4, StepKind.Edit, "VoiceInput.cs"),
            Step(5, StepKind.Run, "dotnet test"),
            Step(6, StepKind.Read, "image.png", hidden: true),
            Step(7, StepKind.Read, "talkinput.cs"),
            Step(8, StepKind.Edit, ""),
        ];

        Assert.Equal(["talkinput.cs", "VoiceInput.cs"], VoiceHint.Files(items));
        Assert.Empty(VoiceHint.Files([]));
    }

    private static VoiceState State(JsonElement pair) => new(
        Enum.Parse<VoicePhase>(pair[0].GetString()!),
        Enum.Parse<VoiceNotice>(pair[1].GetString()!));
}
