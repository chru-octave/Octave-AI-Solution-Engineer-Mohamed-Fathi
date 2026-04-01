@echo off
echo Starting Insurance Extraction System...

echo.
echo [1/2] Starting Backend API on http://localhost:5000
start "Insurance API" cmd /k "cd src\backend\InsuranceExtraction.API && dotnet run --urls http://localhost:5000"

echo [2/2] Starting Frontend on http://localhost:5173
timeout /t 3 /nobreak >nul
start "Insurance Frontend" cmd /k "cd src\frontend && npm run dev"

echo.
echo Both services starting...
echo API:      http://localhost:5000
echo Swagger:  http://localhost:5000/swagger
echo Frontend: http://localhost:5173
echo.
pause
