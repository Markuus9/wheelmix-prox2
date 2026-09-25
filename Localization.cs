using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ControlAudioLogitech;

// Translation files contain data only. English is embedded as a guaranteed fallback.
sealed record LanguageChoice(string Code,string Name) {
    public override string ToString()=>Name;
}
sealed class LanguagePack {
    public string Code {get;set;}="";
    public string Name {get;set;}="";
    public Dictionary<string,string> Strings {get;set;}=new();
}
static class L {
    static readonly Dictionary<string,LanguagePack> packs=new(StringComparer.OrdinalIgnoreCase);
    static readonly JsonSerializerOptions json=new(){PropertyNameCaseInsensitive=true};
    public static event Action? Changed;
    public static string Current {get;private set;}="en";
    static L() {
        var assembly=Assembly.GetExecutingAssembly();
        foreach(string resource in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("WheelMix.Locales."))) {
            using var stream=assembly.GetManifestResourceStream(resource)!;
            var pack=JsonSerializer.Deserialize<LanguagePack>(stream,json)!;
            packs[pack.Code]=pack;
        }
        string directory=Path.Combine(AppContext.BaseDirectory,"locales");
        if(Directory.Exists(directory))foreach(string path in Directory.EnumerateFiles(directory,"*.json")) {
            try {
                var pack=JsonSerializer.Deserialize<LanguagePack>(File.ReadAllText(path),json);
                if(pack!=null&&pack.Code!="en"&&Regex.IsMatch(pack.Code,@"^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$")&&!string.IsNullOrWhiteSpace(pack.Name)
                   &&Validate(pack).Count==0)packs[pack.Code]=pack;
            }catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) {
                System.Diagnostics.Trace.WriteLine("Ignoring language file "+path+": "+e.Message);
            }
        }
    }
    public static IReadOnlyList<string> Validate(LanguagePack pack) {
        var errors=new List<string>();
        var english=packs["en"].Strings;
        foreach(var (key,value) in pack.Strings) {
            if(!english.TryGetValue(key,out var baseline)){errors.Add("Unknown key: "+key);continue;}
            if(value==null||!Placeholders(value).SequenceEqual(Placeholders(baseline)))errors.Add("Invalid placeholders: "+key);
            else try{_ = string.Format(CultureInfo.InvariantCulture,value,Enumerable.Repeat<object>("test",10).ToArray());}
                 catch(FormatException){errors.Add("Invalid format: "+key);}
        }
        return errors;
    }
    static IEnumerable<string> Placeholders(string text)=>Regex.Matches(text,@"\{\d+\}").Select(m=>m.Value).OrderBy(s=>s);
    public static IEnumerable<LanguageChoice> Choices=>new[]{new LanguageChoice("auto",Text("language.auto"))}
        .Concat(packs.Values.OrderBy(p=>p.Name).Select(p=>new LanguageChoice(p.Code,p.Name)));
    public static IReadOnlyDictionary<string,LanguagePack> Packs=>packs;
    public static void SetLanguage(string? code) {
        if(string.IsNullOrWhiteSpace(code)||code=="auto")code=CultureInfo.CurrentUICulture.Name;
        Current=packs.ContainsKey(code)?code:packs.ContainsKey(code.Split('-')[0])?code.Split('-')[0]:"en";
        Changed?.Invoke();
    }
    public static string Text(string key,params object[] values) {
        string text=packs[Current].Strings.GetValueOrDefault(key)??packs["en"].Strings.GetValueOrDefault(key)??key;
        return values.Length==0?text:string.Format(CultureInfo.CurrentCulture,text,values);
    }
    public static T Bind<T>(T control,string key) where T:Control {
        void Update()=>control.Text=Text(key);
        Update();Changed+=Update;control.Disposed+=(_,_)=>Changed-=Update;
        return control;
    }
}
