@echo off
setlocal enableextensions enabledelayedexpansion

docker run ^
  --name mono-compiler ^
  -it ^
  --rm ^
  -v %cd%:/protocol ^
  mono ^
  bash -c "cd /protocol && ./elixir.sh"

endlocal
