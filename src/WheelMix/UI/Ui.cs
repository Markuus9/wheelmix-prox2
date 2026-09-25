using System.Drawing.Drawing2D;
namespace ControlAudioLogitech;
static class Theme {
    public static Label LabelKey(string key,float size=10,Color? color=null,FontStyle style=FontStyle.Regular)=>L.Bind(Label("",size,color,style),key);
    public static Button ButtonKey(string key,Action action,bool primary=false)=>L.Bind(Button("",action,primary),key);
    public static CheckBox CheckKey(string key,bool value)=>L.Bind(Check("",value),key);
    public static readonly Color Background = Color.FromArgb(15,18,26), Card = Color.FromArgb(24,28,39),
        Border = Color.FromArgb(43,49,65), Text = Color.FromArgb(241,243,250), Muted = Color.FromArgb(161,171,192),
        Accent = Color.FromArgb(158,139,255), Mint = Color.FromArgb(107,224,197);
    public static Label Label(string text, float size = 10, Color? color = null, FontStyle style = FontStyle.Regular) =>
        new() { Text = text, AutoSize = true, ForeColor = color ?? Text, Font = new Font("Segoe UI", size, style), BackColor = Color.Transparent, Margin = new Padding(0,0,0,8) };
    public static Button Button(string text, Action action, bool primary = false) {
        var b = new Button { Text = text, AutoSize = true, Height = 38, MinimumSize = new Size(110,38),
            FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Color.FromArgb(35,40,55),
            ForeColor = primary ? Background : Text, Cursor = Cursors.Hand, Padding = new Padding(12,4,12,4),
            Margin = new Padding(0,0,8,0), Font = new Font("Segoe UI",10,FontStyle.Bold) };
        b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(180,165,255) : Border;
        b.Click += (_,_) => action(); return b;
    }
    public static FlowLayoutPanel Row() => new() { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0) };
    public static CheckBox Check(string text, bool value) => new() { Text = text, Checked = value, AutoSize = true,
        ForeColor = Text, Font = new Font("Segoe UI",11), Margin = new Padding(0,8,0,8), Cursor = Cursors.Hand };
}
sealed class CardPanel : Panel {
    public CardPanel() { DoubleBuffered = true; ResizeRedraw = true; BackColor = Theme.Card; Padding = new Padding(22); }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        using var p = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(p, 0,0,Width-1,Height-1);
    }
}
sealed class MixSurface : Control {
    double value;
    public double Value { get => value; set { double next=Math.Clamp(value,-1,1); if(Math.Abs(this.value-next)<.00001)return; this.value=next; Invalidate(); } }
    public double Step { get; set; } = .05;
    public event Action<double>? Requested;
    public MixSurface() {
        // Everything is drawn relative to Width: repaint fully on resize or stale copies remain.
        DoubleBuffered = true; ResizeRedraw = true; TabStop = true; Height = 170; Dock = DockStyle.Fill;
        BackColor = Theme.Card; Cursor = Cursors.Hand;
        AccessibleName = L.Text("mix.accessibleName");
        AccessibleDescription = L.Text("mix.accessibleHint");
        AccessibleRole = AccessibleRole.Slider;
        L.Changed+=RefreshLanguage;Disposed+=(_,_)=>L.Changed-=RefreshLanguage;
    }
    void RefreshLanguage() {
        AccessibleName=L.Text("mix.accessibleName");AccessibleDescription=L.Text("mix.accessibleHint");Invalidate();
    }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Home || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Left) Requested?.Invoke(Math.Clamp(value-Step,-1,1));
        if (e.KeyCode == Keys.Right) Requested?.Invoke(Math.Clamp(value+Step,-1,1));
        if (e.KeyCode == Keys.Home) Requested?.Invoke(0);
        if(e.KeyCode is Keys.Left or Keys.Right or Keys.Home)e.SuppressKeyPress=true;
        base.OnKeyDown(e);
    }
    void Pick(int x) { double v = Math.Clamp((x-28.0*DeviceDpi/96)/(Width-56.0*DeviceDpi/96)*2-1,-1,1); Requested?.Invoke(Math.Abs(v)<.04 ? 0 : Math.Round(v,2)); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button==MouseButtons.Left) { Focus(); Capture=true; Pick(e.X); } }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(Capture && e.Button==MouseButtons.Left) Pick(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { Capture=false; base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
        float scale=DeviceDpi/96f, y=100*scale, left=28*scale, right=Width-left;
        using var big = new Font("Segoe UI",27,FontStyle.Bold); using var small = new Font("Segoe UI",10,FontStyle.Bold);
        using var mid = new Font("Segoe UI",12,FontStyle.Bold);
        var game = ((int)Math.Round(WindowsMixer.Factor(false,value)*100))+"%";
        var chat = ((int)Math.Round(WindowsMixer.Factor(true,value)*100))+"%";
        TextRenderer.DrawText(g,game,big,new Rectangle(0,0,Width/3,(int)(50*scale)),Theme.Accent,TextFormatFlags.Left);
        TextRenderer.DrawText(g,chat,big,new Rectangle(Width*2/3,0,Width/3,(int)(50*scale)),Theme.Mint,TextFormatFlags.Right);
        TextRenderer.DrawText(g,"GAME",small,new Rectangle(0,(int)(49*scale),Width/3,(int)(24*scale)),Theme.Muted,TextFormatFlags.Left);
        TextRenderer.DrawText(g,"CHAT",small,new Rectangle(Width*2/3,(int)(49*scale),Width/3,(int)(24*scale)),Theme.Muted,TextFormatFlags.Right);
        string caption = value == 0 ? L.Text("mix.center") : value < 0 ? L.Text("mix.game") : L.Text("mix.chat");
        TextRenderer.DrawText(g,caption,mid,new Rectangle(Width/3,(int)(17*scale),Width/3,(int)(40*scale)),Theme.Text,TextFormatFlags.HorizontalCenter);
        using var gradient = new LinearGradientBrush(new PointF(left,y),new PointF(right,y),Theme.Accent,Theme.Mint);
        using var track = new Pen(gradient,8*scale) { StartCap=LineCap.Round,EndCap=LineCap.Round };
        g.DrawLine(track,left,y,right,y);
        using var tick = new Pen(Theme.Muted,scale);
        g.DrawLine(tick,Width/2f,y-16*scale,Width/2f,y+16*scale);
        float x=left+(float)((value+1)/2)*(right-left), radius=12*scale;
        using var fill = new SolidBrush(Theme.Text);
        g.FillEllipse(fill,x-radius,y-radius,radius*2,radius*2);
        using var inner = new SolidBrush(Theme.Card);
        g.FillEllipse(inner,x-4*scale,y-4*scale,8*scale,8*scale);
        TextRenderer.DrawText(g,L.Text("mix.hint"),Font,
            new Rectangle(0,(int)(132*scale),Width,(int)(28*scale)),Theme.Muted,TextFormatFlags.HorizontalCenter);
        if(Focused) ControlPaint.DrawFocusRectangle(g,ClientRectangle,Theme.Accent,Theme.Card);
    }
}
