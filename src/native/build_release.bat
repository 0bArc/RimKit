@echo off
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat" x64
if errorlevel 1 exit /b 1
cd /d "%~dp0"
if exist build rmdir /s /q build
cmake -G "NMake Makefiles" -DCMAKE_BUILD_TYPE=Release -S . -B build
if errorlevel 1 exit /b 1
cmake --build build
if errorlevel 1 exit /b 1
build\rimkit.exe mod sync ..\..
build\rimkit.exe mod sync ..\..\src\examples\hello_lua
build\rimkit.exe mod sync ..\..\src\templates\lua-mod
exit /b %ERRORLEVEL%
