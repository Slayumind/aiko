using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// Voice input with the person's own OpenAI key (D-317): the Realtime API in transcription mode.
///
/// The sound goes in pieces of a tenth of a second while the person speaks; the server cuts it at
/// pauses and sends the text of each phrase when the phrase is over. The text never goes to the
/// log; the key is read from Credential Manager right before the socket opens and goes nowhere
/// but the header of that request.
sealed class CloudRecognizer : IAsyncDisposable
{
    private static readonly Uri Address = new("wss://api.openai.com/v1/realtime?intent=transcription");

    /// How long the last phrase may take after the second press (0.9 s in the spike).
    private static readonly TimeSpan LastPhrase = TimeSpan.FromSeconds(10);

    private readonly ClientWebSocket _socket;
    private readonly CancellationTokenSource _closing = new();
    private readonly Task _listening;
    private readonly StringBuilder _done = new();
    private readonly StringBuilder _reading = new();
    private readonly Lock _lock = new();
    private readonly TaskCompletionSource _allIn = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _waiting;
    private bool _speaking;
    private bool _finishing;

    private CloudRecognizer(ClientWebSocket socket)
    {
        _socket = socket;
        _listening = Task.Run(Listen);
    }

    /// The phrases that are done and the one being read, on a thread of the pool.
    public event Action<string, string>? Changed;

    /// Set when OpenAI refused or the connection broke: a short reason for the log, never the text.
    public string? Problem { get; private set; }

    public string Done
    {
        get
        {
            lock (_lock)
            {
                return _done.ToString();
            }
        }
    }

    /// Null when no key is stored or the socket did not open. key is for checks only; the app
    /// always reads the stored one.
    public static async Task<CloudRecognizer?> Open(string language, string hint, CancellationToken cancel, string? key = null)
    {
        if ((key ?? VoiceKey.Load()) is not { } bearer)
        {
            return null;
        }

        ClientWebSocket socket;
        try
        {
            socket = await AikoHttp.Socket(Address, bearer, cancel).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is WebSocketException or InvalidOperationException or OperationCanceledException)
        {
            Log.Write($"voice input: OpenAI did not open: {exception.GetType().Name} {exception.InnerException?.GetType().Name} {exception.InnerException?.Message}");
            return null;
        }

        var recognizer = new CloudRecognizer(socket);
        await recognizer.Send(new JsonObject
        {
            ["type"] = "session.update",
            ["session"] = new JsonObject
            {
                ["type"] = "transcription",
                ["audio"] = new JsonObject
                {
                    ["input"] = new JsonObject
                    {
                        ["format"] = new JsonObject { ["type"] = "audio/pcm", ["rate"] = 24000 },
                        ["transcription"] = new JsonObject
                        {
                            ["model"] = VoiceModel.CloudModel,
                            ["prompt"] = hint,
                            ["language"] = language,
                        },
                        ["turn_detection"] = new JsonObject
                        {
                            ["type"] = "server_vad",
                            ["threshold"] = 0.5,
                            ["prefix_padding_ms"] = 300,
                            ["silence_duration_ms"] = 500,
                        },
                    },
                },
            },
        }).ConfigureAwait(false);
        return recognizer;
    }

    /// Sound from the mic, 16 kHz: OpenAI takes 16-bit PCM at 24 kHz.
    public Task Append(ReadOnlySpan<float> sound) =>
        Send(new JsonObject { ["type"] = "input_audio_buffer.append", ["audio"] = Convert.ToBase64String(To24k(sound)) });

    /// After the second press: the phrase still being spoken is cut, and the text of every phrase
    /// is awaited. Null when OpenAI failed.
    public async Task<string?> Finish()
    {
        bool cut;
        lock (_lock)
        {
            _finishing = true;
            cut = _speaking;
            if (!cut && _waiting == 0)
            {
                _allIn.TrySetResult();
            }
        }

        if (cut)
        {
            await Send(new JsonObject { ["type"] = "input_audio_buffer.commit" }).ConfigureAwait(false);
        }

        await Task.WhenAny(_allIn.Task, Task.Delay(LastPhrase)).ConfigureAwait(false);
        return Problem is null && _allIn.Task.IsCompleted ? Done.Trim() : null;
    }

    public async ValueTask DisposeAsync()
    {
        _closing.Cancel();
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
        }

        await _listening.ConfigureAwait(false);
        _socket.Dispose();
    }

    private async Task Send(JsonObject message)
    {
        if (_socket.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, _closing.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
            Fail(exception.GetType().Name);
        }
    }

    private async Task Listen()
    {
        var buffer = new byte[64 * 1024];
        var message = new MemoryStream();
        try
        {
            while (_socket.State == WebSocketState.Open && !_closing.IsCancellationRequested)
            {
                var part = await _socket.ReceiveAsync(buffer, _closing.Token).ConfigureAwait(false);
                if (part.MessageType == WebSocketMessageType.Close)
                {
                    if (!_closing.IsCancellationRequested)
                    {
                        Fail($"closed {part.CloseStatus}");
                    }

                    return;
                }

                message.Write(buffer, 0, part.Count);
                if (part.EndOfMessage)
                {
                    Read(message.ToArray());
                    message.SetLength(0);
                }
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
            if (!_closing.IsCancellationRequested)
            {
                Fail(exception.GetType().Name);
            }
        }
    }

    private void Read(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var type = root.TryGetProperty("type", out var named) ? named.GetString() : null;
        string? done = null;
        string? reading = null;

        lock (_lock)
        {
            switch (type)
            {
                case "input_audio_buffer.speech_started":
                    _speaking = true;
                    break;
                case "input_audio_buffer.speech_stopped":
                    _speaking = false;
                    break;
                case "input_audio_buffer.committed":
                    _speaking = false;
                    _waiting++;
                    break;
                case "conversation.item.input_audio_transcription.delta":
                    _reading.Append(root.GetProperty("delta").GetString());
                    break;
                case "conversation.item.input_audio_transcription.completed":
                    _done.Append(root.GetProperty("transcript").GetString()).Append(' ');
                    _reading.Clear();
                    _waiting = Math.Max(0, _waiting - 1);
                    if (_finishing && _waiting == 0)
                    {
                        _allIn.TrySetResult();
                    }

                    break;
                case "conversation.item.input_audio_transcription.failed" or "error":
                    var code = root.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var c) ? c.GetString() : type;

                    // The server had cut the last phrase itself a moment before the commit.
                    if (code == "input_audio_buffer_commit_empty")
                    {
                        if (_waiting == 0)
                        {
                            _allIn.TrySetResult();
                        }

                        return;
                    }

                    Fail(code ?? "error");
                    return;
                default:
                    return;
            }

            done = _done.ToString();
            reading = _reading.ToString();
        }

        Changed?.Invoke(done, reading);
    }

    private void Fail(string reason)
    {
        Problem ??= reason;
        Log.Write($"voice input: OpenAI failed: {reason}");
        _allIn.TrySetResult();
    }

    /// 16 kHz to 24 kHz by straight lines between samples, and to 16-bit little endian.
    private static byte[] To24k(ReadOnlySpan<float> sound)
    {
        var count = sound.Length * 3 / 2;
        var bytes = new byte[count * 2];
        for (var i = 0; i < count; i++)
        {
            var x = i / 1.5;
            var j = (int)x;
            var f = x - j;
            var a = sound[Math.Min(j, sound.Length - 1)];
            var b = sound[Math.Min(j + 1, sound.Length - 1)];
            var value = (short)Math.Clamp((a * (1 - f) + b * f) * 32767, short.MinValue, short.MaxValue);
            bytes[i * 2] = (byte)value;
            bytes[i * 2 + 1] = (byte)(value >> 8);
        }

        return bytes;
    }
}
