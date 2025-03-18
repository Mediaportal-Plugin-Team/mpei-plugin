@echo off
cls
Title Creating MPEI Plugin Installer

IF "%programfiles(x86)%XXX"=="XXX" GOTO 32BIT
    :: 64-bit
    SET PROGS=%programfiles(x86)%
    GOTO CONT
:32BIT
    SET PROGS=%ProgramFiles%
:CONT

IF NOT EXIST "%PROGS%\Team MediaPortal\MediaPortal\" SET PROGS=C:

:: Get version from DLL
FOR /F "tokens=*" %%i IN ('..\Tools\Tools\sigcheck.exe /accepteula /nobanner /n "..\mpei-plugin\MPEIPlugin\bin\Release\MPEIPlugin.dll"') DO (SET version=%%i)

:: Temp xmp2 file
COPY ..\Setup\mpeiplugin.xmp2 ..\Setup\mpeipluginTemp.xmp2

:: Build MPE1
CD ..\Setup
"%PROGS%\Team MediaPortal\MediaPortal\MPEMaker.exe" ..\Setup\mpeipluginTemp.xmp2 /B /V=%version% /UpdateXML
CD ..\build

:: Cleanup
del ..\Setup\mpeipluginTemp.xmp2
