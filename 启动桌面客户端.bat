@echo off
rem Local Knowledge Base - Desktop client launcher (double-click)
rem Uses the compiled exe when available; falls back to dotnet run.

set "ROOT=%~dp0"
set "EXE=%ROOT%frontend\desktop\LocalKnowledgeBase.Desktop\bin\Debug\net10.0-windows\LocalKnowledgeBase.Desktop.exe"

if exist "%EXE%" (
    start "" "%EXE%"
    exit /b 0
)

echo [..] Compiled client not found, building and starting (slow first time)...
start "LocalKnowledgeBase Desktop" /min dotnet run --project "%ROOT%frontend\desktop\LocalKnowledgeBase.Desktop"
