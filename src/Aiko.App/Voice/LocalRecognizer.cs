using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Aiko.App;

/// whisper small on this computer (D-314). Loaded on the press (0.4 s, inside the time the mic
/// takes to wake up) and freed when the recording is over: the model holds about 870 MB.
sealed unsafe class LocalRecognizer : IDisposable
{
    private readonly IntPtr _context;
    private readonly IntPtr _language;
    private readonly IntPtr _hint;
    private readonly IntPtr _vadModel;

    private LocalRecognizer(IntPtr context, string language, string hint, string vadModel)
    {
        _context = context;
        _language = Marshal.StringToCoTaskMemUTF8(language);
        _hint = hint.Length > 0 ? Marshal.StringToCoTaskMemUTF8(hint) : IntPtr.Zero;
        _vadModel = Marshal.StringToCoTaskMemUTF8(vadModel);
    }

    /// The VAD model ships with the app; without it silence turns into "[Музыка]".
    public static string VadModel => Path.Combine(WhisperNative.Folder, "ggml-silero-v5.1.2.bin");

    /// Null when whisper or a model cannot be loaded.
    public static LocalRecognizer? Open(string model, string language, string hint)
    {
        if (!WhisperNative.Ready || !File.Exists(model) || !File.Exists(VadModel))
        {
            return null;
        }

        WhisperNative.whisper_log_set(&WhisperNative.Quiet, null);
        var parameters = WhisperNative.DefaultContextParams();
        parameters.use_gpu = 0;
        var path = Marshal.StringToCoTaskMemUTF8(model);
        var context = WhisperNative.whisper_init_from_file_with_params(path, parameters);
        Marshal.FreeCoTaskMem(path);
        return context == IntPtr.Zero ? null : new LocalRecognizer(context, language, hint, VadModel);
    }

    /// The text of all the sound given. Takes about 2 s for a phrase on a laptop processor; call it
    /// off the window's thread. Not safe to call from two threads at once.
    public string Read(float[] samples)
    {
        var parameters = WhisperNative.DefaultParams();
        parameters.n_threads = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
        parameters.no_timestamps = 1;
        parameters.print_progress = 0;
        parameters.print_realtime = 0;
        parameters.print_timestamps = 0;
        parameters.language = _language;
        parameters.initial_prompt = _hint;
        parameters.vad = 1;
        parameters.vad_model_path = _vadModel;

        int result;
        fixed (float* sound = samples)
        {
            result = WhisperNative.whisper_full(_context, parameters, sound, samples.Length);
        }

        if (result != 0)
        {
            return "";
        }

        var text = new StringBuilder();
        var segments = WhisperNative.whisper_full_n_segments(_context);
        for (var i = 0; i < segments; i++)
        {
            text.Append(Marshal.PtrToStringUTF8(WhisperNative.whisper_full_get_segment_text(_context, i)));
        }

        return text.ToString().Trim();
    }

    public void Dispose()
    {
        WhisperNative.whisper_free(_context);
        Marshal.FreeCoTaskMem(_language);
        Marshal.FreeCoTaskMem(_hint);
        Marshal.FreeCoTaskMem(_vadModel);
    }
}
