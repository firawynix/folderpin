@echo off
setlocal

rem Compila o Firaw - TaskBar com o compilador C# que ja vem no Windows.
rem Nao precisa de Visual Studio, SDK nem nada baixado.

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Nao achei o csc.exe do .NET Framework 4.x.
    exit /b 1
)

set REFS=/r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll
set OPTS=/nologo /target:winexe /platform:anycpu

cd /d "%~dp0"

echo [1/4] icone...
"%CSC%" /nologo /target:exe /r:System.dll /r:System.Drawing.dll /out:"%TEMP%\firaw-taskbar-icon.exe" "tools\png-to-ico.cs"
if errorlevel 1 exit /b 1
"%TEMP%\firaw-taskbar-icon.exe" "assets\firaw-taskbar.png" "firaw-taskbar.ico"
if errorlevel 1 exit /b 1

echo [2/4] motor da janela...
"%CSC%" %OPTS% %REFS% /win32icon:"firaw-taskbar.ico" /resource:"firaw-taskbar.ico",FirawTaskBarIcon /out:"Firaw - TaskBar.exe" "FolderPin.cs"
if errorlevel 1 exit /b 1

echo [3/4] configurador...
"%CSC%" %OPTS% %REFS% /win32icon:"firaw-taskbar.ico" /resource:"firaw-taskbar.ico",FirawTaskBarIcon /resource:"Firaw - TaskBar.exe" /out:"Firaw - TaskBar Studio.exe" "FolderPinStudio.cs"
if errorlevel 1 exit /b 1

echo [4/4] instalador...
"%CSC%" %OPTS% %REFS% /win32icon:"firaw-taskbar.ico" /resource:"firaw-taskbar.ico",FirawTaskBarIcon /resource:"Firaw - TaskBar.exe" /resource:"Firaw - TaskBar Studio.exe" /out:"Firaw - TaskBar Setup.exe" "FolderPinSetup.cs"
if errorlevel 1 exit /b 1

echo.
echo Pronto. Rode "Firaw - TaskBar Setup.exe" para instalar.
endlocal
