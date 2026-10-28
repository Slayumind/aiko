using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Media;
using Windows.Win32.Media.Audio;

namespace Aiko.App;

/// The default microphone of Windows while a recording goes on: 16 kHz, one channel, the format
/// whisper reads. It is opened on the press and closed on the second press; between recordings
/// nothing of it exists (Aiko is not seen by the system).
///
/// waveIn and not WASAPI: it gives the format whisper wants without a resampler, and the default
/// device follows the choice in the Windows settings.
sealed unsafe class Microphone : IDisposable
{
    private const int Rate = 16000;

    /// Four buffers of 100 ms: the driver fills one while we read another.
    private const int Buffers = 4;
    private const int BufferBytes = Rate / 10 * 2;

    /// Below this a sample is digital silence. The Windows mic gives exact zeros for about 0.4 s
    /// after it opens (spike 2026-10-15); "listening" waits for the first real sound.
    private const short Silence = 2;

    private readonly List<float> _samples = new(Rate * 30);
    private readonly Lock _lock = new();
    private readonly AutoResetEvent _filled = new(false);
    private readonly WAVEHDR*[] _headers = new WAVEHDR*[Buffers];
    private HWAVEIN _device;
    private Thread? _reader;
    private volatile bool _stopping;
    private bool _heard;
    private double _level;

    /// Raised once, on the reading thread, when the first sound that is not silence comes.
    public event Action? FirstSound;

    /// Raised on the reading thread when the device stops giving sound: unplugged mid-sentence.
    public event Action? Lost;

    /// How loud the last tenth of a second was, from 0 to 1.
    public double Level => Volatile.Read(ref _level);

    /// False when there is no microphone or Windows does not let Aiko use it.
    public bool Start()
    {
        var format = new WAVEFORMATEX
        {
            wFormatTag = (ushort)PInvoke.WAVE_FORMAT_PCM,
            nChannels = 1,
            nSamplesPerSec = Rate,
            nAvgBytesPerSec = Rate * 2,
            nBlockAlign = 2,
            wBitsPerSample = 16,
        };

        HWAVEIN device;
        var opened = PInvoke.waveInOpen(&device, PInvoke.WAVE_MAPPER, &format,
            (nuint)_filled.SafeWaitHandle.DangerousGetHandle(), 0, MIDI_WAVE_OPEN_TYPE.CALLBACK_EVENT);
        if (opened != 0)
        {
            return false;
        }

        _device = device;
        for (var i = 0; i < Buffers; i++)
        {
            var header = (WAVEHDR*)NativeMemory.AllocZeroed((nuint)sizeof(WAVEHDR));
            header->lpData = new Windows.Win32.Foundation.PSTR((byte*)NativeMemory.AllocZeroed(BufferBytes));
            header->dwBufferLength = BufferBytes;
            _headers[i] = header;
            PInvoke.waveInPrepareHeader(_device, header, (uint)sizeof(WAVEHDR));
            PInvoke.waveInAddBuffer(_device, header, (uint)sizeof(WAVEHDR));
        }

        if (PInvoke.waveInStart(_device) != 0)
        {
            Close();
            return false;
        }

        _reader = new Thread(Read) { IsBackground = true, Name = "Aiko microphone" };
        _reader.Start();
        return true;
    }

    /// Everything heard so far, from the press.
    public float[] Heard()
    {
        lock (_lock)
        {
            return [.. _samples];
        }
    }

    public void Dispose()
    {
        if (_device.IsNull)
        {
            _filled.Dispose();
            return;
        }

        _stopping = true;
        PInvoke.waveInReset(_device);
        _filled.Set();
        _reader?.Join();
        Close();
    }

    private void Read()
    {
        while (!_stopping)
        {
            // A driver that stops calling back is a device that went away.
            if (!_filled.WaitOne(TimeSpan.FromSeconds(2)) && !_stopping)
            {
                Lost?.Invoke();
                return;
            }

            foreach (var header in _headers)
            {
                if ((header->dwFlags & PInvoke.WHDR_DONE) == 0)
                {
                    continue;
                }

                Take(new ReadOnlySpan<short>(header->lpData.Value, (int)header->dwBytesRecorded / 2));
                header->dwFlags &= ~PInvoke.WHDR_DONE;
                if (!_stopping)
                {
                    PInvoke.waveInAddBuffer(_device, header, (uint)sizeof(WAVEHDR));
                }
            }
        }
    }

    private void Take(ReadOnlySpan<short> pcm)
    {
        var first = false;
        var energy = 0.0;
        lock (_lock)
        {
            foreach (var sample in pcm)
            {
                _samples.Add(sample / 32768f);
                energy += (double)sample * sample;
                if (!_heard && Math.Abs((int)sample) > Silence)
                {
                    _heard = first = true;
                }
            }
        }

        // Speech sits around a tenth of full scale; times eight makes a normal voice fill the ring.
        var loudness = pcm.Length == 0 ? 0 : Math.Sqrt(energy / pcm.Length) / 32768;
        Volatile.Write(ref _level, Math.Min(1, loudness * 8));

        if (first)
        {
            FirstSound?.Invoke();
        }
    }

    private void Close()
    {
        foreach (var header in _headers)
        {
            if (header is null)
            {
                continue;
            }

            PInvoke.waveInUnprepareHeader(_device, header, (uint)sizeof(WAVEHDR));
            NativeMemory.Free(header->lpData.Value);
            NativeMemory.Free(header);
        }

        Array.Clear(_headers);
        PInvoke.waveInClose(_device);
        _device = default;
        _filled.Dispose();
    }
}
