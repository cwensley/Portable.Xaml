@echo off

if not defined VisualStudioVersion (
    if defined VS150COMNTOOLS (
        call "%VS150COMNTOOLS%\VsDevCmd.bat"
        goto :EnvSet
    )

	WHERE msbuild > nul
	IF %ERRORLEVEL% NEQ 0 (
		ECHO msbuild not found.  Run in the Developer Command Prompt for VS 2017
		exit /b 1
	)
)

:EnvSet

set BUILD_DIR=%~dp0build
msbuild -t:Package -p:BuildVersion=%1 "%BUILD_DIR%\Build.proj"

pause