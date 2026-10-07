# Dugout - Milestone 02

## SQL Metrics & Diagnostics

### Objective

Milestone 02 expands Dugout beyond timing-based benchmarking and introduces SQL Server diagnostic metrics.

Milestone 01 answered:

```text
"Which query is faster?"
```

Milestone 02 answers:

```text
"Why is the query faster?"
```

The goal is to begin collecting SQL Server execution metrics that explain performance differences while maintaining the benchmark accuracy, repeatability, and observability established in Milestone 01.

---

# Success Criteria

At completion of Milestone 02:

- Dugout captures SQL diagnostic metrics.
- Diagnostic metrics are persisted with benchmark results.
- Existing benchmark functionality remains intact.
- Benchmark validation continues to work.
- Existing experiments remain compatible.
- EXP001 demonstrates measurable diagnostic differences.
- All automated tests pass.

---

# Non-Goals

Do not implement:

- Execution Plan Analysis
- Query Plan Visualization
- Query Store Integration
- Wait Statistics
- Memory Grants
- TempDB Analysis
- Parallelism Analysis
- Historical Result Comparison
- Results Database
- REST API
- Web UI
- AI-assisted diagnostics

These are candidates for future milestones.

---

# Primary Deliverable

Add SQL Server execution metrics alongside existing duration metrics.

Current output:

```text
Duration
Rows Returned
```

New output:

```text
Duration
Rows Returned

Logical Reads
Physical Reads
Scan Count

CPU Time
Elapsed Time
```

---

# SQL Metrics Collection

## Source

Use:

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
```

for benchmark execution.

Metrics should be collected automatically by Dugout.

---

# Metrics To Capture

## Logical Reads

Example:

```text
Table 'Orders'.
logical reads 15742
```

Capture:

```text
LogicalReads
```

---

## Physical Reads

Capture:

```text
PhysicalReads
```

---

## Scan Count

Capture:

```text
ScanCount
```

---

## SQL CPU Time

Example:

```text
CPU time = 156 ms
```

Capture:

```text
CpuTimeMs
```

---

## SQL Elapsed Time

Example:

```text
elapsed time = 189 ms
```

Capture:

```text
SqlElapsedTimeMs
```

---

# Metrics Model Changes

Extend BenchmarkRun metrics.

Example:

```csharp
BenchmarkRunMetric
{
    DurationMs
    RowsReturned

    LogicalReads
    PhysicalReads
    ScanCount

    CpuTimeMs
    SqlElapsedTimeMs
}
```

All metrics should support future aggregation.

---

# Statistics Support

Calculate statistics for:

```text
Duration

Logical Reads

Physical Reads

CPU Time

Elapsed Time
```

Store:

```text
Min
Max
Average
Median
StandardDeviation
```

---

# Results Persistence

Extend result JSON.

Current:

```json
{
  "durationMs": 75.4
}
```

Future:

```json
{
  "durationMs": 75.4,
  "logicalReads": 1823,
  "physicalReads": 0,
  "scanCount": 1,
  "cpuTimeMs": 47,
  "sqlElapsedTimeMs": 52
}
```

Store raw values for every measured iteration.

Do not store only aggregates.

---

# Benchmark Summary

Console output should include diagnostics.

Example:

```text
------------------------------------------------

Baseline

Duration       248 ms
Reads        15,742
CPU            189 ms

------------------------------------------------

Optimized

Duration        76 ms
Reads         1,225
CPU             48 ms

------------------------------------------------

Improvement

Duration       69%
Reads          92%
CPU            74%

------------------------------------------------
```

---

# Diagnostic Comparison

Add metric-level improvement calculations.

Example:

```text
Duration Improvement

(Baseline - Optimized)
/
Baseline
```

Apply to:

```text
Duration
Logical Reads
CPU
Elapsed Time
```

---

# EXP001 Validation

The experiment should demonstrate meaningful diagnostic changes.

Expected observations:

```text
Reduced logical reads

Reduced CPU time

Reduced elapsed time

Reduced scan activity
```

Exact numbers are not required.

Metric differences should support the benchmark conclusion.

---

# Result Schema Version

Increment result schema.

Current:

```text
SchemaVersion = 1
```

New:

```text
SchemaVersion = 2
```

Maintain backwards compatibility when reading Version 1 result files.

---

# New Components

Add:

```text
ISqlMetricsCollector
```

Responsibilities:

```text
Execute benchmark query

Capture STATISTICS IO

Capture STATISTICS TIME

Parse results

Return structured metrics
```

---

# Testing Requirements

Create automated tests for:

## IO Parsing

Verify:

```text
Logical Reads

Physical Reads

Scan Count
```

are extracted correctly.

---

## TIME Parsing

Verify:

```text
CPU Time

Elapsed Time
```

are extracted correctly.

---

## Statistics Generation

Verify calculations for:

```text
Duration

Reads

CPU
```

---

## JSON Serialization

Verify:

```text
SchemaVersion 2
```

is persisted correctly.

---

## Backward Compatibility

Verify:

```text
SchemaVersion 1
```

results can still be loaded.

---

# Documentation

Update:

```text
README.md
```

to include:

- New metrics
- Example output
- Sample benchmark report

---

# Acceptance Test

Run:

```bash
dugout run EXP001
```

Expected:

✅ Validation passes

✅ Duration metrics collected

✅ SQL metrics collected

✅ Diagnostic improvements calculated

✅ Result file created

✅ SchemaVersion = 2

✅ Tests pass

---

# Outcome

Milestone 02 is complete when Dugout can explain benchmark results using SQL Server diagnostics rather than duration alone.

The milestone successfully transitions Dugout from:

```text
Benchmark Runner
```

toward:

```text
SQL Performance Engineering Platform
```