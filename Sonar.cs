using System.Globalization;
using System.Net;
using System.Text.Json;

namespace ControlAudioLogitech;
sealed class Sonar : IMixer
{
    public string Description => "Sonar conectado";
    readonly HttpClient http;
    Uri? endpoint;
    string route = "v1/chatMix";
    public Sonar()
    {
        http = new HttpClient(new HttpClientHandler {
            UseProxy = false, AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (r, c, ch, e) => r.RequestUri?.IsLoopback == true
        }) { Timeout = TimeSpan.FromSeconds(3) };
    }
    internal Sonar(HttpMessageHandler handler, Uri testEndpoint) {
        http = new HttpClient(handler); endpoint = Local(testEndpoint.ToString());
    }
    static Uri Local(string s) {
        var u = new Uri(s);
        if (!u.IsLoopback || (u.Scheme != "http" && u.Scheme != "https") || u.UserInfo.Length != 0)
            throw new InvalidDataException("Sonar debe usar una dirección local HTTP/HTTPS.");
        return u;
    }
    async Task Discover()
    {
        string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SteelSeries", "GG", "coreProps.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(file));
        string address = doc.RootElement.GetProperty("ggEncryptedAddress").GetString()!;
        using var apps = JsonDocument.Parse(await http.GetStringAsync(Local("https://" + address + "/subApps")));
        var sonar = apps.RootElement.GetProperty("subApps").GetProperty("sonar");
        if (!sonar.GetProperty("isRunning").GetBoolean()) throw new InvalidOperationException("Activa Sonar en SteelSeries GG.");
        endpoint = Local(sonar.GetProperty("metadata").GetProperty("webServerAddress").GetString()!.TrimEnd('/') + "/");
        route = "v1/chatMix";
    }
    public async Task<double> Read()
    {
        try {
            if (endpoint == null) await Discover();
            using var response = await http.GetAsync(new Uri(endpoint!, route));
            if (response.StatusCode == HttpStatusCode.NotFound && route == "v1/chatMix") {
                route = "chatMix";
                return await Read();
            }
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return ParseBalance(doc.RootElement);
        } catch { endpoint = null; throw; }
    }
    internal static double ParseBalance(JsonElement root) {
        double value = root.GetProperty("balance").GetDouble();
        if (!double.IsFinite(value) || value < -1 || value > 1) throw new InvalidDataException("Balance de Sonar inválido.");
        return value;
    }
    public async Task Write(double balance)
    {
        if (!double.IsFinite(balance) || balance < -1 || balance > 1) throw new ArgumentOutOfRangeException(nameof(balance));
        try {
            if (endpoint == null) await Read();
            using var r = await http.PutAsync(new Uri(endpoint!, route + "?balance=" + balance.ToString("0.####", CultureInfo.InvariantCulture)), null);
            r.EnsureSuccessStatusCode();
        } catch { endpoint = null; throw; } // No replay of old wheel commands after reconnection.
    }
    public void Dispose() => http.Dispose();
}
static class Mix {
    public static double Move(double value, int direction, double step, bool reverse) =>
        Math.Round(Math.Clamp(value + direction * step * (reverse ? -1 : 1), -1, 1), 4);
}
