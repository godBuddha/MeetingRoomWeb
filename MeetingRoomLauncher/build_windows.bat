@echo off
:: ── Build MeetingRoom Launcher trên Windows ──
:: Yêu cầu: CMake, Visual Studio 2022 (hoặc MinGW), Git

setlocal
set SCRIPT_DIR=%~dp0

echo =^> Configuring...
cmake -B "%SCRIPT_DIR%build" -S "%SCRIPT_DIR%" -G "Visual Studio 17 2022" -A x64
if %ERRORLEVEL% neq 0 (
    echo [!] Visual Studio not found, trying MinGW...
    cmake -B "%SCRIPT_DIR%build" -S "%SCRIPT_DIR%" -G "MinGW Makefiles" -DCMAKE_BUILD_TYPE=Release
)

echo =^> Building...
cmake --build "%SCRIPT_DIR%build" --config Release

echo.
echo   Build xong! Chay:
echo   %SCRIPT_DIR%build\Release\MeetingRoomLauncher.exe
pause
