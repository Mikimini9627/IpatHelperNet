@echo off
setlocal enabledelayedexpansion

rem ============================================================
rem  NuGet publish script for IpatHelperNet
rem ============================================================

rem ---- .csproj を検索 ----------------------------------------
set CSPROJ_FILE=
for %%f in (*.csproj) do (
    set CSPROJ_FILE=%%f
)

if "%CSPROJ_FILE%"=="" (
    echo [ERROR] .csproj が見つかりませんでした。
    pause
    exit /b 1
)
echo [INFO] プロジェクトファイル: %CSPROJ_FILE%

rem ---- 現在のバージョン番号を取得 ----------------------------
set CURRENT_VERSION=
for /f "tokens=*" %%a in ('powershell -NoProfile -Command ^
    "[xml]$x = Get-Content '%CSPROJ_FILE%'; $x.Project.PropertyGroup.Version"') do (
    set CURRENT_VERSION=%%a
)

if "%CURRENT_VERSION%"=="" (
    echo [ERROR] .csproj からバージョン番号を取得できませんでした。
    pause
    exit /b 1
)
echo [INFO] 現在のバージョン: %CURRENT_VERSION%

rem ---- バージョン番号をインクリメント（パッチ番号 +1）--------
for /f "tokens=1,2,3 delims=." %%a in ("%CURRENT_VERSION%") do (
    set VER_MAJOR=%%a
    set VER_MINOR=%%b
    set VER_PATCH=%%c
)

set /a VER_PATCH_NEW=%VER_PATCH% + 1
set NEW_VERSION=%VER_MAJOR%.%VER_MINOR%.%VER_PATCH_NEW%
echo [INFO] 新しいバージョン: %NEW_VERSION%

rem ---- .csproj のバージョンを書き換え ------------------------
powershell -NoProfile -Command ^
    "(Get-Content '%CSPROJ_FILE%') -replace '<Version>%CURRENT_VERSION%</Version>', '<Version>%NEW_VERSION%</Version>' | Set-Content '%CSPROJ_FILE%'"

echo [INFO] %CSPROJ_FILE% のバージョンを %NEW_VERSION% に更新しました。

rem ---- publish_key.ini から APIキーを読み取り ----------------
set INI_FILE=%USERPROFILE%\.nuget\packages\publish_key.ini
if not exist "%INI_FILE%" (
    echo [ERROR] APIキーファイルが見つかりませんでした: %INI_FILE%
    pause
    exit /b 1
)

set API_KEY=
for /f "tokens=1,2 delims==" %%a in ('findstr /i "IpatHelperNet" "%INI_FILE%"') do (
    set API_KEY=%%b
)

rem 前後の空白を除去
for /f "tokens=* delims= " %%a in ("!API_KEY!") do set API_KEY=%%a

if "!API_KEY!"=="" (
    echo [ERROR] APIキーを取得できませんでした。publish_key.ini を確認してください。
    pause
    exit /b 1
)
echo [INFO] APIキーを読み込みました。

rem ---- dotnet pack -------------------------------------------
echo.
echo [INFO] パッケージをビルドしています...
dotnet pack -c Release -o ./nupkg
if %ERRORLEVEL% neq 0 (
    echo [ERROR] dotnet pack に失敗しました。
    pause
    exit /b 1
)

rem ---- dotnet nuget push -------------------------------------
set NUPKG_FILE=./nupkg/IpatHelperNet.%NEW_VERSION%.nupkg
echo.
echo [INFO] NuGet へプッシュしています: %NUPKG_FILE%
dotnet nuget push "%NUPKG_FILE%" --api-key "!API_KEY!" --source https://api.nuget.org/v3/index.json
if %ERRORLEVEL% neq 0 (
    echo [ERROR] dotnet nuget push に失敗しました。
    pause
    exit /b 1
)

echo.
echo [SUCCESS] IpatHelperNet %NEW_VERSION% を NuGet へ公開しました。
pause
exit /b 0