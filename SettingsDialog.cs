using System.Diagnostics;
namespace ControlAudioLogitech;
sealed class SettingsDialog : Form {
    readonly NumericUpDown step;
    readonly CheckBox reverse,guard,startup;
    readonly ComboBox backend,language;
    readonly CheckedListBox apps;
    readonly Label selectedStatus;
    public bool RequestedStartup {get;private set;}
    public Settings? Value {get;private set;}
    public SettingsDialog(Settings config,IEnumerable<string> running) {
        Text=L.Text("settings.title");ClientSize=new Size(660,760);MinimumSize=new Size(650,650);
        StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Background;ForeColor=Theme.Text;
        Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        var body=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(24)};
        body.Controls.Add(Theme.LabelKey("settings.heading",21,null,FontStyle.Bold));
        body.Controls.Add(Theme.LabelKey("settings.saveHint",10,Theme.Muted));
        body.Controls.Add(Theme.LabelKey("settings.language",12,null,FontStyle.Bold));
        language=new ComboBox {Name="languageSelector",DropDownStyle=ComboBoxStyle.DropDownList,Width=360,BackColor=Theme.Card,ForeColor=Theme.Text};
        foreach(var choice in L.Choices)language.Items.Add(choice);
        language.SelectedItem=language.Items.Cast<LanguageChoice>().FirstOrDefault(c=>c.Code==config.Language)??language.Items[0];
        body.Controls.Add(language);AddNote(body,L.Text("settings.languageHint"));
        var row=Theme.Row();row.Controls.Add(Theme.LabelKey("settings.step",11));
        step=new NumericUpDown {Minimum=1,Maximum=100,Value=(decimal)(config.Step*100),Width=70,BackColor=Theme.Card,ForeColor=Theme.Text,Margin=new Padding(20,0,0,0)};
        row.Controls.Add(step);body.Controls.Add(row);
        reverse=Theme.CheckKey("settings.reverse",config.Reverse);body.Controls.Add(reverse);
        guard=Theme.CheckKey("settings.compensate",config.Compensate);body.Controls.Add(guard);
        AddNote(body,L.Text("settings.compensateHint"));
        startup=Theme.CheckKey("settings.startup",Startup.Enabled);body.Controls.Add(startup);
        var startupStatus=Theme.Label(Startup.Status,9,Theme.Muted);startupStatus.MaximumSize=new Size(590,0);body.Controls.Add(startupStatus);
        startup.CheckedChanged+=(_,_)=>startupStatus.Text=startup.Checked?L.Text("startup.pendingOn"):L.Text("startup.pendingOff");
        var startupLink=new LinkLabel {Text=L.Text("startup.link"),AutoSize=true,LinkColor=Theme.Accent,Margin=new Padding(0,0,0,10)};
        startupLink.LinkClicked+=(_,_)=> {try{Process.Start(new ProcessStartInfo("ms-settings:startupapps"){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,L.Text("error.openWindows"));}};
        body.Controls.Add(startupLink);
        body.Controls.Add(Theme.LabelKey("settings.chat",13,null,FontStyle.Bold));
        AddNote(body,L.Text("settings.chatHint"));
        apps=new CheckedListBox {Width=590,Height=150,CheckOnClick=true,BackColor=Theme.Card,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,IntegralHeight=false};
        var selected=config.ChatApps.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var name in selected.Concat(running).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n=>n))apps.Items.Add(name,selected.Contains(name));
        body.Controls.Add(apps);
        selectedStatus=Theme.LabelKey("settings.selectedHint",9,Theme.Muted);
        selectedStatus.MaximumSize=new Size(590,0);body.Controls.Add(selectedStatus);
        var addRow=Theme.Row();
        var browse=Theme.ButtonKey("settings.browse",ChooseExecutable,true);browse.Name="chooseExeButton";addRow.Controls.Add(browse);
        addRow.Controls.Add(Theme.ButtonKey("settings.remove",()=> {if(apps.SelectedIndex>=0)apps.SetItemChecked(apps.SelectedIndex,false);}));
        body.Controls.Add(addRow);
        AddNote(body,L.Text("settings.exeHint"));
        var advanced=Theme.Row();advanced.Controls.Add(Theme.LabelKey("settings.backend",10,Theme.Muted));
        backend=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=160,BackColor=Theme.Card,ForeColor=Theme.Text,Margin=new Padding(16,0,0,0)};
        backend.Items.AddRange(new object[]{"Windows","Sonar"});backend.SelectedItem=config.Backend=="Sonar"?"Sonar":"Windows";advanced.Controls.Add(backend);body.Controls.Add(advanced);
        var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=66,Padding=new Padding(24,12,24,12),FlowDirection=FlowDirection.RightToLeft};
        var save=Theme.ButtonKey("common.save",()=> {
            Value=new Settings {Language=((LanguageChoice)language.SelectedItem!).Code,Step=(double)step.Value/100,Reverse=reverse.Checked,Compensate=guard.Checked,Backend=backend.SelectedItem?.ToString()??"Windows",ChatApps=string.Join(",",apps.CheckedItems.Cast<string>())};
            RequestedStartup=startup.Checked;DialogResult=DialogResult.OK;Close();
        },true);
        footer.Controls.Add(save);footer.Controls.Add(Theme.ButtonKey("common.cancel",()=> {DialogResult=DialogResult.Cancel;Close();}));
        AcceptButton=save;Controls.Add(body);Controls.Add(footer);
    }
    void AddNote(Control body,string text){var label=Theme.Label(text,9,Theme.Muted);label.MaximumSize=new Size(590,0);body.Controls.Add(label);}
    void ChooseExecutable() {
        using var picker=new OpenFileDialog {Title=L.Text("settings.picker"),Filter=L.Text("settings.filter"),CheckFileExists=true,Multiselect=true,DereferenceLinks=true};
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        foreach(var path in picker.FileNames)AddExecutable(path);
    }
    internal void AddExecutable(string path) {
        string name=ChatApplications.FromExecutable(path);
        int index=apps.Items.Cast<string>().ToList().FindIndex(s=>string.Equals(s,name,StringComparison.OrdinalIgnoreCase));
        if(index>=0)apps.SetItemChecked(index,true);else apps.Items.Add(name,true);
        selectedStatus.Text=L.Text("settings.added",name);
    }
    internal int SelectionCount=>apps.CheckedItems.Count;
}
