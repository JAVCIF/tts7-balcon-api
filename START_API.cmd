@echo off
setlocal
set "EXE=%~dp0Tts7BalconApi.exe"
if not exist "%EXE%" (
  set "EXE=%~dp0release\Tts7BalconApi.exe"
)
if not exist "%EXE%" (
  echo Falta el ejecutable. Ejecuta: powershell -ExecutionPolicy Bypass -File build.ps1
  exit /b 1
)
"%EXE%" http --port 8767
