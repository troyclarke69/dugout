# Dugout - Milestone 07
## Regression Detection & Trend Analysis

### Objective

Milestone 07 introduces automated analysis of historical benchmark performance.

Previous milestones focused on collecting and storing benchmark data.

Milestone 07 focuses on identifying trends, regressions, and performance changes across benchmark runs.

The objective is to help answer:

```text
Did performance get worse?

When did it get worse?

What changed?

How does this run compare to history?
```

---

# Success Criteria

At completion of Milestone 07:

- Historical benchmark trends can be analyzed.
- Regressions can be detected automatically.
- Baselines can be established.
- Benchmark runs can be compared.
- Existing experiments remain functional.
- Existing repository functionality remains intact.
- All tests pass.

---

# Non-Goals

Do not implement:

- AI Analysis
- AI Recommendations
- REST APIs
- Web UI
- Multi-Database Support
- Automatic Query Tuning

---

# Primary Deliverable

Introduce trend analysis and regression detection.

Current:

```text
Benchmark Run
```

Future:

```text
Benchmark Run

Compared To History
```

---

# Performance Baselines

Every experiment can establish a baseline.

Example:

```text
EXP001

Baseline Duration

85 ms
```

Future runs are compared to this baseline.

---

# Regression Detection

Detect:

```text
Duration Regression

Logical Read Regression

CPU Regression

Elapsed Time Regression
```

---

# Regression Rules

Initial detection rules:

Minor Regression

```text
> 10%
```

Moderate Regression

```text
> 25%
```

Severe Regression

```text
> 50%
```

Thresholds should be configurable.

---

# Trend Analysis

Track:

```text
Duration

Logical Reads

CPU

Elapsed Time
```

across historical runs.

---

# Run Comparison

Support comparing two benchmark runs.

Example:

```bash
dugout compare run1 run2
```

Output:

```text
Duration

+12%

Logical Reads

+18%

CPU

+15%
```

---

# Historical Reporting

Support:

```bash
dugout history EXP001
```

Example:

```text
Run History

Date               Duration

2026-10-01          82 ms

2026-10-02          85 ms

2026-10-03          91 ms
```

---

# Performance Scores

Introduce benchmark health.

Example:

```text
Healthy

Warning

Regression
```

Based on configurable thresholds.

---

# Repository Changes

Add:

## BenchmarkBaselines

Store:

```text
Experiment Id

Baseline Run

Baseline Metrics
```

---

## BenchmarkComparisons

Store:

```text
Compared Run

Metric Differences

Detected Regressions
```

---

# New Services

Add:

```csharp
IRegressionAnalyzer

ITrendAnalyzer
```

Responsibilities:

```text
Calculate Trends

Detect Regressions

Calculate Baseline Comparisons
```

---

# Comparison Model

Example:

```csharp
BenchmarkComparison
{
    RunA
    RunB

    DurationDifference
    CpuDifference
    ReadDifference

    RegressionDetected
}
```

---

# Console Reporting

Example:

```text
------------------------------------------------

Regression Analysis

Duration

+14%

Status

Warning

Logical Reads

+2%

Status

Healthy

CPU

+16%

Status

Warning

------------------------------------------------
```

---

# Historical Queries

Support:

```bash
dugout baseline EXP001

dugout history EXP001

dugout compare run1 run2
```

---

# Testing Requirements

Create tests for:

## Regression Detection

Verify:

```text
Minor

Moderate

Severe
```

classification.

---

## Trend Analysis

Verify:

```text
Increasing

Stable

Decreasing
```

trends are detected.

---

## Baseline Comparison

Verify calculations.

---

## Repository Persistence

Verify baseline and comparison storage.

---

## Backward Compatibility

Verify existing repository data remains functional.

---

# Documentation

Update:

README.md

Include:

- Baselines
- Trend Analysis
- Comparison Commands
- Regression Detection

---

# Acceptance Test

Run:

```bash
dugout baseline EXP001

dugout run EXP001

dugout compare baseline latest

dugout history EXP001
```

Expected:

✅ Benchmark executes successfully

✅ Baseline established

✅ Historical trends available

✅ Regressions detected

✅ Comparison reports generated

✅ Repository stores comparisons

✅ Tests pass

---

# Outcome

Milestone 07 is complete when Dugout can identify and explain performance changes across benchmark history.

The platform evolves from:

```text
Performance Engineering Repository
```

to:

```text
Performance Regression Detection Platform
```