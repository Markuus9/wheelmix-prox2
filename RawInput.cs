using System.Runtime.InteropServices;
using System.Text;

namespace ControlAudioLogitech;
sealed class RawInput : NativeWindow, IDisposable
{
    const uint Info = 0x2000000b, Name = 0x20000007, Preparsed = 0x20000005;
    readonly Action<string> log;
    readonly Action<int> wheel;
    readonly Action otherVolume;
    readonly Dictionary<nint, Device> devices = new();
    public int TargetCount => devices.Values.Count(d => d.Target);
    sealed record Device(string Path, bool Target, byte[] Descriptor);
    public RawInput(Action<string> log, Action<int> wheel, Action otherVolume) {
        this.log = log; this.wheel = wheel; this.otherVolume = otherVolume;
        CreateHandle(new CreateParams { Caption = "ProX2 Raw Input", Parent = new nint(-3) });
        var regs = new[] { new Registration { Page = 0x0c, Usage = 1, Flags = 0x2100, Window = Handle } };
        if (!RegisterRawInputDevices(regs, 1, (uint)Marshal.SizeOf<Registration>())) throw new System.ComponentModel.Win32Exception();
        Enumerate();
    }
    public void Enumerate() {
        uint count = 0, size = (uint)Marshal.SizeOf<Entry>();
        if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue) return;
        var list = new Entry[count];
        uint read = GetRawInputDeviceList(list, ref count, size);
        if (read == uint.MaxValue) return;
        devices.Clear();
        for (int i = 0; i < read; i++) if (list[i].Type == 2) {
            try { GetDevice(list[i].Handle); } catch (Exception e) { log("Dispositivo omitido: " + e.Message); }
        }
        log($"Dispositivos PRO X 2 Consumer Control: {TargetCount}");
    }
    Device GetDevice(nint h) {
        if (devices.TryGetValue(h, out var found)) return found;
        uint n = 0; GetRawInputDeviceInfo(h, Name, nint.Zero, ref n);
        var mem = Marshal.AllocHGlobal(checked((int)(n + 1) * 2));
        string path;
        try {
            if (GetRawInputDeviceInfo(h, Name, mem, ref n) == uint.MaxValue) throw new System.ComponentModel.Win32Exception();
            path = Marshal.PtrToStringUni(mem) ?? "";
        } finally { Marshal.FreeHGlobal(mem); }
        uint length = 32;
        mem = Marshal.AllocHGlobal(32);
        uint vid, pid; ushort page, usage;
        try {
            Marshal.WriteInt32(mem, 32);
            if (GetRawInputDeviceInfo(h, Info, mem, ref length) == uint.MaxValue) throw new System.ComponentModel.Win32Exception();
            vid = (uint)Marshal.ReadInt32(mem, 8); pid = (uint)Marshal.ReadInt32(mem, 12);
            page = (ushort)Marshal.ReadInt16(mem, 20); usage = (ushort)Marshal.ReadInt16(mem, 22);
        } finally { Marshal.FreeHGlobal(mem); }
        n = 0; GetRawInputDeviceInfo(h, Preparsed, nint.Zero, ref n);
        byte[] desc = new byte[n];
        mem = Marshal.AllocHGlobal((int)n);
        try {
            if (GetRawInputDeviceInfo(h, Preparsed, mem, ref n) == uint.MaxValue) throw new System.ComponentModel.Win32Exception();
            Marshal.Copy(mem, desc, 0, (int)n);
        } finally { Marshal.FreeHGlobal(mem); }
        bool target = vid == 0x046d && pid == 0x0af7 && page == 0x0c && usage == 1;
        found = new Device(path, target, desc); devices[h] = found;
        log($"HID VID={vid:X4} PID={pid:X4} page={page:X4} usage={usage:X4} target={target} {path}");
        return found;
    }
    protected override void WndProc(ref Message m) {
        try {
            if (m.Msg == 0x00fe) Enumerate();
            if (m.Msg == 0x00ff) Process(m.LParam);
        } catch (Exception ex) { log("HID error: " + ex.Message); }
        base.WndProc(ref m); // Required WM_INPUT cleanup, including foreground messages.
    }
    void Process(nint raw) {
        uint size = 0, header = (uint)Marshal.SizeOf<Header>();
        if (GetRawInputData(raw, 0x10000003, nint.Zero, ref size, header) == uint.MaxValue || size < header + 8) return;
        var ptr = Marshal.AllocHGlobal((int)size);
        try {
            if (GetRawInputData(raw, 0x10000003, ptr, ref size, header) == uint.MaxValue) return;
            var h = Marshal.PtrToStructure<Header>(ptr);
            if (h.Type != 2) return;
            Device d = GetDevice(h.Device);
            int length = Marshal.ReadInt32(ptr, (int)header), count = Marshal.ReadInt32(ptr, (int)header + 4);
            if (length <= 0 || count < 0 || (long)length * count > size - header - 8) return;
            for (int i = 0; i < count; i++) {
                var report = new byte[length];
                Marshal.Copy(ptr + (int)header + 8 + i * length, report, 0, length);
                ushort[] usages = new ushort[256]; uint usageCount = (uint)usages.Length;
                int status = HidP_GetUsages(0, 0x0c, 0, usages, ref usageCount, d.Descriptor, report, (uint)length);
                int direction = 0;
                if (status >= 0) {
                    bool up = usages.Take((int)usageCount).Contains((ushort)0xe9);
                    bool down = usages.Take((int)usageCount).Contains((ushort)0xea);
                    direction = up == down ? 0 : up ? 1 : -1;
                }
                log($"INPUT target={d.Target} hid={h.Device:X} data={Convert.ToHexString(report)} usages={(status >= 0 ? string.Join(",", usages.Take((int)usageCount).Select(u => u.ToString("X4"))) : $"parse-error:{status:X8}")} device={d.Path}");
                // Consume only target reports; never subscribe to the keyboard usage page.
                // Each nonzero report is a tick; neutral/release reports have no effect.
                if (direction != 0) { if (d.Target) wheel(direction); else otherVolume(); }
            }
        } finally { Marshal.FreeHGlobal(ptr); }
    }
    public void Dispose() {
        RegisterRawInputDevices(new[] { new Registration { Page = 12, Usage = 1, Flags = 1 } }, 1, (uint)Marshal.SizeOf<Registration>());
        DestroyHandle();
    }
    [StructLayout(LayoutKind.Sequential)] struct Registration { public ushort Page, Usage; public uint Flags; public nint Window; }
    [StructLayout(LayoutKind.Sequential)] struct Entry { public nint Handle; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] struct Header { public uint Type, Size; public nint Device, WParam; }
    [DllImport("user32.dll", SetLastError=true)] static extern bool RegisterRawInputDevices(Registration[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError=true)] static extern uint GetRawInputDeviceList([In, Out] Entry[]? list, ref uint count, uint size);
    [DllImport("user32.dll", EntryPoint="GetRawInputDeviceInfoW", SetLastError=true)] static extern uint GetRawInputDeviceInfo(nint device, uint command, nint data, ref uint size);
    [DllImport("user32.dll", SetLastError=true)] static extern uint GetRawInputData(nint raw, uint command, nint data, ref uint size, uint header);
    [DllImport("hid.dll")] static extern int HidP_GetUsages(int type, ushort page, ushort collection, [Out] ushort[] usages, ref uint count, byte[] preparsed, byte[] report, uint length);
}
