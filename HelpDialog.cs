namespace ControlAudioLogitech;
sealed class HelpDialog : Form {
    public HelpDialog() {
        Text="Ayuda · WheelMix";ClientSize=new Size(640,620);MinimumSize=new Size(600,500);
        BackColor=Theme.Background;ForeColor=Theme.Text;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterParent;
        var body=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(26)};
        void Section(string title,string text) {
            body.Controls.Add(Theme.Label(title,13,null,FontStyle.Bold));
            var detail=Theme.Label(text,10,Theme.Muted);detail.MaximumSize=new Size(555,0);detail.Margin=new Padding(0,0,0,18);body.Controls.Add(detail);
        }
        body.Controls.Add(Theme.Label("Cómo usar WheelMix",23,null,FontStyle.Bold));
        Section("Mezcla juego y voz","Reproduce audio en Discord y un juego o música. Girar hacia subir favorece Chat; hacia bajar favorece Game. Puedes invertir el sentido en Preferencias. Centro recupera los volúmenes originales.");
        Section("Receptor y auricular","El receptor USB puede seguir conectado con el auricular apagado. WheelMix consulta el enlace del auricular cada pocos segundos. Si no recibe una respuesta válida, muestra estado sin confirmar.");
        Section("Compensar volumen general","La rueda también envía una orden de volumen a Windows. Esta opción intenta deshacer ese cambio después del giro. No fija un volumen absoluto ni bloquea la orden. Puede haber un salto breve o afectar cambios simultáneos. El estado y las últimas correcciones aparecen en Diagnóstico.");
        Section("Inicio con Windows","Marca o desmarca la opción y pulsa Guardar. Al activarla se registra esta copia para abrirse en la bandeja cuando inicies sesión. Al desactivarla se elimina esa entrada. Windows puede bloquearla desde Aplicaciones de inicio: compruébalo con el enlace de Preferencias.");
        Section("Aplicaciones de chat","Las marcadas se atenúan como Chat y las demás como Game. Elegir archivo .exe añade su nombre; no lo ejecuta ni crea una salida de audio. Si varias apps usan el mismo nombre de proceso, se agrupan. Usa Discord de escritorio para separarlo del navegador.");
        Section("Bandeja y salida","Minimizar mantiene WheelMix activo. Pausar centra la mezcla y deja funcionar la rueda de volumen normal. Cerrar restaura los volúmenes y termina la app.");
        var close=Theme.Button("Entendido",Close,true);close.Dock=DockStyle.Bottom;close.Height=44;
        Controls.Add(body);Controls.Add(close);AcceptButton=close;
    }
}
