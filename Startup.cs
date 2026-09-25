using Microsoft.Win32;
namespace ControlAudioLogitech;

// Only our two named values are touched; preserve exact names when rolling back.
sealed record StartupSnapshot(string? Current,string? Legacy);
sealed class StartupRegistration(RegistryKey key) {
    public const string Name="WheelMix", LegacyName="ControlAudioLogitech";
    public StartupSnapshot Capture()=>new(key.GetValue(Name) as string,key.GetValue(LegacyName) as string);
    public void Restore(StartupSnapshot state) {
        Put(Name,state.Current);Put(LegacyName,state.Legacy);
    }
    void Put(string name,string? value) {
        if(value==null)key.DeleteValue(name,false);else key.SetValue(name,value,RegistryValueKind.String);
    }
    public void Set(string? command) {
        Put(Name,command);Put(LegacyName,null);
        if(Capture()!=new StartupSnapshot(command,null))throw new IOException(L.Text("startup.error"));
    }
}
static class Startup {
    const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public static StartupSnapshot Capture() {using var key=Registry.CurrentUser.CreateSubKey(Key);return new StartupRegistration(key).Capture();}
    public static string? Command {get{var state=Capture();return state.Current??state.Legacy;}}
    public static bool Enabled=>Command!=null;
    public static string BuildCommand(string executable)=>"\""+executable+"\" --tray";
    public static string Status {
        get {
            var state=Capture();
            return (state.Current??state.Legacy)==null?L.Text("startup.off"):
                state.Current!=BuildCommand(Environment.ProcessPath!)||state.Legacy!=null?L.Text("startup.other"):
                DisabledByWindows?L.Text("startup.disabled"):L.Text("startup.on");
        }
    }
    // Windows stores the Startup apps toggle separately; an odd first byte means the user switched it off there.
    const string ApprovalKey=@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    public static bool DisabledByWindows {
        get {using var key=Registry.CurrentUser.OpenSubKey(ApprovalKey);return IsDisabledApproval(key?.GetValue(StartupRegistration.Name) as byte[]);}
    }
    internal static bool IsDisabledApproval(byte[]? approval)=>approval is {Length:>0}&&(approval[0]&1)==1;
    public static void Set(bool enabled) {
        using var key=Registry.CurrentUser.CreateSubKey(Key);
        new StartupRegistration(key).Set(enabled?BuildCommand(Environment.ProcessPath!):null);
        // Ticking the option in WheelMix is an explicit opt-in, so lift a stale "disabled" left in Windows.
        if(enabled&&DisabledByWindows) {
            using var approvals=Registry.CurrentUser.OpenSubKey(ApprovalKey,true);
            approvals?.DeleteValue(StartupRegistration.Name,false);
        }
    }
    public static void Restore(StartupSnapshot state) {
        using var key=Registry.CurrentUser.CreateSubKey(Key);new StartupRegistration(key).Restore(state);
    }
    public static void MigrateLegacy() {
        // Keep the old Windows startup approval state, including disabled status.
        var state=Capture();
        if(state.Current!=null||state.Legacy==null)return;
        const string approvalPath=@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        using var approvals=Registry.CurrentUser.OpenSubKey(approvalPath,true);
        if(approvals?.GetValue(StartupRegistration.Name)==null&&approvals?.GetValue(StartupRegistration.LegacyName) is byte[] approval)
            approvals.SetValue(StartupRegistration.Name,approval,RegistryValueKind.Binary);
        using var key=Registry.CurrentUser.CreateSubKey(Key);
        try{new StartupRegistration(key).Set(state.Legacy);}
        catch{new StartupRegistration(key).Restore(state);throw;}
    }
}
static class Preferences {
    public static void Save(Settings next,bool startup) {
        string? previousJson=File.Exists(Settings.FilePath)?File.ReadAllText(Settings.FilePath):null;
        var previousStartup=Startup.Capture();
        try {next.Save();Startup.Set(startup);}
        catch {
            if(previousJson==null){if(File.Exists(Settings.FilePath))File.Delete(Settings.FilePath);}
            else File.WriteAllText(Settings.FilePath,previousJson);
            Startup.Restore(previousStartup);
            throw;
        }
    }
}
static class ChatApplications {
    public static string FromExecutable(string path) {
        if(!string.Equals(Path.GetExtension(path),".exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException(L.Text("error.exe"));
        string name=Path.GetFileNameWithoutExtension(path);
        if(string.IsNullOrWhiteSpace(name)||name.Contains(','))throw new ArgumentException(L.Text("error.appName"));
        return name;
    }
}
