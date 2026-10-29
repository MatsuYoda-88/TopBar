@echo off
title TopBar - build and run
:: Builds TopBar.exe with the C# compiler that already comes with Windows, then starts it.
:: Nothing gets installed. Run this again any time to rebuild (after an update, for example).
set "HERE=%~dp0"
set "SRC=%HERE%src\TopBar.cs"
set "OUT=%HERE%TopBar.exe"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo   Can't find the C# compiler that comes with Windows:
  echo   %CSC%
  echo   TopBar needs 64-bit Windows 10 or 11.
  pause
  exit /b
)
echo   Stopping TopBar if it's already running...
taskkill /f /im "TopBar.exe" >nul 2>&1
timeout /t 1 >nul
echo   Building TopBar...
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:"%OUT%" "%SRC%" > "%HERE%build.log" 2>&1
if errorlevel 1 (
  echo.
  echo   BUILD FAILED. Here's why:
  type "%HERE%build.log"
  pause
  exit /b
)
echo   Built. Starting TopBar...
start "" "%OUT%"
echo.
echo   TopBar is running. Push the mouse to the very top of your main screen, or press the Windows key.
echo   Right-click the bar for options.
timeout /t 8
