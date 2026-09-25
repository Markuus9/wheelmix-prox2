using NAudio.CoreAudioApi;
namespace ControlAudioLogitech;

// Compensation, not a device-specific input filter. Concurrent changes can be reverted.
sealed class VolumeGuard : IDisposable {
    readonly MMDeviceEnumerator enumerator=new();
    readonly Dictionary<string,Endpoint> endpoints=new();
    readonly Dictionary<string,float> restore=new();
    readonly Action<string> log;
    long until,lastRefresh,retryAfter;
    string? failure;
    sealed class Endpoint {
        public MMDevice Device;
        public readonly Queue<(long Time,float Volume)> History=new();
        public Endpoint(MMDevice device){Device=device;}
    }
    public bool Enabled {get;set;}
    public int Corrections {get;private set;}
    public DateTime? LastCorrection {get;private set;}
    public string Description=>!Enabled?"Compensación desactivada: la rueda también cambia el volumen de Windows.":!IsOperational?"Compensación en espera de una salida de audio.":$"Compensación activa · {Corrections} correcciones esta sesión · última: {(LastCorrection.HasValue?LastCorrection.Value.ToString("HH:mm:ss"):"ninguna")}";
    public bool IsOperational=>Enabled&&endpoints.Count>0&&failure==null;
    public VolumeGuard(Action<string> log){this.log=log;}
    void Refresh() {
        using var def=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
        var wanted=new HashSet<string>();
        foreach(var d in enumerator.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active)) {
            string id=d.ID;
            if(id==def.ID||d.FriendlyName.Contains("PRO X 2",StringComparison.OrdinalIgnoreCase)) {
                wanted.Add(id);
                if(endpoints.ContainsKey(id))d.Dispose(); // Keep existing history across refreshes.
                else endpoints.Add(id,new Endpoint(d));
            } else d.Dispose();
        }
        foreach(string id in endpoints.Keys.Where(id=>!wanted.Contains(id)).ToArray()) {
            endpoints[id].Device.Dispose();endpoints.Remove(id);restore.Remove(id);
        }
    }
    public void Tick() {
        if(!Enabled){Cancel();return;}
        long now=Environment.TickCount64;
        if(now<retryAfter)return;
        try {
            if(now-lastRefresh>3000&&now>until){Refresh();lastRefresh=now;}
            foreach(var (id,e) in endpoints) {
                float value=e.Device.AudioEndpointVolume.MasterVolumeLevelScalar;
                if(now<=until&&restore.TryGetValue(id,out float baseline)) {
                    if(Math.Abs(value-baseline)>.0001f) {
                        e.Device.AudioEndpointVolume.MasterVolumeLevelScalar=baseline;Corrections++;LastCorrection=DateTime.Now;
                        log($"Compensación: {e.Device.FriendlyName} {value:P0} → {baseline:P0}");
                    }
                } else {
                    e.History.Enqueue((now,value));
                    while(e.History.Count>0&&now-e.History.Peek().Time>500)e.History.Dequeue();
                }
            }
            failure=null;
        }catch(Exception ex) {
            Cancel();retryAfter=now+3000;lastRefresh=0;
            if(failure!=ex.Message)log("Compensación en espera: "+ex.Message);
            failure=ex.Message;
            foreach(var e in endpoints.Values)e.Device.Dispose();
            endpoints.Clear();
        }
    }
    public void Cancel(){restore.Clear();until=0;}
    public void Wheel() {
        if(!IsOperational)return;
        long now=Environment.TickCount64;
        if(now>until) {
            restore.Clear();
            foreach(var (id,e) in endpoints) {
                var samples=e.History.Where(s=>s.Time<=now-80).ToArray();
                if(samples.Length>0)restore[id]=samples[^1].Volume;
            }
        }
        until=now+180;Tick();
    }
    public void Dispose(){foreach(var e in endpoints.Values)e.Device.Dispose();enumerator.Dispose();}
}
