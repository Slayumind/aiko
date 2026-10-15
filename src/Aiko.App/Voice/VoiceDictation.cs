using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// What the hint of a recording is made of: the session it starts in (D-313).
sealed record VoiceContext(string Folder, IReadOnlyList<string> Files, bool MemoryOn);

/// Voice input of the terminal window: the press, the mic, the recogniser and the field.
///
/// The rules are the core's (VoiceInput, DictationText); this class only moves sound and text
/// between threads. The mic reads on its own thread, the recogniser runs on a worker, and every
/// change to the field happens on the window's thread. Nothing of it lives between recordings:
/// the mic is closed and the model is freed.
sealed class VoiceDictation
{
    /// The recogniser waits for this much sound before its first pass, and between passes when
    /// the last one came back at once (a pass over silence takes a few milliseconds with VAD).
    private static readonly TimeSpan FirstSound = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan Breath = TimeSpan.FromMilliseconds(250);

    private readonly TalkInput _input;
    private readonly Func<VoiceContext?> _context;
    private readonly Dispatcher _window;
    private readonly DispatcherTimer _tick;
    private readonly Stopwatch _listened = new();
    private VoiceState _state = VoiceState.For(false);
    private AppSettings _settings = AppSettings.Default;
    private Recording? _recording;
    private DictationText _text = DictationText.Empty;

    public VoiceDictation(TalkInput input, Func<VoiceContext?> context)
    {
        _input = input;
        _context = context;
        _window = input.Dispatcher;

        // Only while a recording goes on: the ring follows the sound and the clock counts.
        _tick = new DispatcherTimer(TimeSpan.FromMilliseconds(80), DispatcherPriority.Render, (_, _) => Tick(), _window);
        _tick.Stop();

        input.VoicePressed += Press;
        input.VoiceCancelled += () => Cue(VoiceCue.Cancel);
        Show();
    }

    /// Opens the page of the settings where voice input is set up.
    public event Action? SettingsAsked
    {
        add => _input.VoiceSettingsAsked += value;
        remove => _input.VoiceSettingsAsked -= value;
    }

    /// The settings changed: voice input was turned on or off, or the engine changed.
    public void Use(AppSettings settings)
    {
        _settings = settings;
        if (!_state.Recording && _state.Phase != VoicePhase.Finishing)
        {
            _state = VoiceState.For(settings.VoiceInput);
        }

        Show();
    }

    /// The window closes: whatever was being said is dropped.
    public void Close()
    {
        if (_state.Phase != VoicePhase.Off)
        {
            Cue(VoiceCue.Cancel);
        }
    }

    private VoiceReadiness Readiness()
    {
        var ready = VoiceModel.Readiness(_settings.VoiceEngine, ModelDownload.ModelBytes, VoiceKey.Stored);

        // A model on disk is no use without the engine that reads it.
        return ready == VoiceReadiness.Ready && _settings.VoiceEngine == VoiceEngine.Local && !WhisperNative.Ready
            ? VoiceReadiness.NeedsModel
            : ready;
    }

    private void Press()
    {
        var before = _state.Phase;
        _state = VoiceInput.Next(_state, VoiceCue.Press, before == VoicePhase.Idle ? Readiness() : VoiceReadiness.Ready);

        if (before == VoicePhase.Idle && _state.Phase == VoicePhase.Warming)
        {
            Start();
        }
        else if (_state.Phase == VoicePhase.Finishing)
        {
            _recording?.Finish();
        }

        Show();
    }

    private void Cue(VoiceCue cue)
    {
        var before = _state.Phase;
        _state = VoiceInput.Next(_state, cue, VoiceReadiness.Ready);

        if (cue == VoiceCue.FirstSound && _state.Phase == VoicePhase.Listening && before == VoicePhase.Warming)
        {
            _listened.Restart();
        }

        if (_state.Phase == VoicePhase.Idle && before != VoicePhase.Idle)
        {
            // Esc, a lost mic, or the end: the recording is over either way.
            var dropped = cue != VoiceCue.Finished;
            _recording?.Drop();
            _recording = null;
            _input.EndDictation(dropped ? null : _text.Text);
        }

        Show();
    }

    private void Start()
    {
        var context = _context();
        var hint = context is null
            ? ""
            : VoiceHint.Build(context.Folder, BranchOf(context.Folder), context.Files, context.MemoryOn ? MemoryKeys() : null);
        var language = VoiceHint.Language(_settings.Language, CultureInfo.CurrentUICulture.Name);

        _text = DictationText.Empty;
        _input.BeginDictation();
        _recording = new Recording(this, _settings.VoiceEngine, language, hint);
        if (!_recording.Open())
        {
            Cue(VoiceCue.NoMicrophone);
        }
    }

    private void Show()
    {
        _input.ShowVoice(VoiceInput.Line(_state, _listened.Elapsed, _settings.VoiceEngine), _state.Notice);
        var moving = _state.Recording;
        if (moving && !_tick.IsEnabled)
        {
            _tick.Start();
        }
        else if (!moving && _tick.IsEnabled)
        {
            _tick.Stop();
            _input.SetLevel(0);
        }
    }

    private void Tick()
    {
        _input.SetLevel(_recording?.Level ?? 0);
        Show();
    }

    // ---- what the worker and the mic report, brought to the window's thread ----

    private void Heard(Recording from) => _window.BeginInvoke(() =>
    {
        if (from == _recording)
        {
            Cue(VoiceCue.FirstSound);
        }
    });

    private void Lost(Recording from) => _window.BeginInvoke(() =>
    {
        if (from == _recording)
        {
            Cue(VoiceCue.NoMicrophone);
        }
    });

    private void Passed(Recording from, string text, bool final) => _window.BeginInvoke(() =>
    {
        if (from != _recording)
        {
            return;
        }

        _text = final ? _text.Final(text) : _text.Pass(text);
        _input.ShowDictation(_text);
        if (final)
        {
            Cue(VoiceCue.Finished);
        }
    });

    /// Text from OpenAI: the phrases that are done and the one being read (D-317).
    private void Phrased(Recording from, string done, string reading, bool final) => _window.BeginInvoke(() =>
    {
        if (from != _recording)
        {
            return;
        }

        _text = DictationText.Phrases(done, reading);
        _input.ShowDictation(_text);
        if (final)
        {
            Cue(VoiceCue.Finished);
        }
    });

    private void Failed(Recording from) => _window.BeginInvoke(() =>
    {
        if (from == _recording)
        {
            Cue(VoiceCue.CloudFailed);
        }
    });

    private static string? BranchOf(string folder)
    {
        try
        {
            var dotGit = Path.Combine(folder, ".git");
            var gitFolder = File.Exists(dotGit) ? VoiceHint.GitFolder(File.ReadAllText(dotGit)) : dotGit;
            var head = gitFolder is null ? null : Path.Combine(folder, gitFolder, "HEAD");
            return head is not null && File.Exists(head) ? VoiceHint.Branch(File.ReadAllText(head)) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static IEnumerable<string> MemoryKeys() => MemoryStore.Load().Facts.SelectMany(f => f.Keys);

    /// One recording: the open mic and the worker that reads it. Dropped or finished, it is done;
    /// the next press makes a new one, so a late answer of an old one is easy to tell apart.
    private sealed class Recording(VoiceDictation owner, VoiceEngine engine, string language, string hint)
    {
        private readonly Microphone _mic = new();
        private readonly CancellationTokenSource _dropped = new();
        private volatile bool _finishing;

        public double Level => _mic.Level;

        public bool Open()
        {
            _mic.FirstSound += () => owner.Heard(this);
            _mic.Lost += () => owner.Lost(this);
            if (!_mic.Start())
            {
                _mic.Dispose();
                return false;
            }

            _ = engine == VoiceEngine.OpenAI ? Task.Run(WorkWithOpenAI) : Task.Run(WorkOnComputer);
            return true;
        }

        /// The second press: the mic closes now, the worker reads what was said to the end.
        public void Finish()
        {
            _finishing = true;
            _mic.Dispose();
        }

        public void Drop()
        {
            _dropped.Cancel();
            _mic.Dispose();
        }

        private void WorkOnComputer()
        {
            using var recognizer = LocalRecognizer.Open(ThisComputer.Folders.VoiceModelFile, language, hint);
            if (recognizer is null)
            {
                Log.Write("voice input: the speech model did not load");
                owner._window.BeginInvoke(() => owner.Cue(VoiceCue.Cancel));
                return;
            }

            while (!_finishing && !_dropped.IsCancellationRequested)
            {
                var heard = _mic.Heard();
                if (heard.Length < Rate(FirstSound))
                {
                    Thread.Sleep(Breath);
                    continue;
                }

                var started = Stopwatch.StartNew();
                var text = Unechoed(recognizer.Read(heard));
                if (!_finishing && !_dropped.IsCancellationRequested)
                {
                    owner.Passed(this, text, final: false);
                }

                if (started.Elapsed < Breath)
                {
                    Thread.Sleep(Breath - started.Elapsed);
                }
            }

            if (!_dropped.IsCancellationRequested)
            {
                owner.Passed(this, Unechoed(recognizer.Read(_mic.Heard())), final: true);
            }
        }

        /// The sound goes to OpenAI as it comes, a tenth of a second at a time; the text comes back
        /// phrase by phrase (D-317).
        private async Task WorkWithOpenAI()
        {
            await using var cloud = await CloudRecognizer.Open(hint, _dropped.Token).ConfigureAwait(false);
            if (cloud is null)
            {
                owner.Failed(this);
                return;
            }

            cloud.Changed += (done, reading) =>
            {
                if (!_dropped.IsCancellationRequested)
                {
                    owner.Phrased(this, done, reading, final: false);
                }
            };

            var sent = 0;
            while (!_dropped.IsCancellationRequested && cloud.Problem is null)
            {
                var finishing = _finishing;
                var heard = _mic.Heard();
                if (heard.Length > sent)
                {
                    await cloud.Append(heard.AsSpan(sent)).ConfigureAwait(false);
                    sent = heard.Length;
                }

                if (finishing)
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            if (_dropped.IsCancellationRequested)
            {
                return;
            }

            var text = cloud.Problem is null ? await cloud.Finish().ConfigureAwait(false) : null;
            if (text is null)
            {
                owner.Failed(this);
            }
            else
            {
                owner.Phrased(this, text, "", final: true);
            }
        }

        /// whisper may give the hint back over noise as well; such a pass counts as silence.
        private string Unechoed(string text) => VoiceHint.IsEcho(text, hint) ? "" : text;

        private static int Rate(TimeSpan time) => (int)(16000 * time.TotalSeconds);
    }
}
