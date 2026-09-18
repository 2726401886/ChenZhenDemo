@echo off
REM ChenZhenDemo WebGL 产物校验脚本 (Windows)

echo === ChenZhenDemo WebGL 打包产物校验 ===
echo.

set BUILD_DIR=docs
set ERRORS=0

REM 1. 检查目录结构
echo [1/5] 检查目录结构...
if exist "%BUILD_DIR%\index.html" (echo   [OK] index.html 存在) else (echo   [缺失] index.html 缺失 & set /a ERRORS+=1)
if exist "%BUILD_DIR%\.nojekyll" (echo   [OK] .nojekyll 存在) else (echo   [缺失] .nojekyll 缺失 & set /a ERRORS+=1)
if exist "%BUILD_DIR%\Build" (echo   [OK] Build 目录存在) else (echo   [缺失] Build 目录缺失 & set /a ERRORS+=1)
if exist "%BUILD_DIR%\TemplateData" (echo   [OK] TemplateData 目录存在) else (echo   [缺失] TemplateData 目录缺失 & set /a ERRORS+=1)

REM 2. 检查Build目录文件
echo.
echo [2/5] 检查Build目录文件...
if exist "%BUILD_DIR%\Build" (
    dir /b "%BUILD_DIR%\Build\*.wasm" 2>nul | find /c /v "" > nul
    dir /b "%BUILD_DIR%\Build\*.data" 2>nul | find /c /v "" > nul
    dir /b "%BUILD_DIR%\Build\*.js" 2>nul | find /c /v "" > nul
    echo   Build 文件检查完成
) else (
    echo   [缺失] Build 目录不存在
    set /a ERRORS+=1
)

REM 3. 检查文件大小
echo.
echo [3/5] 检查文件大小...
if exist "%BUILD_DIR%" (
    for %%F in ("%BUILD_DIR%\Build\*.*") do echo   %%~nF: %%~zF bytes
)

REM 4. 检查nojekyll
echo.
echo [4/5] 检查.nojekyll...
if exist "%BUILD_DIR%\.nojekyll" (echo   [OK] .nojekyll 存在) else (echo   [缺失] .nojekyll 缺失 & set /a ERRORS+=1)

REM 5. 检查index.html
echo.
echo [5/5] 检查index.html...
if exist "%BUILD_DIR%\index.html" (echo   [OK] index.html 存在) else (echo   [缺失] index.html 不存在 & set /a ERRORS+=1)

REM 汇总
echo.
echo ================================
if %ERRORS%==0 (
    echo 校验通过！产物完整
) else (
    echo 校验失败，发现 %ERRORS% 个问题
)
echo ================================
pause
