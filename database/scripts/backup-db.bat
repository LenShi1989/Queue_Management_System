@echo off
REM ---------------------------------------------------------------------------
REM 每日備份（Windows 排程）
REM
REM   schtasks /Create /SC DAILY /ST 02:00 /TN "QueueDBBackup" ^
REM     /TR "C:\queue\database\scripts\backup-db.bat"
REM ---------------------------------------------------------------------------
setlocal

if "%POSTGRES_DB%"=="" set POSTGRES_DB=queue
if "%POSTGRES_USER%"=="" set POSTGRES_USER=postgres
if "%PGHOST%"=="" set PGHOST=localhost
if "%PGPORT%"=="" set PGPORT=5432
if "%KEEP_DAYS%"=="" set KEEP_DAYS=14

REM PGPASSWORD 請改用 pgpass 或由排程環境提供，勿寫入此檔
if "%PGPASSWORD%"=="" (
    echo [ERROR] 請先設定 PGPASSWORD 環境變數
    exit /b 1
)

set "BACKUP_DIR=%~dp0..\backups"
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

for /f "tokens=2 delims==" %%I in ('wmic os get LocalDateTime /value') do set "NOW=%%I"
set "STAMP=%NOW:~0,4%%NOW:~4,2%%NOW:~6,2_%NOW:~8,2%%NOW:~10,2%%NOW:~12,2%"
set "TARGET=%BACKUP_DIR%\%POSTGRES_DB%_%STAMP%.dump"

echo [%DATE% %TIME%] 開始備份 %POSTGRES_DB% -^> %TARGET%
pg_dump -h %PGHOST% -p %PGPORT% -U %POSTGRES_USER% -d %POSTGRES_DB% ^
    --format=custom --no-owner --no-privileges -f "%TARGET%"

if errorlevel 1 (
    echo [ERROR] 備份失敗
    exit /b 1
)

echo [%DATE% %TIME%] 備份完成：%TARGET%

REM 清理過期備份
forfiles /p "%BACKUP_DIR%" /m "%POSTGRES_DB%_*.dump" /d -%KEEP_DAYS% /c "cmd /c del /q @path" 2>nul

echo [%DATE% %TIME%] 保留最近 %KEEP_DAYS% 天
endlocal
