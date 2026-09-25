using System.Runtime.InteropServices;
using System.Text.Json;
namespace ControlAudioLogitech;
static class Program {
    public static readonly string DataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ControlAudioLogitech");
    public static readonly int ExitMessage=RegisterWindowMessage("WheelMix.Exit.MainWindow");
    public static readonly int ActivateMessage=RegisterWindowMessage("WheelMix.Activate.MainWindow");
    [STAThread]static int Main(string[] args) {
        Directory.CreateDirectory(DataDir);
        bool cli=args.Any(a=>a is "--self-test" or "--test-windows" or "--probe-headset" or "--probe-sonar" or "--enumerate" or "--diagnose");
        if(cli) {
            if(!AttachConsole(unchecked((uint)-1))&&args.Contains("--diagnose"))AllocConsole();
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush=true});
        }
        if(args.Contains("--self-test"))return Tests.Run();
        if(args.Contains("--test-windows"))return WindowsAudioTest.Run();
        if(args.Contains("--probe-sonar")) {
            try {using var s=new Sonar();Console.WriteLine(s.Read().GetAwaiter().GetResult());return 0;}
            catch(Exception e){Console.WriteLine(e.Message);return 1;}
        }
        L.SetLanguage(Settings.Load().Language);
        int languageIndex=Array.IndexOf(args,"--language");
        if(languageIndex>=0&&languageIndex+1<args.Length)L.SetLanguage(args[languageIndex+1]);
        ApplicationConfiguration.Initialize();
        if(args.Contains("--exit")){PostMessage(new nint(0xffff),ExitMessage,0,0);return 0;}
        if(args.Contains("--probe-headset")) {
            using var input=new RawInput(Console.WriteLine,_=>{},()=>{});
            var reading=input.StatusDevicePath==null?new HeadsetReading(HeadsetLink.Unknown,Detail:"Receptor ausente"):HeadsetStatus.Query(input.StatusDevicePath).GetAwaiter().GetResult();
            Console.WriteLine(reading);return 0;
        }
        bool preview=args.Contains("--ui-smoke");
        using var mutex=new Mutex(true,"Local\\ControlAudioLogitech.ProX2",out bool created);
        if(!created&&!preview){PostMessage(new nint(0xffff),ActivateMessage,0,0);return 0;}
        Application.ThreadException+=(_,e)=> {
            if(preview){File.WriteAllText(Path.Combine(DataDir,"ui-test-error.log"),e.Exception.ToString());Environment.Exit(1);}
            else MessageBox.Show(e.Exception.Message,"WheelMix");
        };
        if(!preview&&!cli)try{Startup.MigrateLegacy();}catch(Exception e){System.Diagnostics.Trace.WriteLine(e);}
        using var form=new MainWindow(args.Contains("--diagnose"),args.Contains("--tray"),preview);
        if(args.Contains("--enumerate"))form.Load+=(_,_)=>form.BeginInvoke(()=>form.RequestExit());
        if(preview) {
            form.Shown+=(_,_)=> {
                form.BeginInvoke(()=> {
                    if(args.Contains("--ui-test-tray")) {
                        form.TestTrayLifecycle();return;
                    }
                    if(args.Contains("--ui-test-language")) {
                        L.SetLanguage("en");
                        if(form.Controls.Find("helpButton",true).Single().Text!="Help")throw new Exception("Main language did not update.");
                        L.SetLanguage("pt");
                        if(form.Controls.Find("helpButton",true).Single().Text!="Ajuda")throw new Exception("Second language change failed.");
                    }
                    string path=args.Last().EndsWith(".png",StringComparison.OrdinalIgnoreCase)?Path.GetFullPath(args.Last()):Path.Combine(DataDir,"preview.png");
                    using var settingsPreview=args.Contains("--ui-settings")?new SettingsDialog(Settings.Load(),new[]{"Discord","Spotify","chrome"}):null;
                    Form target=args.Contains("--ui-help")?form.OpenHelpForTest():settingsPreview??(Form)form;
                    if(settingsPreview!=null){settingsPreview.Show(form);Application.DoEvents();
                        if(args.Contains("--ui-test-selection")) {
                        int before=settingsPreview.SelectionCount;settingsPreview.AddExecutable(@"C:\Apps\Discord.exe");
                        if(settingsPreview.SelectionCount!=before)throw new Exception("Selector añadió duplicado.");
                        settingsPreview.AddExecutable(@"C:\Apps\CustomVoice.exe");
                        if(settingsPreview.SelectionCount!=before+1)throw new Exception("Selector no añadió la app.");
                        }
                    }
                    using var bitmap=new Bitmap(target.Width,target.Height);target.DrawToBitmap(bitmap,new Rectangle(Point.Empty,target.Size));
                    bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);form.Close();
                });
            };
        }
        Application.Run(form);return 0;
    }
    [DllImport("kernel32.dll")]static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")]static extern bool AllocConsole();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int RegisterWindowMessage(string message);
    [DllImport("user32.dll")]static extern bool PostMessage(nint hwnd,int message,nint w,nint l);
}
sealed class Settings {
    public string Language {get;set;}="auto";
    public double Step {get;set;}=.05;
    public bool Reverse {get;set;}
    public string Backend {get;set;}="Windows";
    public string ChatApps {get;set;}="Discord,DiscordPTB,DiscordCanary,Teams,ms-teams,ts3client_win64,Zoom";
    public bool Compensate {get;set;}=true;
    public static string FilePath=>Path.Combine(Program.DataDir,"settings.json");
    public static Settings Load(){
        try {
            var s=JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath))??new();
            if(!double.IsFinite(s.Step)||s.Step<.01||s.Step>1)s.Step=.05;
            s.Backend=s.Backend=="Sonar"?"Sonar":"Windows";
            s.ChatApps??="Discord";return s;
        }catch{return new();}
    }
    public void Save(){
        File.WriteAllText(FilePath+".tmp",JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(FilePath+".tmp",FilePath,true);
    }
}
