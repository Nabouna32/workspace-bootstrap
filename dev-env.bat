@echo off
setlocal

rem Public Windows entrypoint: use legacy PowerShell 5.1 only to bootstrap PowerShell 7.
rem ExecutionPolicy Bypass is process-scoped and does not change the machine policy.
pushd "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0bootstrap\windows\dev-env.ps1" %*
set "EXITCODE=%ERRORLEVEL%"
popd

exit /b %EXITCODE%
