@echo off
chcp 65001 >nul
setlocal

set "FRAMEWORK=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319"
set "CSC=%FRAMEWORK%\csc.exe"
set "TEST_OUT=%TEMP%\ProxyTakeoverLifecycleTests.exe"

"%CSC%" /nologo /t:exe /out:"%TEST_OUT%" /main:ProxyTakeoverLifecycleTests /warn:4 /codepage:65001 ^
    /reference:"%FRAMEWORK%\System.dll" ^
    /reference:"%FRAMEWORK%\System.Drawing.dll" ^
    /reference:"%FRAMEWORK%\System.Windows.Forms.dll" ^
    "HelloMessageBox.cs" "ProxyTakeoverLifecycle.cs" "ProxyTakeoverLifecycleTests.cs"

if errorlevel 1 exit /b 1
"%TEST_OUT%"
set "TEST_EXIT=%ERRORLEVEL%"
del "%TEST_OUT%" 2>nul
exit /b %TEST_EXIT%
