@echo off

dotnet build "GeneratorLibrary\GeneratorLibrary.csproj" /p:Configuration=Release

copy /Y ".\GeneratorLibrary\bin\Release\netstandard2.0\GeneratorLibrary.dll" ".\"

pause