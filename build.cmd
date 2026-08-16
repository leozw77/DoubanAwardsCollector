@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: .NET SDK was not found.
  exit /b 1
)

dotnet restore ".\src\DoubanAwardsCollector\DoubanAwardsCollector.csproj"
if errorlevel 1 exit /b 1

dotnet build ".\src\DoubanAwardsCollector\DoubanAwardsCollector.csproj" -c Release --no-restore
if errorlevel 1 exit /b 1

echo.
echo Build succeeded.
endlocal
