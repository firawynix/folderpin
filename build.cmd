@echo off
setlocal

rem Compila o FolderPin com o compilador C# que ja vem no Windows.
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

echo [1/3] motor da janela...
"%CSC%" %OPTS% %REFS% /out:"FolderPin.exe" "FolderPin.cs"
if errorlevel 1 exit /b 1

echo [2/3] configurador...
"%CSC%" %OPTS% %REFS% /win32icon:"folderpin.ico" /resource:"FolderPin.exe,FolderPin.exe" /out:"FolderPin Studio.exe" "FolderPinStudio.cs"
if errorlevel 1 exit /b 1

echo [3/3] instalador...
"%CSC%" %OPTS% %REFS% /win32icon:"folderpin.ico" /resource:"FolderPin.exe,FolderPin.exe" /resource:"FolderPin Studio.exe,FolderPin Studio.exe" /out:"FolderPin Setup.exe" "FolderPinSetup.cs"
if errorlevel 1 exit /b 1

echo.
echo Pronto. Rode "FolderPin Setup.exe" para instalar.
endlocal
