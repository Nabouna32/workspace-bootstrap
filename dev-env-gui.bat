@echo off
setlocal
pushd "%~dp0"

set "APP="
if exist "desktop\bin\Release\net8.0-windows\BounaDevEnvironment.exe" set "APP=desktop\bin\Release\net8.0-windows\BounaDevEnvironment.exe"
if not defined APP if exist "desktop\bin\Debug\net8.0-windows\BounaDevEnvironment.exe" set "APP=desktop\bin\Debug\net8.0-windows\BounaDevEnvironment.exe"

if not defined APP (
    echo.
    echo Bouna Dev Environment - application desktop non compilee.
    echo.
    echo Compilez-la avec :
    echo   cd desktop
    echo   dotnet build -c Release
    echo.
    popd
    exit /b 20
)

start "" "%APP%"
popd
exit /b 0
