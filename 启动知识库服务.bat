@echo off
rem Local Knowledge Base - API launcher (double-click)
rem Starts the local API minimized. AI tools (MCP) and desktop client need it.
rem Stop: close the minimized "LocalKnowledgeBase API" window.

netstat -ano | findstr :7047 | findstr LISTENING >nul 2>&1
if not errorlevel 1 (
    echo [OK] Knowledge Base API is already running. This window closes in 3s.
    ping -n 4 127.0.0.1 >nul
    exit /b 0
)

echo [..] Starting Knowledge Base API (takes ~10s)...
echo      A minimized "LocalKnowledgeBase API" window will appear - keep it open.
start "LocalKnowledgeBase API" /min dotnet run --project "%~dp0backend\LocalKnowledgeBase.Api" --launch-profile https

ping -n 4 127.0.0.1 >nul
