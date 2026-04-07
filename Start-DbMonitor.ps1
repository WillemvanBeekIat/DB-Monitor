<# 
.SYNOPSIS
    Start DbMonitor with PostgreSQL in Docker

.DESCRIPTION
    This script starts the PostgreSQL Docker container and then runs the DbMonitor application.
    Use -Stop to stop all containers, -Reset to reset the database.

.PARAMETER Stop
    Stop all running containers

.PARAMETER Reset
    Reset the database (removes all data)

.PARAMETER WithPgAdmin
    Also start pgAdmin for database management (available at http://localhost:5050)

.EXAMPLE
    .\Start-DbMonitor.ps1
    # Starts PostgreSQL and the application

.EXAMPLE
    .\Start-DbMonitor.ps1 -WithPgAdmin
    # Starts PostgreSQL, pgAdmin, and the application

.EXAMPLE
    .\Start-DbMonitor.ps1 -Stop
    # Stops all containers

.EXAMPLE
    .\Start-DbMonitor.ps1 -Reset
    # Resets the database and starts fresh
#>

param(
    [switch]$Stop,
    [switch]$Reset,
    [switch]$WithPgAdmin
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

function Write-Status($message) {
    Write-Host "[DbMonitor] " -ForegroundColor Cyan -NoNewline
    Write-Host $message
}

function Test-DockerRunning {
    try {
        $null = docker info 2>&1
        return $true
    }
    catch {
        return $false
    }
}

# Check Docker is running
if (-not (Test-DockerRunning)) {
    Write-Host "ERROR: Docker is not running. Please start Docker Desktop and try again." -ForegroundColor Red
    exit 1
}

# Stop containers
if ($Stop) {
    Write-Status "Stopping containers..."
    docker compose --profile tools down
    Write-Status "Containers stopped."
    exit 0
}

# Reset database
if ($Reset) {
    Write-Status "Resetting database..."
    docker compose --profile tools down -v
    Write-Status "Database reset. Volumes removed."
}

# Start PostgreSQL
Write-Status "Starting PostgreSQL container..."
if ($WithPgAdmin) {
    docker compose --profile tools up -d
    Write-Status "pgAdmin available at: http://localhost:5050"
    Write-Status "  Email: admin@dbmonitor.local"
    Write-Status "  Password: admin"
}
else {
    docker compose up -d
}

# Wait for PostgreSQL to be healthy
Write-Status "Waiting for PostgreSQL to be ready..."
$maxAttempts = 30
$attempt = 0
do {
    $attempt++
    $health = docker inspect --format='{{.State.Health.Status}}' dbmonitor-postgres 2>$null
    if ($health -eq "healthy") {
        break
    }
    if ($attempt -ge $maxAttempts) {
        Write-Host "ERROR: PostgreSQL failed to start within timeout." -ForegroundColor Red
        docker compose logs postgres
        exit 1
    }
    Start-Sleep -Seconds 1
} while ($true)

Write-Status "PostgreSQL is ready!"
Write-Status ""
Write-Status "Connection Details:"
Write-Status "  Host: localhost"
Write-Status "  Port: 5432"
Write-Status "  Database: dbmonitor"
Write-Status "  Username: dbmonitor"
Write-Status "  Password: dbmonitor_dev_password"
Write-Status ""
Write-Status "Starting DbMonitor application..."
Write-Status ""

# Run the application
Set-Location "$scriptDir\src\DbMonitor.Web"
dotnet run
