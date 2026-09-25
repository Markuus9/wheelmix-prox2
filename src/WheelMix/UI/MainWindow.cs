using System.Diagnostics;
using System.Runtime.InteropServices;
namespace ControlAudioLogitech;
sealed class MainWindow : Form {
    Settings config=Settings.Load();
    IMixer? mixer;
    RawInput? raw;
    readonly VolumeGuard guard;
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer timer=new() { Interval=25 };
    readonly StreamWriter logfile;
    readonly Queue<string> lines=new();
    readonly bool diagnostic,preview,startHidden;
    readonly Label device=Theme.LabelKey("main.searching",10,Theme.Muted);
    readonly Label state=Theme.LabelKey("main.preparing",10,Theme.Muted);
    readonly Label gameApps=Theme.LabelKey("main.playGame",10,Theme.Muted);
    readonly Label chatApps=Theme.LabelKey("main.playChat",10,Theme.Muted);
    readonly Label note=Theme.Label("",9,Theme.Muted);
    readonly MixSurface surface=new();
    readonly Button pause;
    bool exitRequested,notified;
    internal bool CloseToTray {get;set;}=true;
    bool busy,connected,paused,closing,readyToClose,editing,statusBusy;
    long nextStatus;
    HeadsetReading headset=new(HeadsetLink.Unknown);
    readonly CancellationTokenSource statusCancellation=new();
    HelpDialog? helpWindow;
    Label? compensationStatus;
    int pending;
    double current;
    double? requested;
    long nextPoll;
    Form? diagnosticWindow;
    TextBox? diagnosticText;
    public MainWindow(bool diagnostic,bool startHidden,bool preview=false) {
        CloseToTray=!preview&&!diagnostic;this.diagnostic=diagnostic; this.startHidden=startHidden; this.preview=preview;
        Text="WheelMix"; Name="WheelMix";
        AutoScaleMode=AutoScaleMode.Dpi; ClientSize=new Size(900,700); MinimumSize=new Size(800,700);
        StartPosition=FormStartPosition.CenterScreen; BackColor=Theme.Background; ForeColor=Theme.Text; Font=new Font("Segoe UI",10);
        Icon=Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??SystemIcons.Application;
        logfile=new StreamWriter(Path.Combine(Program.DataDir,"diagnostic-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".log")) { AutoFlush=true };
        guard=new VolumeGuard(Log);
        if(!preview && !diagnostic) mixer=CreateMixer();
        var root=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(28),BackColor=Theme.Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,154));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,80));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
        var header=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=new Padding(0) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        header.Controls.Add(Theme.Label("WheelMix",25,null,FontStyle.Bold),0,0);
        var preferences=Theme.ButtonKey("main.preferences",OpenSettings); preferences.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        header.Controls.Add(preferences,1,0);
        header.Controls.Add(Theme.LabelKey("main.tagline",10,Theme.Muted),0,1);
        device.Anchor=AnchorStyles.Right|AnchorStyles.Top; header.Controls.Add(device,1,1); root.Controls.Add(header,0,0);
        var hero=new CardPanel { Dock=DockStyle.Fill,Margin=new Padding(0,0,0,18) };
        var heroLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3 };
        heroLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
        var heroHeading=Theme.Row(); heroHeading.Controls.Add(Theme.LabelKey("main.balance",10,Theme.Muted,FontStyle.Bold));
        heroLayout.Controls.Add(heroHeading,0,0);
        surface.Step=config.Step; surface.Requested+=v=> { if(!paused&&!diagnostic&&!preview) {requested=v;pending=0;} };
        surface.Enabled=!diagnostic; heroLayout.Controls.Add(surface,0,1);
        var heroFooter=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1 };
        heroFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65)); heroFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
        state.Anchor=AnchorStyles.Left|AnchorStyles.Top; heroFooter.Controls.Add(state,0,0);
        var center=Theme.ButtonKey("main.reset",()=> { requested=0;pending=0; },true); center.Anchor=AnchorStyles.Right|AnchorStyles.Top; center.Enabled=!diagnostic;
        heroFooter.Controls.Add(center,1,0); heroLayout.Controls.Add(heroFooter,0,2); hero.Controls.Add(heroLayout); root.Controls.Add(hero,0,1);
        var groups=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0) };
        groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        var game=Group("GAME","main.gameSubtitle",gameApps,Theme.Accent);
        var chat=Group("CHAT","main.chatSubtitle",chatApps,Theme.Mint);
        game.Margin=new Padding(0,0,9,18);chat.Margin=new Padding(9,0,0,18);
        groups.Controls.Add(game,0,0);groups.Controls.Add(chat,1,0);root.Controls.Add(groups,0,2);
        var quick=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0) };
        quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        var info=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0) };
        info.Controls.Add(Theme.LabelKey("main.volumes",11,null,FontStyle.Bold));
        note.MaximumSize=new Size(480,0);info.Controls.Add(note);quick.Controls.Add(info,0,0);
        var controls=Theme.Row(); controls.Anchor=AnchorStyles.Top|AnchorStyles.Right; controls.Dock=DockStyle.None;
        pause=Theme.ButtonKey("main.pause",()=> { if(busy)return; paused=!paused;pause!.Text=paused?L.Text("main.resume"):L.Text("main.pause");if(paused){pending=0;requested=0;} guard.Enabled=!paused&&config.Compensate;UpdatePresentation(); });
        pause.Enabled=!diagnostic; controls.Controls.Add(pause);
        var helpButton=Theme.ButtonKey("main.help",ShowHelp);helpButton.Name="helpButton";controls.Controls.Add(helpButton);quick.Controls.Add(controls,1,0);root.Controls.Add(quick,0,3);
        var bottom=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
        bottom.Controls.Add(Theme.Label("PRO X 2 LIGHTSPEED  /  Windows  /  v"+typeof(MainWindow).Assembly.GetName().Version!.ToString(3),9,Theme.Muted),0,0);
        var logs=new LinkLabel { Text=L.Text("main.diagnostics"),AutoSize=true,LinkColor=Theme.Muted,ActiveLinkColor=Theme.Accent,Anchor=AnchorStyles.Top|AnchorStyles.Right };
        L.Bind(logs,"main.diagnostics");logs.LinkClicked+=(_,_)=>ShowDiagnostics();bottom.Controls.Add(logs,1,0);root.Controls.Add(bottom,0,4);
        Controls.Add(root);
        tray=new NotifyIcon { Icon=Icon,Text="WheelMix · Game + Chat",Visible=!preview };
        var menu=new ContextMenuStrip();menu.Items.Add(L.Text("tray.open"),null,(_,_)=>Restore());
        menu.Items.Add(L.Text("main.reset"),null,(_,_)=> {requested=0;pending=0;});
        menu.Items.Add(L.Text("tray.exit"),null,(_,_)=>RequestExit());tray.ContextMenuStrip=menu;
        tray.MouseClick+=(_,e)=> {if(e.Button==MouseButtons.Left)Restore();};
        Resize+=(_,_)=> {if(WindowState==FormWindowState.Minimized)HideToTray();};
        Load+=(_,_)=> {
            if(preview) { connected=true; device.Text=L.Text("headset.on")+" · 44%";device.ForeColor=Theme.Mint;gameApps.Text="Spotify  ·  Chrome";chatApps.Text="Discord";UpdatePresentation();return; }
            try {raw=new RawInput(Log,OnWheel,guard.Cancel);}
            catch(Exception e) {Log("HID: "+e.Message);}
            guard.Enabled=!diagnostic&&config.Compensate; Log(diagnostic?"Modo diagnóstico: solo lectura.":"WheelMix iniciado · "+config.Backend);
            timer.Start();if(diagnostic)ShowDiagnostics();if(startHidden)BeginInvoke(()=>HideToTray());
        };
        timer.Tick+=async(_,_)=> {
            if(closing)return;
            guard.Tick();
            if(!diagnostic&&!statusBusy&&Environment.TickCount64>=nextStatus)_=RefreshHeadset();
            if(!diagnostic&&!editing&&!busy&&(pending!=0||requested.HasValue||Environment.TickCount64>=nextPoll))await Pump();
            UpdatePresentation();
        };
        FormClosing+=async(_,e)=> {
            if(readyToClose)return;
            if(ShouldHideOnClose(e.CloseReason,exitRequested,!CloseToTray)) {
                e.Cancel=true;HideToTray();
                if(!notified){notified=true;tray.ShowBalloonTip(2500,"WheelMix",L.Text("tray.hidden"),ToolTipIcon.Info);}
                return;
            }
            closing=true;timer.Stop();statusCancellation.Cancel();
            if(busy) {e.Cancel=true;while(busy)await Task.Delay(25);readyToClose=true;Close();}
        };
        FormClosed+=(_,_)=> {L.Changed-=LanguageChanged;timer.Dispose();raw?.Dispose();guard.Dispose();mixer?.Dispose();tray.Dispose();diagnosticWindow?.Close();helpWindow?.Close();logfile.Dispose();statusCancellation.Dispose();};
        L.Changed+=LanguageChanged;UpdatePresentation();
    }
    CardPanel Group(string title,string subtitle,Label apps,Color color) {
        var p=new CardPanel {Dock=DockStyle.Fill};var stack=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        stack.Controls.Add(Theme.Label(title,10,color,FontStyle.Bold));stack.Controls.Add(Theme.LabelKey(subtitle,11,null,FontStyle.Bold));
        apps.AutoEllipsis=true;apps.AutoSize=false;apps.Height=45;apps.Width=320;stack.Controls.Add(apps);
        p.Resize+=(_,_)=>apps.Width=Math.Max(50,p.ClientSize.Width-p.Padding.Horizontal);
        p.Controls.Add(stack);return p;
    }
    IMixer CreateMixer()=>config.Backend=="Sonar"?new Sonar():new WindowsMixer(config.ChatApps,Log);
    void UpdatePresentation() {
        surface.Value=current;
        if(compensationStatus is {IsDisposed:false})compensationStatus.Text=guard.Description;
        if(!preview) {bool found=(raw?.TargetCount??0)>0;
            device.Text=!found?L.Text("headset.receiverOff"):headset.Link==HeadsetLink.Connected?L.Text("headset.on")+(headset.Battery.HasValue?$" · {headset.Battery}%":""):headset.Link==HeadsetLink.Disconnected?L.Text("headset.off"):L.Text("headset.unknown");
            device.ForeColor=found&&headset.Link==HeadsetLink.Connected?Theme.Mint:Theme.Muted;}
        if(diagnostic)state.Text=L.Text("main.readOnly");
        else if(paused)state.Text=L.Text("main.paused");
        else if(connected)state.Text=L.Text("main.ready");
        else state.Text=config.Backend=="Sonar"?L.Text("main.openSonar"):L.Text("main.preparingWindows");
        note.Text=L.Text("main.centerHint")+(guard.IsOperational||preview?L.Text("guard.active"):guard.Enabled?L.Text("guard.waiting"):L.Text("guard.normal"));
        if(mixer is WindowsMixer native) {
            chatApps.Text=native.ChatNames.Count==0?L.Text("main.waitChat"):string.Join("  ·  ",native.ChatNames);
            gameApps.Text=native.GameNames.Count==0?L.Text("main.waitGame"):string.Join("  ·  ",native.GameNames);
        } else if(config.Backend=="Sonar") {chatApps.Text=L.Text("main.sonarChat");gameApps.Text=L.Text("main.sonarGame");}
    }
    void OnWheel(int direction) {
        if(closing||paused||diagnostic||editing)return;
        if(!connected)return;
        guard.Wheel();pending=Math.Clamp(pending+(config.Reverse?-direction:direction),-100,100);
    }
    async Task Pump() {
        if(mixer==null||busy)return;
        busy=true;int delta=pending;pending=0;double? target=requested;requested=null;
        try {
            current=await mixer.Read();
            if(delta!=0||target.HasValue) {
                double desired=target??Mix.Move(current,delta,config.Step,false);
                await mixer.Write(desired);current=await mixer.Read();
                if(Math.Abs(current-desired)>.015)throw new InvalidOperationException("El mezclador no aplicó el balance.");
                Log($"Balance: {current:0.00}");
            }
            connected=true;nextPoll=Environment.TickCount64+1500;
        }catch(Exception e){connected=false;pending=0;requested=null;nextPoll=Environment.TickCount64+5000;Log("Audio: "+e.Message);}
        finally {busy=false;}
        if(!closing)UpdatePresentation();
    }
    async Task RefreshHeadset() {
        statusBusy=true;string? path=raw?.StatusDevicePath;
        try {
            var value=path==null?new HeadsetReading(HeadsetLink.Unknown):await HeadsetStatus.Query(path,statusCancellation.Token);
            if(closing||IsDisposed)return;
            if(path==raw?.StatusDevicePath) {
                if(value!=headset)Log($"Auricular: {value.Link} · batería {value.Battery} · {value.Detail}");
                headset=value;
            } else headset=new(HeadsetLink.Unknown);
            nextStatus=Environment.TickCount64+2000;UpdatePresentation();
        } finally {statusBusy=false;}
    }
    void OpenSettings() {
        if(busy||diagnostic)return;
        editing=true;guard.Cancel();guard.Enabled=false;
        try {
            var running=mixer is WindowsMixer native?native.ChatNames.Concat(native.GameNames):Array.Empty<string>();
            using var dialog=new SettingsDialog(config,running);
            if(dialog.ShowDialog(this)!=DialogResult.OK||dialog.Value==null)return;
            var updated=dialog.Value;Preferences.Save(updated,dialog.RequestedStartup);
            mixer?.Dispose();config=updated;L.SetLanguage(config.Language);mixer=preview?null:CreateMixer();
            pending=0;requested=null;current=0;nextPoll=0;connected=false;surface.Step=config.Step;
            Log("Preferencias guardadas. "+Startup.Status);UpdatePresentation();
        }catch(Exception e){MessageBox.Show(this,e.Message,L.Text("error.save"));}
        finally{editing=false;guard.Enabled=!preview&&!paused&&config.Compensate;}
    }
    void ShowHelp() {
        if(helpWindow==null||helpWindow.IsDisposed){helpWindow=new HelpDialog();helpWindow.Show(this);}
        if(helpWindow.WindowState==FormWindowState.Minimized)helpWindow.WindowState=FormWindowState.Normal;
        helpWindow.BringToFront();helpWindow.Activate();Log("Ayuda abierta.");
    }
    internal Form OpenHelpForTest() {
        var button=Controls.Find("helpButton",true).OfType<Button>().Single();button.PerformClick();
        return helpWindow??throw new InvalidOperationException("El botón Ayuda no abrió la ventana.");
    }
    void ShowDiagnostics() {
        if(diagnosticWindow is {IsDisposed:false}) {diagnosticWindow.Activate();return;}
        diagnosticWindow=new Form {Text=L.Text("diagnostics.title"),Size=new Size(900,530),BackColor=Theme.Background,ForeColor=Theme.Text,StartPosition=FormStartPosition.CenterParent};
        diagnosticText=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=Theme.Card,ForeColor=Theme.Muted,Font=new Font("Consolas",9),Text=string.Join(Environment.NewLine,lines)};
        var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=56,Padding=new Padding(10)};
        footer.Controls.Add(Theme.ButtonKey("diagnostics.logs",()=>Process.Start(new ProcessStartInfo(Program.DataDir){UseShellExecute=true})));
        footer.Controls.Add(Theme.ButtonKey("diagnostics.detect",()=> {raw?.Enumerate();nextPoll=0;}));
        compensationStatus=Theme.Label(guard.Description,10,Theme.Muted);compensationStatus.Dock=DockStyle.Top;compensationStatus.AutoSize=false;compensationStatus.Height=48;
        diagnosticWindow.Controls.Add(diagnosticText);diagnosticWindow.Controls.Add(compensationStatus);diagnosticWindow.Controls.Add(footer);diagnosticWindow.Show(this);
    }
    void Log(string message) {
        if(IsDisposed)return;
        string line=DateTime.Now.ToString("HH:mm:ss.fff")+" "+message;
        if(diagnostic)Console.WriteLine(line);
        try {if(logfile.BaseStream.Length<20_000_000)logfile.WriteLine(line);}catch(ObjectDisposedException){return;}
        lines.Enqueue(line);while(lines.Count>400)lines.Dequeue();
        if(diagnosticText is {IsDisposed:false}) {if(diagnosticText.TextLength>60000)diagnosticText.Clear();diagnosticText.AppendText(line+Environment.NewLine);}
    }
    internal void TestTrayLifecycle() {
        CloseToTray=true;notified=true;
        Close();
        if(IsDisposed||Visible||ShowInTaskbar)throw new Exception("X did not hide the live window.");
        tray.ContextMenuStrip!.Items[0].PerformClick();
        if(!Visible||!ShowInTaskbar)throw new Exception("Tray Open did not restore the window.");
        L.SetLanguage("fr");
        if(tray.ContextMenuStrip.Items[2].Text!="Quitter")throw new Exception("Tray language did not update.");
        tray.ContextMenuStrip.Items[2].PerformClick();
        if(!IsDisposed)throw new Exception("Tray Exit did not dispose the application.");
    }
    internal static bool ShouldHideOnClose(CloseReason reason,bool exitRequested,bool temporaryMode)=>reason==CloseReason.UserClosing&&!exitRequested&&!temporaryMode;
    internal void RequestExit(){exitRequested=true;Close();}
    void HideToTray(){Hide();ShowInTaskbar=false;}
    void LanguageChanged() {
        tray.ContextMenuStrip!.Items[0].Text=L.Text("tray.open");
        tray.ContextMenuStrip.Items[1].Text=L.Text("main.reset");
        tray.ContextMenuStrip.Items[2].Text=L.Text("tray.exit");
        pause.Text=L.Text(paused?"main.resume":"main.pause");
        helpWindow?.Close();diagnosticWindow?.Close();
        UpdatePresentation();
    }
    void Restore(){ShowInTaskbar=true;Show();if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Activate();}
    protected override void WndProc(ref Message m) {
        if(m.Msg==Program.ExitMessage){RequestExit();return;}
        if(m.Msg==Program.ActivateMessage){Restore();return;}base.WndProc(ref m);
    }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);int dark=1;DwmSetWindowAttribute(Handle,20,ref dark,sizeof(int));
    }
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
}
