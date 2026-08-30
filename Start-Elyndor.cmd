@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\dev\Elyndor.ps1" -Action Start -Open
if errorlevel 1 pause
