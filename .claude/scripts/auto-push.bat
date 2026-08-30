@echo off
setlocal enabledelayedexpansion

REM auto-push.bat
REM Daily auto-push script (intended for midnight schedule)
REM - Push only on main branch
REM - Run only when local commits exist
REM - Append result to log file

set REPO_PATH=C:\Users\jayju\project\도깨비\도깨비
set LOG_FILE=%REPO_PATH%\.claude\logs\auto-push.log

REM Ensure log directory
if not exist "%REPO_PATH%\.claude\logs" mkdir "%REPO_PATH%\.claude\logs"

REM Timestamp
set TIMESTAMP=%date% %time%

echo. >> "%LOG_FILE%"
echo === Auto-push run at %TIMESTAMP% === >> "%LOG_FILE%"

cd /d "%REPO_PATH%"
if errorlevel 1 (
    echo ERROR: Cannot cd to repo path >> "%LOG_FILE%"
    exit /b 1
)

REM Current branch
for /f "tokens=*" %%i in ('git branch --show-current') do set CURRENT_BRANCH=%%i

if not "%CURRENT_BRANCH%"=="main" (
    echo SKIP: Not on main branch [current=%CURRENT_BRANCH%] >> "%LOG_FILE%"
    exit /b 0
)

REM Ahead count (local vs origin)
for /f "tokens=*" %%i in ('git rev-list --count origin/main..HEAD') do set AHEAD=%%i

if "%AHEAD%"=="0" (
    echo SKIP: No local commits to push >> "%LOG_FILE%"
    exit /b 0
)

echo INFO: %AHEAD% commits ahead, pushing... >> "%LOG_FILE%"

REM Push (no force, fast-forward only)
git push origin main >> "%LOG_FILE%" 2>&1

if %ERRORLEVEL%==0 (
    echo SUCCESS: Push completed >> "%LOG_FILE%"
) else (
    echo ERROR: Push failed with code %ERRORLEVEL% >> "%LOG_FILE%"
)

exit /b %ERRORLEVEL%
