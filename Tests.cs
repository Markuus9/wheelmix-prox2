using System.Text.Json;
namespace ControlAudioLogitech;
static class Tests {
    public static int Run() {
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); count++; }
        try {
            Check(Mix.Move(.98,1,.05,false) == 1, "límite Chat");
            Check(Mix.Move(-.98,-1,.05,false) == -1, "límite Game");
            Check(Mix.Move(0,1,.05,true) == -.05, "dirección inversa");
            Check(Mix.Move(-.05,1,.05,false) == 0, "centro exacto");
            using var valid = JsonDocument.Parse("{\"balance\":0.25,\"state\":\"enabled\"}");
            Check(Sonar.ParseBalance(valid.RootElement) == .25, "lectura balance");
            bool rejected = false;
            try { using var bad = JsonDocument.Parse("{\"balance\":2}"); Sonar.ParseBalance(bad.RootElement); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "rechazar balance inválido");
            rejected = false;
            try { using var bad = JsonDocument.Parse("{}"); Sonar.ParseBalance(bad.RootElement); } catch (KeyNotFoundException) { rejected = true; }
            Check(rejected, "no inventar centro ante respuesta desconocida");
            Check(WindowsMixer.Factor(true, 1) == 1 && WindowsMixer.Factor(false, 1) == 0, "extremo Chat");
            Check(WindowsMixer.Factor(true, -1) == 0 && WindowsMixer.Factor(false, -1) == 1, "extremo Game");
            Check(WindowsMixer.Factor(true, 0) == 1 && WindowsMixer.Factor(false, 0) == 1, "centro sin atenuación");
            SonarContractTest.Run(Check);
            Console.WriteLine($"{count} pruebas correctas."); return 0;
        } catch (Exception e) { Console.WriteLine(e); return 1; }
    }
}
