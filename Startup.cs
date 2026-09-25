using Microsoft.Win32;
namespace ControlAudioLogitech;
static class Startup {
    const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public static string? Command {get{using var k=Registry.CurrentUser.OpenSubKey(Key);return k?.GetValue("ControlAudioLogitech") as string;}}
    public static bool Enabled=>Command!=null;
    public static string BuildCommand(string executable)=>"\""+executable+"\" --tray";
    public static string Status {
        get {
            string? value=Command;
            return value==null?"No registrado: WheelMix no se abrirá al iniciar sesión.":
                value==BuildCommand(Environment.ProcessPath!)?"Registrado para esta ubicación. Se abrirá en la bandeja al iniciar sesión.":
                "Registrado con otra ubicación. Guarda activado para actualizarla.";
        }
    }
    public static void Set(bool enabled) {
        string? expected=enabled?BuildCommand(Environment.ProcessPath!):null;
        Restore(expected);
        if(Command!=expected)throw new IOException("Windows no confirmó el cambio de inicio automático.");
    }
    public static void Restore(string? command) {
        using var key=Registry.CurrentUser.CreateSubKey(Key);
        if(command==null)key.DeleteValue("ControlAudioLogitech",false);else key.SetValue("ControlAudioLogitech",command);
    }
}
static class Preferences {
    public static void Save(Settings next,bool startup) {
        string? previousJson=File.Exists(Settings.FilePath)?File.ReadAllText(Settings.FilePath):null;
        string? previousStartup=Startup.Command;
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
        if(!string.Equals(Path.GetExtension(path),".exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Selecciona un archivo .exe.");
        string name=Path.GetFileNameWithoutExtension(path);
        if(string.IsNullOrWhiteSpace(name)||name.Contains(','))throw new ArgumentException("Nombre de aplicación no válido.");
        return name;
    }
}
