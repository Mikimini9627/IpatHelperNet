@echo off
setlocal enabledelayedexpansion

rem  このバッチは Shift_JIS (CP932) で保存されている。
rem  コンソールのコードページが CP932 以外だと日本語部分の解釈が崩れて
rem  構文エラーになるため、最初にコードページを CP932 へ切り替え、
rem  終了時に元の値へ戻す。
for /f "tokens=2 delims=:" %%c in ('chcp') do set "ORIGINAL_CP=%%c"
set "ORIGINAL_CP=%ORIGINAL_CP: =%"
chcp 932 >nul

rem ============================================================
rem  IpatHelperNet NuGet 公開スクリプト
rem
rem  リポジトリ直下に置いて使う。どのフォルダから起動しても、
rem  バッチ自身の位置 (%~dp0) を基準に動作する。
rem
rem  使い方:
rem    publish.bat           ... パッケージを作成して NuGet へ公開する
rem    publish.bat /dryrun   ... 作成のみ (バージョン更新と公開はしない)
rem
rem  補足:
rem   パスに日本語が含まれると powershell へ渡す際に文字化けしやすいため、
rem   最初にプロジェクトフォルダへ pushd し、以降は相対パスだけで扱う。
rem ============================================================

set "REPO_ROOT=%~dp0"
set "PROJECT_DIR=%REPO_ROOT%IpatHelperNet"

set DRYRUN=0
if /i "%~1"=="/dryrun" set DRYRUN=1
if "%DRYRUN%"=="1" echo [INFO] dryrun モードです。バージョン更新と NuGet 公開は行いません。

if not exist "%PROJECT_DIR%" goto NO_PROJECT_DIR
pushd "%PROJECT_DIR%"

rem ---- .csproj を探す ----------------------------------------
set CSPROJ_FILE=
for %%f in (*.csproj) do (
    set CSPROJ_FILE=%%f
)

if "%CSPROJ_FILE%"=="" goto NO_CSPROJ
echo [INFO] プロジェクトファイル: %CSPROJ_FILE%

rem ---- 現在のバージョン番号を取得 ----------------------------
set CURRENT_VERSION=
for /f "usebackq tokens=*" %%a in (`powershell -NoProfile -Command ^
    "[xml]$x = Get-Content '%CSPROJ_FILE%'; $v = @($x.Project.PropertyGroup.Version); $v = $v -ne ''; $v[0]"`) do (
    set CURRENT_VERSION=%%a
)

if "%CURRENT_VERSION%"=="" goto NO_VERSION
echo [INFO] 現在のバージョン: %CURRENT_VERSION%

rem ---- バージョン番号をインクリメント (パッチ番号 +1) --------
for /f "tokens=1,2,3 delims=." %%a in ("%CURRENT_VERSION%") do (
    set VER_MAJOR=%%a
    set VER_MINOR=%%b
    set VER_PATCH=%%c
)

set /a VER_PATCH_NEW=%VER_PATCH% + 1
set NEW_VERSION=%VER_MAJOR%.%VER_MINOR%.%VER_PATCH_NEW%
echo [INFO] 新しいバージョン: %NEW_VERSION%

rem ---- .csproj のバージョンを書き換える ----------------------
rem  Get-Content / Set-Content を素で使うと既定の文字コードで書き戻され、
rem  日本語コメントが少しずつ壊れていく。UTF-8 (BOM なし) で明示的に読み書きする。
if "%DRYRUN%"=="1" goto SKIP_VERSION_WRITE

powershell -NoProfile -Command ^
    "$t = [IO.File]::ReadAllText('%CSPROJ_FILE%', [Text.Encoding]::UTF8) -replace '<Version>[^<]*</Version>', '<Version>%NEW_VERSION%</Version>'; [IO.File]::WriteAllText('%CSPROJ_FILE%', $t, (New-Object Text.UTF8Encoding $false))"
if errorlevel 1 goto VERSION_WRITE_FAILED

echo [INFO] %CSPROJ_FILE% のバージョンを %NEW_VERSION% に更新しました。
goto AFTER_VERSION_WRITE

:SKIP_VERSION_WRITE
set NEW_VERSION=%CURRENT_VERSION%
echo [INFO] dryrun のためバージョンは据え置きます (%NEW_VERSION%)。

:AFTER_VERSION_WRITE

rem ---- publish_key.ini から API キーを読み取る ----------------
rem  公開しない dryrun ではキーが無くても続行する。
set INI_FILE=%USERPROFILE%\.nuget\packages\publish_key.ini
set API_KEY=

if "%DRYRUN%"=="1" goto SKIP_API_KEY
if not exist "%INI_FILE%" goto NO_INI

for /f "tokens=1,2 delims==" %%a in ('findstr /i "IpatHelperNet" "%INI_FILE%"') do (
    set API_KEY=%%b
)

rem 前後の空白を取り除く
for /f "tokens=* delims= " %%a in ("!API_KEY!") do set API_KEY=%%a

if "!API_KEY!"=="" goto NO_API_KEY
echo [INFO] API キーを読み込みました。

:SKIP_API_KEY

rem ---- 上の階層から README.md を一時コピー --------------------
rem  csproj の PackageReadmeFile がプロジェクト直下の README.md を参照するため、
rem  リポジトリ直下の README.md を作成時だけコピーして使う。
set README_SRC=..\README.md
set README_DST=.\README.md
set README_COPIED=0

if not exist "%README_SRC%" goto NO_README

copy /y "%README_SRC%" "%README_DST%" >nul
if errorlevel 1 goto README_COPY_FAILED
set README_COPIED=1
echo [INFO] README.md を一時コピーしました。

rem ---- dotnet pack -------------------------------------------
echo.
echo [INFO] パッケージを作成しています...
dotnet pack -c Release -o ./nupkg
set PACK_RESULT=%ERRORLEVEL%

rem ---- README.md の一時コピーを削除 --------------------------
if "%README_COPIED%"=="1" del /f /q "%README_DST%" >nul
if "%README_COPIED%"=="1" echo [INFO] README.md の一時コピーを削除しました。

if %PACK_RESULT% neq 0 goto PACK_FAILED

set NUPKG_FILE=./nupkg/IpatHelperNet.%NEW_VERSION%.nupkg
if not exist "%NUPKG_FILE%" goto NO_NUPKG

if "%DRYRUN%"=="1" goto DRYRUN_DONE

rem ---- dotnet nuget push -------------------------------------
echo.
echo [INFO] NuGet へ公開しています: %NUPKG_FILE%
dotnet nuget push "%NUPKG_FILE%" --api-key "!API_KEY!" --source https://api.nuget.org/v3/index.json
if errorlevel 1 goto PUSH_FAILED

echo.
echo [SUCCESS] IpatHelperNet %NEW_VERSION% を NuGet へ公開しました。
set EXIT_CODE=0
goto END

:DRYRUN_DONE
echo.
echo [SUCCESS] dryrun 完了: %NUPKG_FILE% を作成しました (公開はしていません)。
set EXIT_CODE=0
goto END

:NO_PROJECT_DIR
echo [ERROR] プロジェクトフォルダが見つかりません: %PROJECT_DIR%
set EXIT_CODE=1
goto END_NO_POPD

:NO_CSPROJ
echo [ERROR] .csproj が見つかりませんでした。
set EXIT_CODE=1
goto END

:NO_VERSION
echo [ERROR] .csproj からバージョン番号を取得できませんでした。
set EXIT_CODE=1
goto END

:VERSION_WRITE_FAILED
echo [ERROR] .csproj のバージョン更新に失敗しました。
set EXIT_CODE=1
goto END

:NO_INI
echo [ERROR] API キーのファイルが見つかりません: %INI_FILE%
set EXIT_CODE=1
goto END

:NO_API_KEY
echo [ERROR] API キーを取得できませんでした。publish_key.ini を確認してください。
set EXIT_CODE=1
goto END

:NO_README
echo [ERROR] README.md が見つかりません: %README_SRC%
set EXIT_CODE=1
goto END

:README_COPY_FAILED
echo [ERROR] README.md のコピーに失敗しました。
set EXIT_CODE=1
goto END

:PACK_FAILED
echo [ERROR] dotnet pack に失敗しました。
set EXIT_CODE=1
goto END

:NO_NUPKG
echo [ERROR] パッケージが作成されていません: %NUPKG_FILE%
set EXIT_CODE=1
goto END

:PUSH_FAILED
echo [ERROR] dotnet nuget push に失敗しました。
set EXIT_CODE=1
goto END

:END
popd

:END_NO_POPD
if not defined EXIT_CODE set EXIT_CODE=1
echo.
pause
if defined ORIGINAL_CP chcp %ORIGINAL_CP% >nul
endlocal & exit /b %EXIT_CODE%
