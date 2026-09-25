namespace ControlAudioLogitech;
sealed class HelpDialog : Form {
    public HelpDialog() {
        Text=L.Text("help.title");ClientSize=new Size(640,620);MinimumSize=new Size(600,500);
        BackColor=Theme.Background;ForeColor=Theme.Text;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;
        var body=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(26)};
        void Section(string title,string text) {
            body.Controls.Add(Theme.Label(title,13,null,FontStyle.Bold));
            var detail=Theme.Label(text,10,Theme.Muted);detail.MaximumSize=new Size(555,0);detail.Margin=new Padding(0,0,0,18);body.Controls.Add(detail);
        }
        body.Controls.Add(Theme.LabelKey("help.heading",23,null,FontStyle.Bold));
        Section(L.Text("help.mixTitle"),L.Text("help.mix"));
        Section(L.Text("help.headsetTitle"),L.Text("help.headset"));
        Section(L.Text("help.guardTitle"),L.Text("help.guard"));
        Section(L.Text("help.startupTitle"),L.Text("help.startup"));
        Section(L.Text("settings.chat"),L.Text("help.apps"));
        Section(L.Text("help.trayTitle"),L.Text("help.tray"));
        var close=Theme.ButtonKey("common.ok",Close,true);close.Dock=DockStyle.Bottom;close.Height=44;
        Controls.Add(body);Controls.Add(close);AcceptButton=close;
    }
}
