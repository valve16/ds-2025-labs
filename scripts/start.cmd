@echo off
chcp 1251 > nul

set DB_MAIN=localhost:6000
set DB_RU=localhost:6001
set DB_EU=localhost:6002
set DB_ASIA=localhost:6003

cd ../Valuator
start dotnet run --urls "http://0.0.0.0:5001"

start dotnet run --urls "http://0.0.0.0:5002"

cd ../RankCalculator/RankCalculator
start dotnet run

cd ../../EventsLogger/EventsLogger
start dotnet run

pause

cd ../../nginx
start nginx.exe

pause