@echo off
rem Builds a self-contained, single-file SeniorDesktop.exe (Windows x64) into the parent folder (..\).
rem Requires the .NET 8 SDK. The resulting exe does not need .NET to be installed.
cd /d "%~dp0"
dotnet publish SeniorDesktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o "%~dp0.."
pause
