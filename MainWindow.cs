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
    readonly Label device=Theme.Label("Buscando auriculares",10,Theme.Muted);
    readonly Label state=Theme.Label("Preparando el audio…",10,Theme.Muted);
    readonly Label gameApps=Theme.Label("Reproduce audio en un juego o una aplicación.",10,Theme.Muted);
    readonly Label chatApps=Theme.Label("Discord se detecta automáticamente al reproducir audio.",10,Theme.Muted);
    readonly Label note=Theme.Label("",9,Theme.Muted);
    readonly MixSurface surface=new();
    readonly Button pause;
    bool busy,connected,paused,closing,readyToClose;
    int pending;
    double current;
    double? requested;
    long nextPoll;
    Form? diagnosticWindow;
    TextBox? diagnosticText;
    public MainWindow(bool diagnostic,bool startHidden,bool preview=false) {
        this.diagnostic=diagnostic; this.startHidden=startHidden; this.preview=preview;
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
        var preferences=Theme.Button("Preferencias",OpenSettings); preferences.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        header.Controls.Add(preferences,1,0);
        header.Controls.Add(Theme.Label("Tu juego y tu voz. En equilibrio.",10,Theme.Muted),0,1);
        device.Anchor=AnchorStyles.Right|AnchorStyles.Top; header.Controls.Add(device,1,1); root.Controls.Add(header,0,0);
        var hero=new CardPanel { Dock=DockStyle.Fill,Margin=new Padding(0,0,0,18) };
        var heroLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3 };
        heroLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
        var heroHeading=Theme.Row(); heroHeading.Controls.Add(Theme.Label("BALANCE DE AUDIO",10,Theme.Muted,FontStyle.Bold));
        heroLayout.Controls.Add(heroHeading,0,0);
        surface.Step=config.Step; surface.Requested+=v=> { if(!paused&&!diagnostic&&!preview) {requested=v;pending=0;} };
        surface.Enabled=!diagnostic; heroLayout.Controls.Add(surface,0,1);
        var heroFooter=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1 };
        heroFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65)); heroFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
        state.Anchor=AnchorStyles.Left|AnchorStyles.Top; heroFooter.Controls.Add(state,0,0);
        var center=Theme.Button("Restablecer centro",()=> { requested=0;pending=0; },true); center.Anchor=AnchorStyles.Right|AnchorStyles.Top; center.Enabled=!diagnostic;
        heroFooter.Controls.Add(center,1,0); heroLayout.Controls.Add(heroFooter,0,2); hero.Controls.Add(heroLayout); root.Controls.Add(hero,0,1);
        var groups=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0) };
        groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        var game=Group("GAME","El resto de tu audio",gameApps,Theme.Accent);
        var chat=Group("CHAT","Tus aplicaciones de voz",chatApps,Theme.Mint);
        game.Margin=new Padding(0,0,9,18);chat.Margin=new Padding(9,0,0,18);
        groups.Controls.Add(game,0,0);groups.Controls.Add(chat,1,0);root.Controls.Add(groups,0,2);
        var quick=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0) };
        quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        var info=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0) };
        info.Controls.Add(Theme.Label("Tus volúmenes, tal como los dejaste.",11,null,FontStyle.Bold));
        note.MaximumSize=new Size(480,0);info.Controls.Add(note);quick.Controls.Add(info,0,0);
        var controls=Theme.Row(); controls.Anchor=AnchorStyles.Top|AnchorStyles.Right; controls.Dock=DockStyle.None;
        pause=Theme.Button("Pausar",()=> { if(busy)return; paused=!paused;pause!.Text=paused?"Reanudar":"Pausar";if(paused){pending=0;requested=0;} guard.Enabled=!paused&&config.Compensate;UpdatePresentation(); });
        pause.Enabled=!diagnostic; controls.Controls.Add(pause);
        controls.Controls.Add(Theme.Button("Ayuda",ShowHelp));quick.Controls.Add(controls,1,0);root.Controls.Add(quick,0,3);
        var bottom=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
        bottom.Controls.Add(Theme.Label("PRO X 2 LIGHTSPEED  /  Windows  /  v0.2.0",9,Theme.Muted),0,0);
        var logs=new LinkLabel { Text="Diagnóstico",AutoSize=true,LinkColor=Theme.Muted,ActiveLinkColor=Theme.Accent,Anchor=AnchorStyles.Top|AnchorStyles.Right };
        logs.LinkClicked+=(_,_)=>ShowDiagnostics();bottom.Controls.Add(logs,1,0);root.Controls.Add(bottom,0,4);
        Controls.Add(root);
        tray=new NotifyIcon { Icon=Icon,Text="WheelMix · Game + Chat",Visible=!preview };
        var menu=new ContextMenuStrip();menu.Items.Add("Abrir WheelMix",null,(_,_)=>Restore());
        menu.Items.Add("Restablecer centro",null,(_,_)=> {requested=0;pending=0;});
        menu.Items.Add("Salir",null,(_,_)=>Close());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>Restore();
        Resize+=(_,_)=> {if(WindowState==FormWindowState.Minimized)Hide();};
        Load+=(_,_)=> {
            if(preview) { connected=true; device.Text="●  PRO X 2 conectado";device.ForeColor=Theme.Mint;gameApps.Text="Spotify  ·  Chrome";chatApps.Text="Discord";UpdatePresentation();return; }
            try {raw=new RawInput(Log,OnWheel,guard.Cancel);}
            catch(Exception e) {Log("HID: "+e.Message);}
            guard.Enabled=!diagnostic&&config.Compensate; Log(diagnostic?"Modo diagnóstico: solo lectura.":"WheelMix iniciado · "+config.Backend);
            timer.Start();if(diagnostic)ShowDiagnostics();if(startHidden)BeginInvoke(()=>Hide());
        };
        timer.Tick+=async(_,_)=> {
            if(closing)return;
            guard.Tick();
            if(!diagnostic&&!busy&&(pending!=0||requested.HasValue||Environment.TickCount64>=nextPoll))await Pump();
            UpdatePresentation();
        };
        FormClosing+=async(_,e)=> {
            if(readyToClose)return;
            closing=true;timer.Stop();
            if(busy) {e.Cancel=true;while(busy)await Task.Delay(25);readyToClose=true;Close();}
        };
        FormClosed+=(_,_)=> {timer.Dispose();raw?.Dispose();guard.Dispose();mixer?.Dispose();tray.Dispose();diagnosticWindow?.Close();logfile.Dispose();};
        UpdatePresentation();
    }
    CardPanel Group(string title,string subtitle,Label apps,Color color) {
        var p=new CardPanel {Dock=DockStyle.Fill};var stack=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        stack.Controls.Add(Theme.Label(title,10,color,FontStyle.Bold));stack.Controls.Add(Theme.Label(subtitle,11,null,FontStyle.Bold));
        apps.AutoEllipsis=true;apps.AutoSize=false;apps.Height=45;apps.Width=320;stack.Controls.Add(apps);
        p.Resize+=(_,_)=>apps.Width=Math.Max(50,p.ClientSize.Width-p.Padding.Horizontal);
        p.Controls.Add(stack);return p;
    }
    IMixer CreateMixer()=>config.Backend=="Sonar"?new Sonar():new WindowsMixer(config.ChatApps,Log);
    void UpdatePresentation() {
        surface.Value=current;
        if(!preview) {bool found=(raw?.TargetCount??0)>0; device.Text=found?"●  PRO X 2 conectado":"○  Conecta el receptor USB";device.ForeColor=found?Theme.Mint:Theme.Muted;}
        if(diagnostic)state.Text="Solo diagnóstico · no modifica el audio";
        else if(paused)state.Text="En pausa · rueda de volumen normal";
        else if(connected)state.Text="Listo para mezclar · "+(config.Backend=="Windows"?"sin SteelSeries":"Sonar");
        else state.Text=config.Backend=="Sonar"?"Abre GG y activa Sonar":"Preparando el audio de Windows…";
        note.Text="Centro conserva el volumen original. "+(guard.IsOperational||preview?"Volumen general: compensación activa.":guard.Enabled?"Compensación en espera de una salida de audio.":"Volumen general: control normal de Windows.");
        if(mixer is WindowsMixer native) {
            chatApps.Text=native.ChatNames.Count==0?"Esperando audio de Discord o tus apps de chat…":string.Join("  ·  ",native.ChatNames);
            gameApps.Text=native.GameNames.Count==0?"Abre un juego o reproduce música.":string.Join("  ·  ",native.GameNames);
        } else if(config.Backend=="Sonar") {chatApps.Text="Canal Sonar Chat";gameApps.Text="Canal Sonar Gaming";}
    }
    void OnWheel(int direction) {
        if(closing||paused||diagnostic)return;
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
    void OpenSettings() {
        if(busy||diagnostic)return;
        var running=mixer is WindowsMixer native?native.ChatNames.Concat(native.GameNames):Array.Empty<string>();
        using var dialog=new SettingsDialog(config,running);
        if(dialog.ShowDialog(this)!=DialogResult.OK||dialog.Value==null)return;
        try {
            var updated=dialog.Value;updated.Save();mixer?.Dispose();config=updated;mixer=preview?null:CreateMixer();
            pending=0;requested=null;current=0;nextPoll=0;connected=false;surface.Step=config.Step;guard.Cancel();guard.Enabled=!preview&&!paused&&config.Compensate;
            UpdatePresentation();
        }catch(Exception e){MessageBox.Show(this,e.Message,"No se pudieron guardar los ajustes");}
    }
    void ShowHelp() {
        MessageBox.Show(this,
            "1. Conecta el receptor USB de tus PRO X 2.\n2. Abre Discord y un juego o música.\n3. Gira la rueda: subir favorece Chat; bajar favorece Game.\n\nEn Preferencias puedes invertir el sentido y elegir tus apps de voz. No se crean salidas virtuales. Las apps conservan su salida habitual.\n\nMantener el volumen general compensa los cambios tras el giro: puede haber un salto breve y puede afectar a otro cambio simultáneo. Desactívalo si notas interferencias.\n\nMinimizar mantiene WheelMix en la bandeja. Cerrar restaura los volúmenes y termina la aplicación.",
            "Usar WheelMix",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }
    void ShowDiagnostics() {
        if(diagnosticWindow is {IsDisposed:false}) {diagnosticWindow.Activate();return;}
        diagnosticWindow=new Form {Text="Diagnóstico · WheelMix",Size=new Size(900,530),BackColor=Theme.Background,ForeColor=Theme.Text,StartPosition=FormStartPosition.CenterParent};
        diagnosticText=new TextBox {Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=Theme.Card,ForeColor=Theme.Muted,Font=new Font("Consolas",9),Text=string.Join(Environment.NewLine,lines)};
        var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=56,Padding=new Padding(10)};
        footer.Controls.Add(Theme.Button("Abrir registros",()=>Process.Start(new ProcessStartInfo(Program.DataDir){UseShellExecute=true})));
        footer.Controls.Add(Theme.Button("Detectar de nuevo",()=> {raw?.Enumerate();nextPoll=0;}));
        diagnosticWindow.Controls.Add(diagnosticText);diagnosticWindow.Controls.Add(footer);diagnosticWindow.Show(this);
    }
    void Log(string message) {
        if(IsDisposed)return;
        string line=DateTime.Now.ToString("HH:mm:ss.fff")+" "+message;
        if(diagnostic)Console.WriteLine(line);
        try {if(logfile.BaseStream.Length<20_000_000)logfile.WriteLine(line);}catch(ObjectDisposedException){return;}
        lines.Enqueue(line);while(lines.Count>400)lines.Dequeue();
        if(diagnosticText is {IsDisposed:false}) {if(diagnosticText.TextLength>60000)diagnosticText.Clear();diagnosticText.AppendText(line+Environment.NewLine);}
    }
    void Restore(){Show();WindowState=FormWindowState.Normal;Activate();}
    protected override void WndProc(ref Message m) {
        if(m.Msg==Program.ActivateMessage){Restore();return;}base.WndProc(ref m);
    }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);int dark=1;DwmSetWindowAttribute(Handle,20,ref dark,sizeof(int));
    }
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
}
