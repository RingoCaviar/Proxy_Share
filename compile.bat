@echo off
chcp 65001 >nul
setlocal

set "SRC=HelloMessageBox.cs"
set "OUT=ProxyShare.exe"
set "FRAMEWORK=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319"
set "CSC=%FRAMEWORK%\csc.exe"

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
    "ProxyTakeoverLifecycle.cs"

if errorlevel 1 (
    echo 编译失败。
    exit /b 1
)

del "%OUT:.exe=.pdb%" 2>nul
echo 编译成功: %OUT%
endlocal
