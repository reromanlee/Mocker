@echo off

rem Paths are relative to this script, so it works from any working directory.
set "ROOT=%~dp0"

dotnet build "%ROOT%GeneratorLibrary.csproj" /p:Configuration=Release || goto :end

copy /Y "%ROOT%bin\Release\netstandard2.0\GeneratorLibrary.dll" "%ROOT%..\UnityPackage\Runtime\Plugins\"

:end
pause
