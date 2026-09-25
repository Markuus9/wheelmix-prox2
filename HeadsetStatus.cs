using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace ControlAudioLogitech;
enum HeadsetLink { Unknown, Connected, Disconnected }
sealed record HeadsetReading(HeadsetLink Link, int? Battery=null, string? Detail=null);
static class HeadsetStatus {
    // Centurion battery query for PRO X 2. Protocol reference:
    // https://github.com/Ayerdi/PROX2-AutoSwitch/blob/main/lib/LogitechProX2Centurion.psm1
    // Missing/timeout/unknown responses are never treated as physical OFF.
    public static HeadsetReading Parse(ReadOnlySpan<byte> packet) {
        if(packet.Length>=13 && packet[0]==0x51 && packet[1]==0x0b && packet[8]==4 && packet[10]<=100)
            return new(HeadsetLink.Connected,packet[10]);
        if(packet.Length>=8 && packet[..8].SequenceEqual(new byte[]{0x51,5,0,0xff,3,0x1a,0x0b,0}))
            return new(HeadsetLink.Disconnected);
        return new(HeadsetLink.Unknown);
    }
    public static async Task<HeadsetReading> Query(string path,CancellationToken cancellation=default) {
        try {
            using var handle=CreateFile(path,0xc0000000,3,0,3,0x40000000,0);
            if(handle.IsInvalid)return new(HeadsetLink.Unknown,Detail:"No se pudo abrir el canal de estado.");
            if(!HidD_GetPreparsedData(handle,out var descriptor))return new(HeadsetLink.Unknown,Detail:"Descriptor no disponible.");
            byte[] caps=new byte[64];
            try {if(HidP_GetCaps(descriptor,caps)<0)return new(HeadsetLink.Unknown);}
            finally {HidD_FreePreparsedData(descriptor);}
            if(BitConverter.ToUInt16(caps,0)!=1||BitConverter.ToUInt16(caps,2)!=0xffa0||
                BitConverter.ToUInt16(caps,4)!=64||BitConverter.ToUInt16(caps,6)!=64)
                return new(HeadsetLink.Unknown,Detail:"Colección HID de estado no compatible.");
            using var stream=new FileStream(handle,FileAccess.ReadWrite,64,isAsync:true);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(900);
            byte[] request=new byte[64];
            request[0]=0x51;request[1]=8;request[3]=3;request[4]=0x1a;request[6]=3;request[8]=4;request[9]=0x0a;
            await stream.WriteAsync(request,timeout.Token).ConfigureAwait(false);
            byte[] response=new byte[64];
            for(int i=0;i<16;i++) {
                int count=await stream.ReadAsync(response,timeout.Token).ConfigureAwait(false);
                if(count==0)break;
                var reading=Parse(response.AsSpan(0,count));
                if(reading.Link!=HeadsetLink.Unknown)return reading;
            }
            return new(HeadsetLink.Unknown,Detail:"Respuesta de estado desconocida.");
        }catch(OperationCanceledException){return new(HeadsetLink.Unknown,Detail:"Sin respuesta de estado.");}
        catch(Exception e){return new(HeadsetLink.Unknown,Detail:e.Message);}
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern SafeFileHandle CreateFile(string path,uint access,uint share,nint security,uint creation,uint flags,nint template);
    [DllImport("hid.dll")]static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out nint descriptor);
    [DllImport("hid.dll")]static extern bool HidD_FreePreparsedData(nint descriptor);
    [DllImport("hid.dll")]static extern int HidP_GetCaps(nint descriptor,[Out]byte[] caps);
}
