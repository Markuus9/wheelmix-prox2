using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace ControlAudioLogitech;
static class WindowsAudioTest {
    public static int Run() {
        try {
            using var output = new WasapiOut(AudioClientShareMode.Shared, 100);
            var silence = new BufferedWaveProvider(new WaveFormat(48000, 16, 2)) { ReadFully = true };
            output.Init(silence); output.Play(); Thread.Sleep(300);
            using var process = Process.GetCurrentProcess();
            using var mixer = new WindowsMixer(process.ProcessName, Console.WriteLine, process.Id);
            mixer.Read().GetAwaiter().GetResult();
            float initial = Volume(process.Id);
            mixer.Write(-1).GetAwaiter().GetResult();
            if (Volume(process.Id) > .001) throw new Exception("No se silenció la sesión de prueba Chat.");
            mixer.Write(0).GetAwaiter().GetResult();
            if (Math.Abs(Volume(process.Id) - initial) > .001) throw new Exception("No se restauró la sesión de prueba.");
            mixer.Write(-.5).GetAwaiter().GetResult();
            if (Math.Abs(Volume(process.Id) - initial * .5) > .001) throw new Exception("Atenuación incorrecta.");
            mixer.Write(0).GetAwaiter().GetResult();
            Console.WriteLine("PASS Windows Core Audio: sesión silenciosa aislada; mute, centro y 50%."); return 0;
        } catch (Exception e) { Console.WriteLine(e); return 1; }
    }
    static float Volume(int pid) {
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) {
            using (device) {
                var sessions = device.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++) {
                    using var s = sessions[i];
                    if (s.GetProcessID == pid) return s.SimpleAudioVolume.Volume;
                }
            }
        }
        throw new Exception("No se encontró la sesión de prueba.");
    }
}
