@echo off

set DIR=%~dp0
dotnet build /v:Minimal /p:BuildVersion=%1 "%DIR%\Portable.Xaml.sln"

dotnet pack /v:Minimal /p:BuildVersion=%1 "%DIR%\Portable.Xaml.sln"
