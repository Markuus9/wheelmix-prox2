using System.Diagnostics;
namespace ControlAudioLogitech;
sealed class SettingsDialog : Form {
    readonly NumericUpDown step;
    readonly CheckBox reverse,guard,startup;
    readonly ComboBox backend;
    readonly CheckedListBox apps;
    readonly Label selectedStatus;
    public bool RequestedStartup {get;private set;}
    public Settings? Value {get;private set;}
    public SettingsDialog(Settings config,IEnumerable<string> running) {
        Text="Preferencias · WheelMix";ClientSize=new Size(660,760);MinimumSize=new Size(650,650);
        StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Background;ForeColor=Theme.Text;
        Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        var body=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(24)};
        body.Controls.Add(Theme.Label("Hazlo a tu manera",21,null,FontStyle.Bold));
        body.Controls.Add(Theme.Label("Marcar o desmarcar no cambia nada hasta pulsar Guardar.",10,Theme.Muted));
        var row=Theme.Row();row.Controls.Add(Theme.Label("Paso por giro (%)",11));
        step=new NumericUpDown {Minimum=1,Maximum=100,Value=(decimal)(config.Step*100),Width=70,BackColor=Theme.Card,ForeColor=Theme.Text,Margin=new Padding(20,0,0,0)};
        row.Controls.Add(step);body.Controls.Add(row);
        reverse=Theme.Check("Invertir dirección de la rueda",config.Reverse);body.Controls.Add(reverse);
        guard=Theme.Check("Compensar el volumen general al girar",config.Compensate);body.Controls.Add(guard);
        AddNote(body,"Activado: intenta devolver el volumen general al nivel anterior después de cada giro. Desactivado: la rueda también cambia el volumen de Windows. Puede haber un salto breve; no es un bloqueo.");
        startup=Theme.Check("Abrir WheelMix al iniciar sesión en Windows",Startup.Enabled);body.Controls.Add(startup);
        var startupStatus=Theme.Label(Startup.Status,9,Theme.Muted);startupStatus.MaximumSize=new Size(590,0);body.Controls.Add(startupStatus);
        startup.CheckedChanged+=(_,_)=>startupStatus.Text=startup.Checked?"Pendiente de guardar: registrar esta copia para abrirla en la bandeja.":"Pendiente de guardar: quitar WheelMix del inicio automático.";
        var startupLink=new LinkLabel {Text="Comprobar en Aplicaciones de inicio de Windows",AutoSize=true,LinkColor=Theme.Accent,Margin=new Padding(0,0,0,10)};
        startupLink.LinkClicked+=(_,_)=> {try{Process.Start(new ProcessStartInfo("ms-settings:startupapps"){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,"No se pudo abrir Windows");}};
        body.Controls.Add(startupLink);
        body.Controls.Add(Theme.Label("Aplicaciones de chat",13,null,FontStyle.Bold));
        AddNote(body,"Marcada = Chat. Desmarcada = Game. La lista reúne tus apps guardadas y las que ahora tienen una sesión de audio.");
        apps=new CheckedListBox {Width=590,Height=150,CheckOnClick=true,BackColor=Theme.Card,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,IntegralHeight=false};
        var selected=config.ChatApps.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var name in selected.Concat(running).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n=>n))apps.Items.Add(name,selected.Contains(name));
        body.Controls.Add(apps);
        selectedStatus=Theme.Label("Solo las aplicaciones marcadas se guardan como Chat.",9,Theme.Muted);
        selectedStatus.MaximumSize=new Size(590,0);body.Controls.Add(selectedStatus);
        var addRow=Theme.Row();
        var browse=Theme.Button("Elegir archivo .exe…",ChooseExecutable,true);browse.Name="chooseExeButton";addRow.Controls.Add(browse);
        addRow.Controls.Add(Theme.Button("Quitar de Chat",()=> {if(apps.SelectedIndex>=0)apps.SetItemChecked(apps.SelectedIndex,false);}));
        body.Controls.Add(addRow);
        AddNote(body,"Se usa el nombre del ejecutable, no su carpeta, para que siga funcionando tras actualizar la app. Selecciona el programa que reproduce audio, no su instalador ni Update.exe.");
        var advanced=Theme.Row();advanced.Controls.Add(Theme.Label("Motor de audio",10,Theme.Muted));
        backend=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=160,BackColor=Theme.Card,ForeColor=Theme.Text,Margin=new Padding(16,0,0,0)};
        backend.Items.AddRange(new object[]{"Windows","Sonar"});backend.SelectedItem=config.Backend=="Sonar"?"Sonar":"Windows";advanced.Controls.Add(backend);body.Controls.Add(advanced);
        var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=66,Padding=new Padding(24,12,24,12),FlowDirection=FlowDirection.RightToLeft};
        var save=Theme.Button("Guardar",()=> {
            Value=new Settings {Step=(double)step.Value/100,Reverse=reverse.Checked,Compensate=guard.Checked,Backend=backend.SelectedItem?.ToString()??"Windows",ChatApps=string.Join(",",apps.CheckedItems.Cast<string>())};
            RequestedStartup=startup.Checked;DialogResult=DialogResult.OK;Close();
        },true);
        footer.Controls.Add(save);footer.Controls.Add(Theme.Button("Cancelar",()=> {DialogResult=DialogResult.Cancel;Close();}));
        AcceptButton=save;Controls.Add(body);Controls.Add(footer);
    }
    void AddNote(Control body,string text){var label=Theme.Label(text,9,Theme.Muted);label.MaximumSize=new Size(590,0);body.Controls.Add(label);}
    void ChooseExecutable() {
        using var picker=new OpenFileDialog {Title="Seleccionar aplicación de Chat",Filter="Aplicaciones (*.exe)|*.exe",CheckFileExists=true,Multiselect=true,DereferenceLinks=true};
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        foreach(var path in picker.FileNames)AddExecutable(path);
    }
    internal void AddExecutable(string path) {
        string name=ChatApplications.FromExecutable(path);
        int index=apps.Items.Cast<string>().ToList().FindIndex(s=>string.Equals(s,name,StringComparison.OrdinalIgnoreCase));
        if(index>=0)apps.SetItemChecked(index,true);else apps.Items.Add(name,true);
        selectedStatus.Text=name+".exe añadido a Chat. Pulsa Guardar para aplicarlo.";
    }
    internal int SelectionCount=>apps.CheckedItems.Count;
}
