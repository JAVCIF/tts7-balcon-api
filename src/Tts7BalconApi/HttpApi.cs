using System.Net;
using System.Text.Json;

namespace Tts7BalconApi;

// HTTP is deliberately local and single-request-at-a-time: the legacy native
// engine shares process state, while each synthesis gets its own output file.
internal static class HttpApi
{
    private const int MaxBodyBytes = 256 * 1024;
    private const int MaxTextLength = 64000;

    internal static async Task RunAsync(int port)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "El puerto debe estar entre 1 y 65535.");

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        Console.WriteLine($"TTS API escuchando en http://127.0.0.1:{port}/ (Ctrl+C para cerrar)");

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); listener.Stop(); };
        while (!shutdown.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync(); }
            catch (HttpListenerException) when (shutdown.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) when (shutdown.IsCancellationRequested) { break; }

            try { await HandleAsync(context); }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                try { await JsonAsync(context.Response, 500, new { error = "Fallo interno de síntesis." }); }
                catch { /* The client may have disconnected. */ }
            }
            finally { try { context.Response.Close(); } catch { } }
        }
    }

    private static async Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";

        if (request.HttpMethod == "GET" && path == "/v1/health")
        {
            await JsonAsync(response, 200, new { ok = true, service = "tts7-balcon-api" });
            return;
        }
        if (request.HttpMethod == "GET" && path == "/v1/providers")
        {
            var enginePath = request.QueryString["enginePath"];
            await JsonAsync(response, 200, TtsDoctor.Scan(enginePath));
            return;
        }
        if (request.HttpMethod == "GET" && path == "/v1/voices")
        {
            var provider = request.QueryString["provider"] ?? "";
            var enginePath = request.QueryString["enginePath"];
            try
            {
                var voices = provider.ToLowerInvariant() switch
                {
                    "loquendo7-native" => GetNativeVoices(enginePath),
                    "sapi5-x86" => Sapi5Provider.GetVoices(),
                    "balcon" => BalconProvider.GetVoices(FindBalcon()),
                    "balcon-sapi4" => BalconProvider.GetVoices(FindBalcon(), true),
                    _ => throw new ArgumentException("Proveedor inválido. Usa loquendo7-native, sapi5-x86, balcon o balcon-sapi4.")
                };
                await JsonAsync(response, 200, new { provider, voices });
            }
            catch (ArgumentException ex) { await JsonAsync(response, 400, new { error = ex.Message }); }
            catch (Exception ex) { await JsonAsync(response, 503, new { error = ex.Message }); }
            return;
        }
        if (request.HttpMethod == "POST" && path == "/v1/synthesize")
        {
            await SynthesizeAsync(request, response);
            return;
        }

        await JsonAsync(response, 404, new { error = "Ruta no encontrada." });
    }

    private static async Task SynthesizeAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (request.ContentType?.Split(';')[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase) != true)
        {
            await JsonAsync(response, 415, new { error = "Se requiere Content-Type: application/json." });
            return;
        }
        if (request.ContentLength64 > MaxBodyBytes)
        {
            await JsonAsync(response, 413, new { error = "JSON demasiado grande (máximo 256 KiB)." });
            return;
        }

        ApiSynthRequest? body;
        try
        {
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await request.InputStream.ReadAsync(buffer)) > 0)
            {
                if (bytes.Length + count > MaxBodyBytes)
                    throw new InvalidDataException("JSON demasiado grande (máximo 256 KiB).");
                bytes.Write(buffer, 0, count);
            }
            body = JsonSerializer.Deserialize<ApiSynthRequest>(bytes.ToArray(), new JsonSerializerOptions(JsonDefaults.Options)
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (InvalidDataException ex) { await JsonAsync(response, 413, new { error = ex.Message }); return; }
        catch (JsonException ex) { await JsonAsync(response, 400, new { error = "JSON inválido: " + ex.Message }); return; }

        if (body is null || string.IsNullOrWhiteSpace(body.Voice) || string.IsNullOrWhiteSpace(body.Text)
            || body.Voice.Length > 256 || body.Text.Length > MaxTextLength
            || body.Provider is not ("loquendo7-native" or "sapi5-x86" or "balcon" or "balcon-sapi4")
            || body.SampleRate is < 8000 or > 48000 || body.Channels is < 1 or > 2)
        {
            await JsonAsync(response, 400, new { error = "Revisa provider, voice, text (máximo 64000 caracteres), sampleRate y channels." });
            return;
        }

        var output = Path.Combine(Path.GetTempPath(), $"tts-api-{Guid.NewGuid():N}.wav");
        var responseStarted = false;
        try
        {
            var synth = new SynthRequest(body.Provider, body.Voice, body.Text, output, body.Rate,
                body.Volume, body.SampleRate, body.Channels, body.EnginePath, body.Pitch);
            switch (body.Provider)
            {
                case "loquendo7-native": Tts7NativeIsolation.Synthesize(synth); break;
                case "sapi5-x86": Sapi5Provider.Synthesize(synth); break;
                default: BalconProvider.Synthesize(FindBalcon(), synth); break;
            }

            var wav = WavInspector.Inspect(output);
            response.StatusCode = 200;
            response.ContentType = "audio/wav";
            response.ContentLength64 = wav.FileBytes;
            response.Headers["X-Audio-Duration-Seconds"] = wav.DurationSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            responseStarted = true;
            await using var file = File.OpenRead(output);
            await file.CopyToAsync(response.OutputStream);
        }
        catch (Exception ex)
        {
            if (!responseStarted)
                await JsonAsync(response, 503, new { error = ex.Message });
            else
                Console.Error.WriteLine(ex);
        }
        finally { try { File.Delete(output); } catch { } }
    }

    private static IReadOnlyList<string> GetNativeVoices(string? enginePath)
    {
        using var native = new Tts7NativeProvider(enginePath);
        return native.GetVoices();
    }

    private static string FindBalcon() => BalconProvider.Find()
        ?? throw new FileNotFoundException("No se encontró balcon.exe. Extrae el paquete completo en tools\\balcon\\.");

    private static async Task JsonAsync(HttpListenerResponse response, int status, object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonDefaults.Options);
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    private sealed record ApiSynthRequest(
        string Provider = "loquendo7-native", string Voice = "", string Text = "",
        int? Rate = null, int? Pitch = null, int? Volume = null,
        int SampleRate = 32000, int Channels = 1, string? EnginePath = null);
}
