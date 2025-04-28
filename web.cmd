bin\igorc.exe -v -t ts -x "gen_ts\*.cs" -p "igor\common" -p "igor\db" -p "igor\web" -o ..\src\app\protocol *.igor
if errorlevel 1 pause && exit

copy /B /V /Y "ts\igor.ts" "..\src\app\protocol"
if errorlevel 1 pause && exit
