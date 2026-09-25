using Microsoft.Win32;
namespace ControlAudioLogitech;
sealed class SettingsDialog : Form {
    readonly NumericUpDown step;
    readonly CheckBox reverse, guard, startup;
    readonly ComboBox backend;
    readonly CheckedListBox apps;
    public SettingsDialog(Settings config, IEnumerable<string> running) {
        Text = "Preferencias · WheelMix"; ClientSize = new Size(620,640); MinimumSize=Size;
        StartPosition=FormStartPosition.CenterParent; BackColor=Theme.Background; ForeColor=Theme.Text;
        Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
        var body = new FlowLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(24) };
        body.Controls.Add(Theme.Label("Hazlo a tu manera",21,null,FontStyle.Bold));
        body.Controls.Add(Theme.Label("Los cambios se aplican al guardar.",10,Theme.Muted));
        var row=Theme.Row(); row.Controls.Add(Theme.Label("Paso por giro (%)",11));
        step=new NumericUpDown { Minimum=1,Maximum=100,Value=(decimal)(config.Step*100),Width=70,BackColor=Theme.Card,ForeColor=Theme.Text,Margin=new Padding(20,0,0,0) };
        row.Controls.Add(step); body.Controls.Add(row);
        reverse=Theme.Check("Invertir dirección de la rueda",config.Reverse); body.Controls.Add(reverse);
        guard=Theme.Check("Mantener el volumen general",config.Compensate); body.Controls.Add(guard);
        var note=Theme.Label("Compensa el cambio de volumen tras cada giro. Puede producir un salto breve o afectar cambios simultáneos. No es un bloqueo del control de Windows.",9,Theme.Muted);
        note.MaximumSize=new Size(535,0); body.Controls.Add(note);
        startup=Theme.Check("Iniciar al entrar en Windows",Startup.Enabled); body.Controls.Add(startup);
        body.Controls.Add(Theme.Label("Aplicaciones de chat",13,null,FontStyle.Bold));
        body.Controls.Add(Theme.Label("Marca las apps de voz. El resto del audio será Game.",9,Theme.Muted));
        apps=new CheckedListBox { Width=540,Height=145,CheckOnClick=true,BackColor=Theme.Card,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,IntegralHeight=false };
        var selected=config.ChatApps.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var name in selected.Concat(running).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n=>n)) apps.Items.Add(name,selected.Contains(name));
        body.Controls.Add(apps);
        var addRow=Theme.Row();
        var custom=new TextBox { PlaceholderText="Añadir aplicación (nombre del proceso)",Width=360,BackColor=Theme.Card,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle };
        addRow.Controls.Add(custom);
        addRow.Controls.Add(Theme.Button("Añadir",()=> {
            string name=custom.Text.Trim(); if(name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)) name=name[..^4];
            if(name.Length==0 || name.Contains(',') || name.Contains('\\') || name.Contains('/')) return;
            int index=apps.FindStringExact(name);
            if(index>=0) apps.SetItemChecked(index,true); else apps.Items.Add(name,true);
            custom.Clear();
        })); body.Controls.Add(addRow);
        var advanced=Theme.Row(); advanced.Controls.Add(Theme.Label("Motor de audio",10,Theme.Muted));
        backend=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=160,BackColor=Theme.Card,ForeColor=Theme.Text,Margin=new Padding(16,0,0,0) };
        backend.Items.AddRange(new object[]{"Windows","Sonar"}); backend.SelectedItem=config.Backend=="Sonar"?"Sonar":"Windows"; advanced.Controls.Add(backend); body.Controls.Add(advanced);
        var footer=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=66,Padding=new Padding(24,12,24,12),FlowDirection=FlowDirection.RightToLeft };
        var save=Theme.Button("Guardar",()=> {
            Value=new Settings { Step=(double)step.Value/100,Reverse=reverse.Checked,Compensate=guard.Checked,
                Backend=backend.SelectedItem?.ToString()??"Windows",ChatApps=string.Join(",",apps.CheckedItems.Cast<string>()) };
            try { Startup.Set(startup.Checked); }
            catch(Exception e) { MessageBox.Show(this,e.Message,"No se pudo cambiar el inicio automático"); return; }
            DialogResult=DialogResult.OK; Close();
        },true);
        footer.Controls.Add(save); footer.Controls.Add(Theme.Button("Cancelar",()=> { DialogResult=DialogResult.Cancel; Close(); }));
        AcceptButton=save; Controls.Add(body); Controls.Add(footer);
    }
    public Settings? Value { get; private set; }
}
static class Startup {
    const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled { get { using var k=Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue("ControlAudioLogitech")!=null; } }
    public static void Set(bool value) {
        using var key=Registry.CurrentUser.CreateSubKey(Key);
        if(value) key.SetValue("ControlAudioLogitech","\""+Environment.ProcessPath+"\" --tray");
        else key.DeleteValue("ControlAudioLogitech",false);
    }
}
