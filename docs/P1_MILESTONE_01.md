# Dugout - Milestone 01

## Objective

Build an executable benchmark runner capable of:

1. Loading a benchmark experiment
2. Executing warmup runs
3. Executing baseline SQL
4. Executing optimized SQL
5. Measuring duration
6. Calculating statistics
7. Persisting benchmark results
8. Displaying benchmark summaries

Milestone 01 focuses only on proving the benchmark engine.

---

## Non Goals

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
- Distributed Execution
- Query Store Integration
- Wait Statistics
- Execution Plan Analysis
- Cloud Deployment
- Multi-Database Support

Implement only what is required for Milestone 01.

---

## Technology Stack

- .NET 9
- C# 13
- System.CommandLine
- System.Text.Json
- SQL Server 2022
- Docker Compose
- xUnit

Use only required dependencies.

---

## Solution Structure

src/

    Dugout.Runner

    Dugout.Contracts

    Dugout.Storage

tests/

    Dugout.Runner.Tests

experiments/

results/

---

## Dataset

Create database:

SalesLab

Table:

Orders

Columns:

OrderId INT IDENTITY
CustomerId INT
OrderDate DATETIME2
TotalAmount MONEY

Seed exactly 1,000,000 rows.

Distribute OrderDate values across five years.

Provide Docker setup and seed scripts.

---

## Experiment Format

Store experiments as JSON.

Example:

{
  "id": "EXP001",
  "name": "Non-SARGable Date Filter",
  "setupScript": "",
  "validationScript": "",
  "baselineScript": "",
  "optimizedScript": "",
  "cleanupScript": ""
}

---

## Sample Experiment

EXP001

Non-SARGable Date Filter

Baseline:

WHERE YEAR(OrderDate) = 2025

Optimized:

WHERE OrderDate >= '20250101'
AND OrderDate < '20260101'

---

## Benchmark Options

Default Configuration:

WarmupRuns = 3

MeasuredRuns = 10

Warmup runs are not recorded.

---

## Execution Pipeline

1. Load experiment

2. Validate experiment

3. Execute setup script

4. Execute warmup runs

5. Execute baseline query

6. Collect metrics

7. Execute optimized query

8. Collect metrics

9. Calculate improvements

10. Persist results

11. Execute cleanup script

12. Display benchmark summary

---

## Metrics

Milestone 01 captures only:

- DurationMs
- RowsReturned

Additional metrics will be added in future milestones.

---

## Statistical Rules

Record:

- Min
- Max
- Average
- Median

Use Median for benchmark comparisons.

Improvement Formula:

(BaselineMedian - OptimizedMedian)
/
BaselineMedian
*
100

---

## Result Storage

Store benchmark results as:

results/EXP001/{runid}.json

Example:

{
  "runId": "",
  "experimentId": "",
  "startedUtc": "",
  "baseline": {
      "medianDurationMs": 0
  },
  "optimized": {
      "medianDurationMs": 0
  },
  "improvement": {
      "durationImprovementPercent": 0
  }
}

---

## CLI Commands

dugout list

dugout run EXP001

dugout run EXP001 --warmups 5

dugout run EXP001 --iterations 25

---

## Progress Output

Loading Experiment

Running Warmups

Running Baseline

Running Optimized

Calculating Statistics

Saving Results

---

## Tests

Create tests for:

- Median calculation
- Improvement calculation
- Experiment loading
- Result persistence
- Benchmark pipeline execution

---

## Success Criteria

At completion of Milestone 01:

- A benchmark can be executed against SQL Server
- Repeatable benchmark results can be produced
- Results are persisted
- Benchmark summaries are displayed
- Tests pass

The command:

dugout run EXP001

must execute successfully from end to end.