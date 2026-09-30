# Dugout

Dugout is a benchmark runner focused on SQL Server performance experiments. The project is being built in line with the Phase 1 requirements in the repo docs, with emphasis on repeatable, observable, and trustworthy benchmark execution.

## Purpose

The current milestone establishes the benchmark engine foundation for:

- loading and validating experiment definitions
- preparing a known database state
- running warmup and measured SQL executions
- capturing raw timing data and row counts
- validating equivalence between baseline and optimized queries
- calculating summary statistics and improvement percentages
- persisting benchmark output for later analysis

## Current Status

The repository now contains the initial working scaffold for Milestone 01, including:

- a .NET solution with runner, contracts, storage, and tests
- a CLI runner with `list` and `run` commands
- experiment validation and statistical calculation logic
- a sample `EXP001` benchmark definition
- local SQL Server docker setup for a benchmark dataset

## Solution Structure

```text
src/
  Dugout.Runner/
  Dugout.Contracts/
  Dugout.Storage/

tests/
  Dugout.Runner.Tests/

experiments/
  EXP001/

docker/
  docker-compose.yml
  seed-saleslab.sql

docs/
  ARCHITECTURE_PRINCIPLES.md
  P1_MILESTONE_01_REVISED.md
```

## Prerequisites

- Docker Desktop running (Linux containers), with at least 4 GB of memory available to Docker
- .NET 10 SDK (`dotnet --version` shows 10.x)
- PowerShell, opened in the repository root (for example `cd C:\Projects\dugout`)
- Port 1433 free. If a local SQL Server instance is using it, stop that instance first.

## Run Locally

1. Start SQL Server and wait until it reports healthy:

```powershell
   docker compose -f docker/docker-compose.yml up -d --wait
```

2. Seed the database (creates `SalesLab` and 1,000,000 rows in `dbo.Orders`):

```powershell
   docker cp .\docker\seed-saleslab.sql dugout-sqlserver:/tmp/seed-saleslab.sql
   docker exec dugout-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong!Passw0rd' -C -i /tmp/seed-saleslab.sql
```

   Success looks like `(1000000 rows affected)` with no lines starting with `Msg`.

3. Set the connection string for this PowerShell session (repeat in any new window):

```powershell
   $env:DUGOUT_CONNECTION = 'Server=localhost,1433;Database=SalesLab;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=False;TrustServerCertificate=True;'
```

4. List experiments:

```powershell
   dotnet run -c Release --project src/Dugout.Runner -- list
```

5. Run the sample benchmark, then show its exit code:

```powershell
   dotnet run -c Release --project src/Dugout.Runner -- run EXP001
   $LASTEXITCODE
```

   Results are written to `results\EXP001\<runId>.json`.

To change the default 3 warmups and 10 measured iterations, pass either or both options:

```powershell
dotnet run -c Release --project src/Dugout.Runner -- run EXP001 --warmups 5 --iterations 25
```

Exit codes: 0 = success, 1 = invalid input or experiment definition, 2 = baseline/optimized results differ, 3 = execution failure.

## Milestone 01 Acceptance Test

Follow these steps in order, in one PowerShell window. Avoid other heavy work on the machine while benchmarks run.

**A. Unit tests**

```powershell
dotnet test Dugout.slnx --nologo
```

Expected: all tests pass.

**B. Fresh environment** (deletes any existing data)

```powershell
docker compose -f docker/docker-compose.yml down -v
docker compose -f docker/docker-compose.yml up -d --wait
docker cp .\docker\seed-saleslab.sql dugout-sqlserver:/tmp/seed-saleslab.sql
docker exec dugout-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong!Passw0rd' -C -i /tmp/seed-saleslab.sql
$env:DUGOUT_CONNECTION = 'Server=localhost,1433;Database=SalesLab;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=False;TrustServerCertificate=True;'
```

Expected: `(1000000 rows affected)`, no `Msg` lines.

**C. Repeatability: three consecutive runs**

Run this block three times in a row:

```powershell
dotnet run -c Release --project src/Dugout.Runner -- run EXP001
$LASTEXITCODE
```

Expected each time: exit code `0`, `Validation: Passed`, and a new file in `results\EXP001\`.

**D. Failure check 1: database unavailable**

```powershell
docker stop dugout-sqlserver
dotnet run -c Release --project src/Dugout.Runner -- run EXP001
$LASTEXITCODE
docker compose -f docker/docker-compose.yml up -d --wait
```

Expected: exit code `3`, summary shows `Failure stage: Opening Connection`, and a result file is still written.

**E. Failure check 2: results differ**

1. Open `experiments\EXP001\scripts\exp001\optimized.sql` and change `'2026-01-01'` to `'2025-12-01'`. Save.
2. Run:

```powershell
   dotnet run -c Release --project src/Dugout.Runner -- run EXP001
   $LASTEXITCODE
```

   Expected: exit code `2`, `Failure stage: Validating Results`, and no measured runs in the result file.
3. Change `'2025-12-01'` back to `'2026-01-01'`. Save.
4. Run once more:

```powershell
   dotnet run -c Release --project src/Dugout.Runner -- run EXP001
   $LASTEXITCODE
```

   Expected: exit code `0`, and `experimentHash` in this result file matches the hash in the step C files (confirms the file was restored exactly).

**F. Collect results**

All result files are in `results\EXP001\`. There should be six: three from C, one from D, two from E.

## Stop / Reset

- Stop SQL Server, keep data: `docker compose -f docker/docker-compose.yml down`
- Stop and delete all data: `docker compose -f docker/docker-compose.yml down -v`

## Troubleshooting

- `sqlcmd` not found: use `/opt/mssql-tools/bin/sqlcmd` instead and remove the `-C` flag.
- `Login failed for user 'sa'` right after startup: wait 10 seconds and retry.
- Port 1433 already in use: stop the local SQL Server service, then rerun step 1.
- `DUGOUT_CONNECTION environment variable is required`: you are in a new PowerShell window; rerun step 3.

## Benchmark Principles Followed

This implementation follows the guiding principles from the docs:

- benchmark accuracy over convenience
- repeatability over speed
- measured data over assumptions
- no hidden state
- raw data preserved in results
- observability as a feature

## Verification

Run the Milestone 01 Acceptance Test above.

## Notes

This is the foundation for Milestone 01, not a full production benchmark platform. The design intentionally avoids non-goal features such as REST APIs, UIs, and background services, keeping scope aligned with the project specification.
