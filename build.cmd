@echo off

if not defined VisualStudioVersion (
    if defined VS150COMNTOOLS (
        call "%VS150COMNTOOLS%\VsDevCmd.bat"
        goto :EnvSet
    )

    echo Error: build.cmd requires Visual Studio 2017.
    exit /b 1
)

:EnvSet

set BUILD_DIR=%~dp0build
msbuild -t:Package "%BUILD_DIR%\Build.proj"

pause