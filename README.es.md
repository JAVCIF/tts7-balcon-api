# TTS7 BALCON API

Puente local y API HTTP para **Loquendo TTS7**, **BALCON** (la utilidad de consola de Balabolka) y voces **SAPI 5 de 32 bits**.

[Read the English documentation](README.md)

El proyecto ofrece un ejecutable de línea de comandos. Puede listar voces, generar WAV, ejecutar diagnósticos o publicar una API HTTP local para otra aplicación. La generación de audio no depende de servicios en la nube.

## Qué incluye

- Acceso directo a TTS7 mediante el `LoqTTS7.dll` instalado.
- Enumeración y síntesis mediante BALCON para voces SAPI 4, SAPI 5 e Infovox compatibles.
- Enumeración y síntesis de voces Microsoft SAPI 5 de 32 bits.
- WAV con frecuencia de muestreo y número de canales configurables.
- Worker x86 aislado para la síntesis nativa TTS7.
- API HTTP limitada a `127.0.0.1`.
- Modo JSON Lines por entrada y salida estándar.
- Diagnóstico de proveedores, prueba ABI, QA, estrés y controles.

No se incluyen voces, `LoqTTS7.dll`, `balcon.exe`, datos de voces de Balabolka ni paquetes de voces SAPI. Esos componentes deben estar instalados en Windows y tienen sus propias condiciones de uso.

## Requisitos

- Windows 10 o posterior.
- .NET SDK 10 para compilar. El resultado publicado es autocontenido y apunta a `win-x86` por la compatibilidad con motores TTS antiguos de 32 bits.
- Motor TTS7 y personas instaladas para `loquendo7-native`.
- Paquete completo de BALCON para `balcon` y `balcon-sapi4`.

### Instalar BALCON

Descarga la utilidad oficial de consola de Balabolka desde <https://www.cross-plus-a.com/es/bconsole.htm>.

Extrae el archivo completo en `release\tools\balcon\`. Conserva `balcon.exe` junto con todas las DLL incluidas en el paquete. Como alternativa, define `TTS7_BALCON_API_BALCON` con la ruta absoluta a `balcon.exe`.

## Compilar y arrancar

Desde PowerShell en la raíz del repositorio:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1
.\release\START_API.cmd
```

La API queda en `http://127.0.0.1:8767/`. Para cambiar el puerto:

```powershell
.\release\Tts7BalconApi.exe http --port 8768
```

## API HTTP

Rutas disponibles:

```text
GET /v1/health
GET /v1/providers
GET /v1/voices?provider=loquendo7-native
GET /v1/voices?provider=balcon
GET /v1/voices?provider=balcon-sapi4
GET /v1/voices?provider=sapi5-x86
POST /v1/synthesize
```

`POST /v1/synthesize` recibe JSON con `provider`, `voice` y `text`. También acepta `rate`, `pitch`, `volume`, `sampleRate`, `channels` y `enginePath`. Devuelve el WAV como respuesta binaria. El texto admite hasta 64 000 caracteres.

Ejemplo TTS7:

```powershell
$body = @{ provider = 'loquendo7-native'; voice = 'Jorge'; text = 'Hola. Esta es una prueba.' } | ConvertTo-Json
Invoke-RestMethod 'http://127.0.0.1:8767/v1/synthesize' -Method Post `
    -ContentType 'application/json; charset=utf-8' `
    -Body ([Text.Encoding]::UTF8.GetBytes($body)) -OutFile '.\prueba.wav'
```

Ejemplo SAPI4/Infovox mediante BALCON:

```powershell
$body = @{ provider = 'balcon-sapi4'; voice = 'Antonio (Spanish) SAPI4 22kHz'; text = 'Prueba de BALCON.' } | ConvertTo-Json
Invoke-RestMethod 'http://127.0.0.1:8767/v1/synthesize' -Method Post `
    -ContentType 'application/json; charset=utf-8' `
    -Body ([Text.Encoding]::UTF8.GetBytes($body)) -OutFile '.\antonio.wav'
```

## Comandos

```text
Tts7BalconApi.exe scan [--json] [--engine <DLL o ruta de datos TTS7>]
Tts7BalconApi.exe voices --provider <proveedor> [--engine <ruta>]
Tts7BalconApi.exe synth --provider <proveedor> --voice <voz> --text <texto> --out <archivo.wav>
Tts7BalconApi.exe synth ... --text-file <archivo.txt>
Tts7BalconApi.exe serve
Tts7BalconApi.exe http [--port 8767]
Tts7BalconApi.exe qa-suite [--provider <proveedor>] [--voice <voz>] [--out-dir <carpeta>]
Tts7BalconApi.exe stress [--provider <proveedor>] [--voice <voz>] [--count 100] [--out-dir <carpeta>]
Tts7BalconApi.exe controls-test [--provider <proveedor>] [--voice <voz>] [--out-dir <carpeta>]
Tts7BalconApi.exe abi-probe [--voice <voz>] [--engine <ruta>]
Tts7BalconApi.exe balcon-probe [--voice <voz SAPI4>]
```

`serve` usa una solicitud JSON por línea y devuelve una respuesta JSON por línea.

## Licencia y software de terceros

El código fuente se distribuye bajo la licencia MIT; consulta [LICENSE](LICENSE). La licencia del código no concede derechos sobre motores, voces, bibliotecas ni datos de terceros. BALCON es freeware de Cross+A; revisa las condiciones del paquete descargado. Los componentes y personas de Loquendo TTS7 son software propietario y deben obtenerse por separado. SAPI de Windows se rige por las condiciones de Microsoft.

Consulta [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) antes de redistribuir un paquete compilado.
