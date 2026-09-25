# TTS7 BALCON API

Local Windows bridge and HTTP API for **Loquendo TTS7**, **BALCON** (the Balabolka console utility), and **32-bit SAPI 5** voices.

[Read this documentation in Spanish](README.es.md)

The project provides one command-line executable. It can enumerate installed voices, synthesize WAV files, run diagnostics, or serve a local HTTP API for another application. It does not require a cloud service while generating audio.

## What it supports

- Direct Loquendo TTS7 access through the installed `LoqTTS7.dll` using the 32-bit Windows ABI.
- BALCON voice enumeration and synthesis for SAPI 4, SAPI 5, and Infovox installations supported by BALCON.
- 32-bit Microsoft SAPI 5 enumeration and synthesis.
- WAV output with configurable sample rate and channel count.
- TTS7 worker isolation so a native engine failure does not take down the parent request process.
- A loopback-only HTTP API bound to `127.0.0.1`.
- JSON Lines mode for applications that prefer stdin/stdout.
- Provider scan, ABI probe, QA suite, stress test, and controls test commands.

The project does not include voices, `LoqTTS7.dll`, `balcon.exe`, Balabolka voice data, or Microsoft SAPI voice packages. Those components remain on the user's Windows installation and are subject to their own terms.

## Requirements

- Windows 10 or later.
- .NET SDK 10 to build from source. The published executable is self-contained and targets `win-x86` because the legacy TTS interfaces are 32-bit.
- An installed TTS7 engine and persona for the `loquendo7-native` provider.
- The complete BALCON package for `balcon` and `balcon-sapi4` providers.

### Install BALCON

Download the official Balabolka Console Utility from:

- Spanish: <https://www.cross-plus-a.com/es/bconsole.htm>
- English: <https://www.cross-plus-a.com/en/bconsole.htm>

Extract the **complete** archive into `release\tools\balcon\`. Keep `balcon.exe` together with every DLL shipped in that archive. The API sets the BALCON directory as its working directory so companion libraries are found.

You may instead set `TTS7_BALCON_API_BALCON` to the absolute path of `balcon.exe`.

## Build and start

From PowerShell in the repository root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1
```

The self-contained output is written to `release\`. Start the HTTP server with:

```powershell
.\release\START_API.cmd
```

The default address is `http://127.0.0.1:8767/`. To choose another port:

```powershell
.\release\Tts7BalconApi.exe http --port 8768
```

The service only listens on loopback and is intended for software running on the same Windows machine.

## HTTP API

### Health and discovery

```text
GET /v1/health
GET /v1/providers
GET /v1/voices?provider=loquendo7-native
GET /v1/voices?provider=balcon
GET /v1/voices?provider=balcon-sapi4
GET /v1/voices?provider=sapi5-x86
```

`/v1/providers` returns detected engines and voice lists. If TTS7 is not found automatically, pass its DLL or data path:

```text
GET /v1/providers?enginePath=C:\\Path\\To\\LoqTTS7.dll
GET /v1/voices?provider=loquendo7-native&enginePath=C:\\Path\\To\\LoqTTS7.dll
```

### Synthesize WAV audio

`POST /v1/synthesize` accepts JSON and returns a binary WAV response.

| Property | Required | Description |
| --- | --- | --- |
| `provider` | yes | `loquendo7-native`, `balcon`, `balcon-sapi4`, or `sapi5-x86` |
| `voice` | yes | Voice name returned by `/v1/voices` |
| `text` | yes | Text to synthesize; maximum 64,000 characters |
| `rate` | no | TTS7: 0–100; BALCON SAPI 5: −10–10; SAPI 4: 0–100 |
| `pitch` | no | TTS7: 0–100; BALCON SAPI 5: −10–10; SAPI 4: 0–100 |
| `volume` | no | TTS7 and BALCON SAPI 5: 0–100 |
| `sampleRate` | no | 8000–48000 Hz; default `32000` |
| `channels` | no | `1` or `2`; default `1` |
| `enginePath` | no | TTS7 DLL or data path when auto-detection is insufficient |

Example with TTS7:

```powershell
$body = @{
    provider = 'loquendo7-native'
    voice = 'Jorge'
    text = 'Hello. This is a TTS7 test.'
} | ConvertTo-Json

Invoke-RestMethod 'http://127.0.0.1:8767/v1/synthesize' `
    -Method Post `
    -ContentType 'application/json; charset=utf-8' `
    -Body ([Text.Encoding]::UTF8.GetBytes($body)) `
    -OutFile '.\jorge.wav'
```

Example with a SAPI 4 / Infovox voice through BALCON:

```powershell
$body = @{
    provider = 'balcon-sapi4'
    voice = 'Antonio (Spanish) SAPI4 22kHz'
    text = 'This is a BALCON SAPI 4 test.'
} | ConvertTo-Json

Invoke-RestMethod 'http://127.0.0.1:8767/v1/synthesize' `
    -Method Post `
    -ContentType 'application/json; charset=utf-8' `
    -Body ([Text.Encoding]::UTF8.GetBytes($body)) `
    -OutFile '.\antonio.wav'
```

Successful responses have `Content-Type: audio/wav` and include the duration in `X-Audio-Duration-Seconds`. Errors are JSON objects with an `error` property and an HTTP 4xx or 5xx status.

## Command-line commands

Run commands from the `release` directory after building.

```text
Tts7BalconApi.exe scan [--json] [--engine <TTS7 DLL or data path>]
Tts7BalconApi.exe voices --provider <provider> [--engine <path>]
Tts7BalconApi.exe synth --provider <provider> --voice <voice> --text <text> --out <file.wav>
Tts7BalconApi.exe synth ... --text-file <file.txt>
Tts7BalconApi.exe serve
Tts7BalconApi.exe http [--port 8767]
Tts7BalconApi.exe qa-suite [--provider <provider>] [--voice <voice>] [--out-dir <folder>]
Tts7BalconApi.exe stress [--provider <provider>] [--voice <voice>] [--count 100] [--out-dir <folder>]
Tts7BalconApi.exe controls-test [--provider <provider>] [--voice <voice>] [--out-dir <folder>]
Tts7BalconApi.exe abi-probe [--voice <voice>] [--engine <path>]
Tts7BalconApi.exe balcon-probe [--voice <SAPI4 voice>]
```

`serve` reads one JSON request per line and writes one JSON response per line. Example:

```json
{"command":"synth","provider":"loquendo7-native","voice":"Jorge","text":"Hello","output":"C:\\Temp\\hello.wav"}
```

## Compatibility and tuning

- Requests are processed sequentially because legacy voice engines keep process-global state.
- TTS7 synthesis runs in an isolated x86 worker process.
- For SAPI 4, `50` is treated as the neutral rate and pitch value. Omitting these properties lets the legacy voice use its defaults.
- BALCON's exact voice names and accepted controls depend on the installed voice package. Always use the name returned by `/v1/voices`.
- Native engine diagnostics may report a completed WAV even if a legacy DLL exits abnormally during teardown; the file is retained after validation.

## License and third-party software

The source code in this repository is released under the MIT License; see [LICENSE](LICENSE).

This repository is an integration layer. It does not grant rights to third-party engines, voices, libraries, or data. BALCON is freeware provided by Cross+A; consult its author and the downloaded package for redistribution terms. Loquendo TTS7 components and personas are proprietary software and must be obtained and licensed separately. Windows SAPI is supplied under Microsoft's terms.

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) before redistributing a compiled bundle.

## Status

The code targets the Windows installation layout described above. Build and audio verification must be performed on a Windows machine with the relevant engines installed.
