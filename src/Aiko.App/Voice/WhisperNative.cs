using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Aiko.App;

/// The part of whisper.h (whisper.cpp 1.9.5, release b5454) that Aiko calls.
///
/// The structs are written by hand from the header. A field in the wrong place does not fail to
/// build, it reads garbage, so Check() compares the defaults whisper.dll gives with the header
/// before the first use (spike 2026-10-15: 304 and 48 bytes, every default matched).
static unsafe class WhisperNative
{
    /// The dlls ship in a folder of their own next to Aiko.App.exe; tools/fetch-whisper.ps1 puts them there.
    public static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "whisper");

    private static readonly Lazy<bool> Loaded = new(Load);

    /// Whether whisper.dll is there, loads, and has the layout this code was written for.
    public static bool Ready => Loaded.Value;

    private static bool Load()
    {
        if (!File.Exists(Path.Combine(Folder, "whisper.dll")))
        {
            return false;
        }

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(WhisperNative).Assembly, (name, _, _) =>
                name is "whisper" or "ggml" ? NativeLibrary.Load(Path.Combine(Folder, name + ".dll")) : IntPtr.Zero);

            // The processor code of ggml sits in its own dlls, one per instruction set. Without
            // this the model load stops on GGML_ASSERT(device).
            var folder = Marshal.StringToCoTaskMemUTF8(Folder);
            ggml_backend_load_all_from_path(folder);
            Marshal.FreeCoTaskMem(folder);
            return Check();
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Log.Write($"whisper did not load: {exception.GetType().Name}");
            return false;
        }
    }

    private static bool Check()
    {
        var full = whisper_full_default_params_by_ref(0);
        var context = whisper_context_default_params_by_ref();
        var same = Marshal.PtrToStringUTF8(whisper_version()) == "1.9.5"
            && full->n_max_text_ctx == 16384
            && full->greedy_best_of == 5
            && Math.Abs(full->temperature_inc - 0.2f) < 1e-6
            && Math.Abs(full->no_speech_thold - 0.6f) < 1e-6
            && Math.Abs(full->vad_params.samples_overlap - 0.1f) < 1e-6
            && context->dtw_mem_size == 128 * 1024 * 1024;
        whisper_free_params(full);
        whisper_free_context_params(context);
        if (!same)
        {
            Log.Write("whisper.dll is not the version Aiko was written for");
        }

        return same;
    }

    public static FullParams DefaultParams()
    {
        var pointer = whisper_full_default_params_by_ref(0);
        var copy = *pointer;
        whisper_free_params(pointer);
        return copy;
    }

    public static ContextParams DefaultContextParams()
    {
        var pointer = whisper_context_default_params_by_ref();
        var copy = *pointer;
        whisper_free_context_params(pointer);
        return copy;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VadParams
    {
        public float threshold;
        public int min_speech_duration_ms;
        public int min_silence_duration_ms;
        public float max_speech_duration_s;
        public int speech_pad_ms;
        public float samples_overlap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ContextParams
    {
        public byte use_gpu;
        public byte flash_attn;
        public int gpu_device;
        public byte dtw_token_timestamps;
        public int dtw_aheads_preset;
        public int dtw_n_top;
        public nuint dtw_n_heads;
        public IntPtr dtw_heads;
        public nuint dtw_mem_size;
    }

    /// C bool is one byte, so every flag here is a byte.
    [StructLayout(LayoutKind.Sequential)]
    public struct FullParams
    {
        public int strategy;
        public int n_threads, n_max_text_ctx, offset_ms, duration_ms;
        public byte translate, no_context, no_timestamps, single_segment, print_special, print_progress, print_realtime, print_timestamps;
        public byte token_timestamps;
        public float thold_pt, thold_ptsum;
        public int max_len;
        public byte split_on_word;
        public int max_tokens;
        public byte debug_mode;
        public int audio_ctx;
        public byte tdrz_enable;
        public IntPtr suppress_regex;
        public IntPtr initial_prompt;
        public byte carry_initial_prompt;
        public IntPtr prompt_tokens;
        public int prompt_n_tokens;
        public IntPtr language;
        public byte detect_language;
        public byte suppress_blank, suppress_nst;
        public float temperature, max_initial_ts, length_penalty;
        public float temperature_inc, entropy_thold, logprob_thold, no_speech_thold;
        public int greedy_best_of;
        public int beam_size;
        public float beam_patience;
        public IntPtr new_segment_callback, new_segment_callback_user_data;
        public IntPtr progress_callback, progress_callback_user_data;
        public IntPtr encoder_begin_callback, encoder_begin_callback_user_data;
        public IntPtr abort_callback, abort_callback_user_data;
        public IntPtr logits_filter_callback, logits_filter_callback_user_data;
        public IntPtr grammar_rules;
        public nuint n_grammar_rules, i_start_rule;
        public float grammar_penalty;
        public byte vad;
        public IntPtr vad_model_path;
        public VadParams vad_params;
    }

    /// whisper writes every step of a model load to stderr. A tray app has nowhere to show it.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static void Quiet(int level, byte* text, void* data)
    {
    }

    [DllImport("ggml")] private static extern void ggml_backend_load_all_from_path(IntPtr folder);
    [DllImport("whisper")] private static extern IntPtr whisper_version();
    [DllImport("whisper")] private static extern FullParams* whisper_full_default_params_by_ref(int strategy);
    [DllImport("whisper")] private static extern void whisper_free_params(FullParams* parameters);
    [DllImport("whisper")] private static extern ContextParams* whisper_context_default_params_by_ref();
    [DllImport("whisper")] private static extern void whisper_free_context_params(ContextParams* parameters);
    [DllImport("whisper")] public static extern void whisper_log_set(delegate* unmanaged[Cdecl]<int, byte*, void*, void> callback, void* data);
    [DllImport("whisper")] public static extern IntPtr whisper_init_from_file_with_params(IntPtr path, ContextParams parameters);
    [DllImport("whisper")] public static extern int whisper_full(IntPtr context, FullParams parameters, float* samples, int count);
    [DllImport("whisper")] public static extern int whisper_full_n_segments(IntPtr context);
    [DllImport("whisper")] public static extern IntPtr whisper_full_get_segment_text(IntPtr context, int segment);
    [DllImport("whisper")] public static extern void whisper_free(IntPtr context);
}
