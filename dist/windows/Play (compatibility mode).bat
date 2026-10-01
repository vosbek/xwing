@echo off
rem Use this if XWing.exe says your video card does not support Vulkan (older GPUs, VMs, remote desktop).
start "" "%~dp0XWing.exe" --rendering-driver opengl3
