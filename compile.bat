@echo off
chcp 65001 >nul
setlocal

set "SRC=HelloMessageBox.cs"
set "OUT=ProxyShare.exe"
set "FRAMEWORK=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319"
set "CSC=%FRAMEWORK%\csc.exe"
set /p "APP_VERSION="<"VERSION"
set "VERSION_SOURCE=%TEMP%\ProxyShareVersion_%RANDOM%_%RANDOM%.cs"

if not defined APP_VERSION (
    echo 无法从 VERSION 读取版本号。
    exit /b 1
)

>"%VERSION_SOURCE%" echo using System.Reflection;
>>"%VERSION_SOURCE%" echo [assembly: AssemblyVersion("%APP_VERSION%.0")]
>>"%VERSION_SOURCE%" echo [assembly: AssemblyFileVersion("%APP_VERSION%.0")]
>>"%VERSION_SOURCE%" echo [assembly: AssemblyInformationalVersion("%APP_VERSION%")]

"%CSC%" ^
    /nologo ^
    /t:winexe ^
    /out:"%OUT%" ^
    /platform:anycpu ^
    /optimize+ ^
    /debug:pdbonly ^
    /warn:4 ^
    /codepage:65001 ^
    /win32icon:"logo.ico" ^
    /reference:"%FRAMEWORK%\System.dll" ^
    /reference:"%FRAMEWORK%\System.Drawing.dll" ^
    /reference:"%FRAMEWORK%\System.Windows.Forms.dll" ^
    "%SRC%" ^
    "ProxyTakeoverLifecycle.cs" ^
    "%VERSION_SOURCE%"

if errorlevel 1 (
    del "%VERSION_SOURCE%" 2>nul
    echo 编译失败。
    exit /b 1
)

del "%VERSION_SOURCE%" 2>nul
del "%OUT:.exe=.pdb%" 2>nul
echo 编译成功: %OUT% (v%APP_VERSION%)
endlocal
