@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

:: 项目信息
set PROJECT_NAME=SodiumPaint
set TARGET_FRAMEWORK=net8.0-windows
set RUNTIME=win-x64
set CONFIG=Release
set OUTPUT_DIR=bin\%CONFIG%\%TARGET_FRAMEWORK%\%RUNTIME%\publish

echo ================================
echo 📦  正在打包 %PROJECT_NAME%（单文件发布）
echo ================================

:: 清理旧发布目录
if exist "%OUTPUT_DIR%" (
    echo 🔄 清理旧发布目录：%OUTPUT_DIR%
    rmdir /s /q "%OUTPUT_DIR%"
)

:: 开始发布
echo 🚀 开始执行 dotnet publish...
dotnet publish -c %CONFIG% -r %RUNTIME% ^
    -p:PublishSingleFile=true ^
    --self-contained false


if %ERRORLEVEL% neq 0 (
    echo ❌ 发布失败，请检查错误。
    pause
    exit /b %ERRORLEVEL%
)

echo ✅ 发布成功！
echo -------------------------------
echo 📁 输出目录：%OUTPUT_DIR%
echo -------------------------------
dir "%OUTPUT_DIR%" /b
echo -------------------------------
echo 💡 双击运行：%OUTPUT_DIR%\%PROJECT_NAME%.exe
echo.
pause
endlocal
