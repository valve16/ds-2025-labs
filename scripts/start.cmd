@echo off
chcp 1251 > nul

cd ../Valuator
start dotnet run --urls "http://0.0.0.0:5001"

start dotnet run --urls "http://0.0.0.0:5002"

pause

cd D:\nginx\
start nginx.exe

pause