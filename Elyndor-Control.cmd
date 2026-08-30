@echo off
title Elyndor Control Center
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\dev\Elyndor.ps1" -Action Menu
if errorlevel 1 pause
