@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\dev\Elyndor.ps1" -Action Stop
if errorlevel 1 pause
