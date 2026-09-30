# Dugout - Milestone 01 (Revised)

## Objective

Build an executable benchmark runner capable of:

1. Loading a benchmark experiment
2. Validating the experiment definition
3. Preparing a known database state
4. Executing warmup runs
5. Executing baseline SQL
6. Executing optimized SQL
7. Measuring execution duration
8. Validating result equivalence
9. Calculating benchmark statistics
10. Persisting benchmark results
11. Displaying benchmark summaries

Milestone 01 exists to prove the benchmark engine and establish trustworthy benchmark methodology.

---

# Non-Goals

Do not implement:

- REST APIs
- Web UI
- Authentication
- RBAC
- Background Services
- Message Queues
- CQRS
- MediatR
- Event Sourcing
- AI Features
- Query Store Integration
- Wait Statistics
- Execution Plan Analysis
- Parallel Execution
- Multi-Database Support
- Distributed Agents
- Cloud Deployment
- Statistical Significance Testing
- Automatic Regression Detection

Implement only what is required for Milestone 01.

---

# Technology Stack

Preferred:

- .NET 10
- C# 14
- Microsoft.Data.SqlClient
- System.Text.Json
- SQL Server 2022
- Docker Compose
- xUnit

Use only required dependencies.

The CLI uses a hand-rolled parser. `System.CommandLine` is intentionally not used for the two supported commands (`list` and `run`).

---

# Solution Structure

src/

    Dugout.Runner

    Dugout.Contracts

    Dugout.Storage

tests/

    Dugout.Runner.Tests

experiments/

results/

docker/

docs/

---

# Guiding Principles

The implementation must follow all principles defined in:

ARCHITECTURE_PRINCIPLES.md

Particularly:

- Benchmark Accuracy Over Convenience
- Repeatability Over Speed
- Measured Results Over Assumptions
- No Hidden State
- Raw Data Must Be Preserved
- Observability Is A Feature

---

# Dataset Definition

Create database:

SalesLab

Compatibility Level:

160

Recovery Model:

SIMPLE

Table:

Orders

```sql
CREATE TABLE dbo.Orders
(
    OrderId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CustomerId INT NOT NULL,
    OrderDate DATETIME2(3) NOT NULL,
    TotalAmount DECIMAL(19,4) NOT NULL
);
```

Seed Requirements:

- Exactly 1,000,000 rows
- Deterministic seed value
- Seed value must be documented
- Rows must be generated identically every time
- Date range:

```text
2022-01-01
through
2026-12-31
```

Customer Distribution:

```text
CustomerId 1 - 50,000
```

After seeding:

```sql
UPDATE STATISTICS dbo.Orders WITH FULLSCAN;
```

The runner must verify dataset integrity before benchmark execution.

Minimum verification:

- Database exists
- Orders table exists
- Row count = 1,000,000

---

# Experiment Format

Experiments are stored as JSON.

Example:

```json
{
  "schemaVersion": 1,
  "id": "EXP001",
  "name": "Non-SARGable Date Filter",
  "description": "Query optimization using SARGable predicates.",
  "setupScript": "scripts/exp001/setup.sql",
  "baselineScript": "scripts/exp001/baseline.sql",
  "optimizedScript": "scripts/exp001/optimized.sql",
  "cleanupScript": "scripts/exp001/cleanup.sql"
}
```

Required Fields:

- schemaVersion
- id
- name
- baselineScript
- optimizedScript

Optional Fields:

- description
- setupScript
- validationScript
- cleanupScript

Experiment IDs must match:

```text
EXP###
```

Examples:

```text
EXP001
EXP002
EXP050
```

Scripts are stored as .sql files.

Do not embed multi-line SQL inside JSON.

---

# Experiment Execution Rules

Every experiment must be:

- Self-contained
- Repeatable
- Idempotent

Setup and cleanup scripts must be safe to execute multiple times.

Experiments may not rely on manual configuration.

---

# Sample Experiment

## EXP001

Non-SARGable Date Filter

Purpose:

Demonstrate benefit of SARGable predicates.

Setup Script:

Create covering index.

```sql
CREATE INDEX IX_Orders_OrderDate
ON dbo.Orders(OrderDate)
INCLUDE (TotalAmount);
```

Baseline:

```sql
SELECT SUM(TotalAmount)
FROM dbo.Orders
WHERE YEAR(OrderDate) = 2025;
```

Optimized:

```sql
SELECT SUM(TotalAmount)
FROM dbo.Orders
WHERE OrderDate >= '2025-01-01'
  AND OrderDate < '2026-01-01';
```

Cleanup:

```sql
DROP INDEX IF EXISTS IX_Orders_OrderDate
ON dbo.Orders;
```

---

# Validation Rules

Validation serves two purposes.

## Definition Validation

Validate:

- JSON structure
- Required fields
- Script existence
- ID format

Failure stops execution.

---

## Result Validation

Before warmups, the runner executes each query once without timing and compares the returned row counts and values. This pre-warmup value comparison is mandatory. A mismatch fails validation and skips warmups and measured runs.

`validationScript` is optional and additive. When present, it runs after measured execution and passes only when it returns an explicit `1` or `true` value. If it fails, persist the measured runs and statistics, mark the benchmark FAILED, and report the validation failure.

---

# Benchmark Options

Default Values:

```text
WarmupRuns = 3
MeasuredRuns = 10
```

Warmup runs are never included in final statistics.

Measured runs are recorded.

---

# Execution Policy

Milestone 01 uses:

```text
WARM CACHE ONLY
```

The runner does not:

- Clear plan cache
- Clear buffer cache

This is intentional.

Cold-cache benchmarking is deferred to a future milestone.

---

# Timing Rules

Connection establishment is excluded from timing.

Use:

```csharp
Stopwatch
```

Timing starts:

```text
Immediately before command execution
```

Timing ends:

```text
After the complete result set
has been fully read and discarded.
```

Use a dedicated open connection.

All benchmark iterations for a run use the same connection.

Duration must be recorded using decimal precision.

---

# Warmup Rules

Warmups must be fair.

Execute:

```text
Baseline Warmups
```

then

```text
Optimized Warmups
```

Neither contributes to benchmark statistics.

---

# Measured Run Order

To reduce drift:

Execute measured runs in alternating order.

Example:

```text
Baseline 1
Optimized 1

Baseline 2
Optimized 2

Baseline 3
Optimized 3
```

Continue until all iterations finish.

The execution order must be stored in the result file.

---

# Metrics

Milestone 01 captures only:

```text
DurationMs
RowsReturned
```

Future metrics are intentionally excluded.

Deferred:

- STATISTICS IO
- CPU Time
- Wait Statistics
- Memory Grants
- Spills
- Parallelism
- Execution Plans

---

# Statistical Rules

Record:

- Min
- Max
- Average
- Median
- Standard Deviation

Median is the official comparison metric.

Standard deviation uses the sample formula with denominator `n - 1`.

---

## Median Rule

Even-sized datasets:

```text
Average of middle two values
```

Example:

```text
[10,20,30,40]

Median = 25
```

---

## Improvement Rule

```text
(BaselineMedian - OptimizedMedian)
/
BaselineMedian
*
100
```

Requirements:

- Negative values represent regressions
- Do not clamp values
- Division-by-zero must be handled safely

---

# Failure Handling

Cleanup always executes.

Implementation must follow:

```csharp
try
{
    // benchmark execution
}
finally
{
    // cleanup
}
```

Failed runs must still be persisted.

Store:

- Failure reason
- Execution stage
- Timestamp

---

# Timeouts

Every SQL command requires:

```text
CommandTimeout
```

Default:

```text
60 seconds
```

Support cancellation tokens.

---

# Result Storage

Store results as:

```text
results/
    EXP001/
        {runId}.json
```

Result Format:

```json
{
  "schemaVersion": 1,
  "runId": "01K123ABC",
  "experimentId": "EXP001",
  "experimentHash": "SHA256",
  "startedUtc": "2026-09-29T18:15:12Z",
  "completedUtc": "2026-09-29T18:15:42Z",
  "status": "Success",
  "options": {
    "warmupRuns": 3,
    "measuredRuns": 10
  },
  "environment": {
    "sqlServerVersion": "",
    "runnerVersion": "",
    "dotnetVersion": "",
    "machineName": ""
  },
  "baseline": {
    "runs": [],
    "statistics": {}
  },
  "optimized": {
    "runs": [],
    "statistics": {}
  },
  "validation": {
    "passed": true,
    "details": ""
  },
  "improvement": {
    "durationPercent": 0
  }
}
```

---

# Raw Data Preservation

Store:

For every measured run:

```text
Duration
Rows Returned
Execution Order
```

Never store only calculated statistics.

Statistics must be derived from stored raw data.

---

# Observability

Every benchmark run must capture:

- Run ID
- Experiment ID
- Experiment Hash
- Timestamp
- Runner Version
- SQL Server Version
- Benchmark Options
- Validation Result

A benchmark should be explainable after execution without rerunning it.

---

# CLI Commands

The CLI uses a hand-rolled parser. `System.CommandLine` is intentionally not used for the two supported commands.

List experiments:

```bash
dugout list
```

Run benchmark:

```bash
dugout run EXP001
```

Override warmup count:

```bash
dugout run EXP001 --warmups 5
```

Override iteration count:

```bash
dugout run EXP001 --iterations 25
```

Connection string source:

```text
DUGOUT_CONNECTION
```

environment variable.

Connection strings must never be written to result files.

---

# Exit Codes

```text
0 = Success

1 = Definition Validation Failed

2 = Benchmark Validation Failed

3 = Execution Failure
```

---

# Console Output

Example:

Loading Experiment

Validating Definition

Validating Dataset

Running Baseline Warmups

Running Optimized Warmups

Executing Benchmarks

Validating Results

Calculating Statistics

Saving Results

Benchmark Complete

---

# Tests

Create tests for:

- Experiment definition validation
- Median calculation
- Standard deviation calculation
- Improvement calculation
- Experiment loading
- Result persistence
- Result validation
- Benchmark pipeline execution
- Dataset validation
- Failure handling

---

# Success Criteria

Milestone 01 is complete when:

1. Docker environment starts successfully

2. Dataset is seeded deterministically

3. EXP001 executes successfully

4. Validation passes

5. Raw benchmark data is persisted

6. Benchmark statistics are generated

7. Results can be reproduced across multiple runs

8. All automated tests pass

9. The command:

```bash
dugout run EXP001
```

executes successfully end-to-end

10. The generated result file contains sufficient data
to explain and reproduce the benchmark outcome.

11. Provisional repeatability criterion: on a freshly seeded dataset, three consecutive runs all pass validation; the coefficient of variation for measured durations is at most 10% for each variant; each run's improvement is within ±5 percentage points of the three-run mean improvement; and the optimized median is faster in all three runs.