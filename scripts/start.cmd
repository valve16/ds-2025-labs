@echo off
chcp 1251 > nul

set REDIS_PASS=password1

set RABBIT_USER=admin
set RABBIT_PASS=password2

cd ../Valuator
start dotnet run --urls "http://0.0.0.0:5001"

start dotnet run --urls "http://0.0.0.0:5002"

cd ../RankCalculator/RankCalculator
start dotnet run

cd ../../EventsLogger/EventsLogger
start dotnet run

pause

cd D:\nginx\
start nginx.exe

pause
