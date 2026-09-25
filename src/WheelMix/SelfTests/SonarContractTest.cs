using System.Net;
namespace ControlAudioLogitech;
static class SonarContractTest {
    public static void Run(Action<bool,string> check) {
        var fake = new Fake();
        using var sonar = new Sonar(fake, new Uri("http://127.0.0.1:12345/"));
        check(sonar.Read().GetAwaiter().GetResult() == .2, "Sonar GET /v1/chatMix");
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try {
            System.Globalization.CultureInfo.CurrentCulture = new("es-ES");
            sonar.Write(.35).GetAwaiter().GetResult();
            check(fake.Last == "PUT /v1/chatMix?balance=0.35", "Sonar PUT decimal independiente del idioma");
        } finally { System.Globalization.CultureInfo.CurrentCulture = old; }
        var legacy = new Fake { Legacy = true };
        using var fallback = new Sonar(legacy, new Uri("http://127.0.0.1:12345/"));
        check(fallback.Read().GetAwaiter().GetResult() == .2, "Sonar fallback solo ante 404");
        fallback.Write(-.1).GetAwaiter().GetResult();
        check(legacy.Last == "PUT /chatMix?balance=-0.1", "Sonar escritura usa ruta detectada");
        bool rejected = false;
        try { sonar.Write(double.NaN).GetAwaiter().GetResult(); } catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "Sonar rechaza NaN antes de HTTP");
    }
    sealed class Fake : HttpMessageHandler {
        public bool Legacy;
        public string Last = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) {
            Last = r.Method + " " + r.RequestUri!.PathAndQuery;
            if (Legacy && r.RequestUri.AbsolutePath == "/v1/chatMix")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"balance\":0.2}") });
        }
    }
}
