using System.Diagnostics;
using System.Text;

namespace Umbra.Core;

// Synthèse vocale coréenne locale (Piper, hors-ligne) - portage direct de
// src/lib/piperTts.js côté Electron, même technique de worker persistant :
// passé la première synthèse, on garde un process piper vivant pour éviter
// de recharger le modèle onnx (~60 Mo, ~0.6s) à chaque appel - un mot
// suivant devient ~10x plus rapide (~70-150ms au lieu de ~700-900ms). On
// coupe le process après quelques minutes d'inactivité pour ne pas garder
// ce poids en mémoire toute la journée (l'app tourne en tray la plupart du
// temps sans qu'on écoute rien).
public static class PiperTts
{
    private const int IdleTimeoutMs = 5 * 60 * 1000;

    private static string PiperExe => Path.Combine(Config.PiperDir, "piper.exe");
    private static string ModelPath => Path.Combine(Config.PiperDir, "ko_KR-kss-medium.onnx");

    public static bool IsAvailable() => File.Exists(PiperExe) && File.Exists(ModelPath);

    private static string ToForwardSlashes(string p) => p.Replace('\\', '/');

    private class WorkerState
    {
        public required Process Proc;
        public readonly Queue<TaskCompletionSource<string>> Queue = new();
        public readonly StringBuilder StdoutBuffer = new();
        public Timer? IdleTimer;
        public bool Dead;
    }

    private static WorkerState? _worker;
    private static readonly object Lock = new();

    private static void ClearIdleTimer(WorkerState state)
    {
        state.IdleTimer?.Dispose();
        state.IdleTimer = null;
    }

    private static void ScheduleIdleShutdown(WorkerState state)
    {
        ClearIdleTimer(state);
        state.IdleTimer = new Timer(_ =>
        {
            lock (Lock)
            {
                if (_worker == state) _worker = null;
            }
            TryKill(state);
        }, null, IdleTimeoutMs, Timeout.Infinite);
    }

    private static void TryKill(WorkerState state)
    {
        try
        {
            if (!state.Proc.HasExited)
            {
                state.Proc.StandardInput.Close();
                state.Proc.Kill();
            }
        }
        catch
        {
            // déjà mort : on ignore
        }
    }

    private static void FailAll(WorkerState state, Exception err)
    {
        lock (Lock)
        {
            state.Dead = true;
            while (state.Queue.Count > 0) state.Queue.Dequeue().TrySetException(err);
            ClearIdleTimer(state);
            if (_worker == state) _worker = null;
        }
    }

    private static WorkerState StartWorker()
    {
        var psi = new ProcessStartInfo
        {
            FileName = PiperExe,
            WorkingDirectory = Config.PiperDir,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(ModelPath);
        psi.ArgumentList.Add("--json-input");

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var state = new WorkerState { Proc = proc };

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (Lock)
            {
                var line = e.Data.Trim();
                if (line.Length == 0) return;
                if (state.Queue.Count > 0) state.Queue.Dequeue().TrySetResult(line);
            }
        };
        proc.Exited += (_, _) => FailAll(state, new IOException("piper process exited"));

        proc.Start();
        proc.BeginOutputReadLine();
        return state;
    }

    public static async Task<byte[]> SpeakAsync(string text)
    {
        if (!IsAvailable()) throw new FileNotFoundException("piper binary or model not found");

        WorkerState state;
        TaskCompletionSource<string> tcs;
        lock (Lock)
        {
            if (_worker == null || _worker.Dead) _worker = StartWorker();
            state = _worker;
            ClearIdleTimer(state);
            tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            state.Queue.Enqueue(tcs);
        }

        var outPath = ToForwardSlashes(Path.Combine(Path.GetTempPath(), $"umbra-piper-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Random.Shared.Next():x}.wav"));
        var line = System.Text.Json.JsonSerializer.Serialize(new { text, output_file = outPath });
        await state.Proc.StandardInput.WriteLineAsync(line);
        await state.Proc.StandardInput.FlushAsync();

        try
        {
            var returnedPath = await tcs.Task;
            var buf = await File.ReadAllBytesAsync(returnedPath);
            try { File.Delete(returnedPath); } catch { /* pas grave */ }
            ScheduleIdleShutdown(state);
            return buf;
        }
        catch
        {
            ScheduleIdleShutdown(state);
            throw;
        }
    }

    public static void Shutdown()
    {
        WorkerState? state;
        lock (Lock)
        {
            state = _worker;
            _worker = null;
        }
        if (state == null) return;
        ClearIdleTimer(state);
        TryKill(state);
    }
}
