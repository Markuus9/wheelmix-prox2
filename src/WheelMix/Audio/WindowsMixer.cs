using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ControlAudioLogitech;
interface IMixer : IDisposable {
    Task<double> Read();
    Task Write(double balance);
    string Description { get; }
}
sealed class WindowsMixer : IMixer
{
    readonly MMDeviceEnumerator enumerator = new();
    readonly HashSet<string> chat;
    readonly Dictionary<string, SessionLevel> levels = new();
    readonly Action<string> log;
    readonly int? onlyProcess;
    double balance;
    int chatCount, gameCount;
    public List<string> ChatNames { get; } = new();
    public List<string> GameNames { get; } = new();
    sealed class SessionLevel {
        public float Baseline, Last;
        public double Factor;
        public SessionLevel(float value) { Baseline = Last = value; Factor = 1; }
    }
    public string Description => $"Windows · Chat: {chatCount} sesiones · Game: {gameCount} sesiones";
    public WindowsMixer(string names, Action<string> log, int? onlyProcess = null) {
        chat = names.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s[..^4] : s)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        this.log = log; this.onlyProcess = onlyProcess;
    }
    public Task<double> Read() { Apply(balance); return Task.FromResult(balance); }
    public Task Write(double value) {
        if (!double.IsFinite(value) || value < -1 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
        Apply(value); balance = value; return Task.CompletedTask;
    }
    internal static double Factor(bool isChat, double value) => isChat ? 1 + Math.Min(value, 0) : 1 - Math.Max(value, 0);
    void Apply(double value, bool restoreOnly = false) {
        var seen = new HashSet<string>();
        chatCount = gameCount = 0; ChatNames.Clear(); GameNames.Clear();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) {
            using (device) {
                var sessions = device.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++) {
                    using var session = sessions[i];
                    try {
                        if (session.State == AudioSessionState.AudioSessionStateExpired) continue;
                        int pid = checked((int)session.GetProcessID);
                        if (onlyProcess.HasValue && pid != onlyProcess.Value) continue;
                        // System sounds remain untouched.
                        if (pid == 0 || session.IsSystemSoundsSession) continue;
                        using var process = Process.GetProcessById(pid);
                        bool isChat = chat.Contains(process.ProcessName);
                        string id = device.ID + "|" + session.GetSessionInstanceIdentifier;
                        seen.Add(id);
                        float actual = session.SimpleAudioVolume.Volume;
                        if (!levels.TryGetValue(id, out var level)) {
                            if (restoreOnly) continue;
                            level = new SessionLevel(actual); levels[id] = level;
                            log($"Sesión {(isChat ? "CHAT" : "GAME")}: {process.ProcessName} · volumen inicial {actual:P0}");
                        }
                        // Respect manual session slider changes made while the app runs.
                        if (Math.Abs(actual - level.Last) > .002f)
                            level.Baseline = level.Factor > .001 ? Math.Clamp((float)(actual / level.Factor), 0, 1) : actual;
                        double factor = restoreOnly ? 1 : Factor(isChat, value);
                        float next = (float)(level.Baseline * factor);
                        if (Math.Abs(actual - next) > .0001f) session.SimpleAudioVolume.Volume = next;
                        level.Last = next; level.Factor = factor;
                        if (isChat) { chatCount++; if(!ChatNames.Contains(process.ProcessName))ChatNames.Add(process.ProcessName); }
                        else { gameCount++; if(!GameNames.Contains(process.ProcessName))GameNames.Add(process.ProcessName); }
                    } catch (ArgumentException) { } // Process ended during enumeration.
                    catch (System.Runtime.InteropServices.COMException e) { log("Sesión de audio no disponible: " + e.Message); }
                }
            }
        }
        foreach (var id in levels.Keys.Where(k => !seen.Contains(k)).ToArray()) levels.Remove(id);
    }
    public void Dispose() {
        try { Apply(0, restoreOnly: true); }
        catch (Exception e) { log("No se pudieron restaurar todas las sesiones: " + e.Message); }
        enumerator.Dispose();
    }
}
