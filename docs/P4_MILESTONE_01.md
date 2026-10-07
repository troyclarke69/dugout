# Dugout - Milestone 04
## Results Repository & Historical Analysis

### Objective

Milestone 04 introduces a dedicated results repository.

Current milestones provide detailed information for individual benchmark runs.

Milestone 04 answers:

```text
What happened across all benchmark runs?
```

The objective is to move from isolated result files toward a searchable benchmark history platform.

---

# Success Criteria

At completion of Milestone 04:

- Benchmark results are stored in a dedicated repository database.
- Existing JSON result generation remains intact.
- Historical benchmark runs can be queried.
- Benchmark metrics can be compared over time.
- Execution plans can be queried and analyzed.
- Experiment versions are tracked.
- Environment metadata is searchable.
- All automated tests pass.

---

# Non-Goals

Do not implement:

- Web UI
- REST API
- AI Analysis
- Automatic Recommendations
- Wait Statistics
- Query Store Integration
- Multi-Database Support
- Cloud Synchronisation

These remain future milestones.

---

# Primary Deliverable

Introduce:

```text
DugoutResults
```

as the benchmark repository database.

JSON remains the export/archive format.

The database becomes the primary queryable store.

---

# Repository Goals

Support questions such as:

```sql
Which experiments showed the
largest logical read reduction?

Which benchmark runs introduced regressions?

How do SQL Server versions compare?

Which execution plans changed
between benchmark runs?
```

---

# Storage Strategy

Current:

```text
Benchmark Run
    -> JSON
```

New:

```text
Benchmark Run
    -> JSON

Benchmark Run
    -> DugoutResults Database
```

Both persistence methods remain active.

---

# Database Design

## Experiments

Store:

```text
Experiment Id

Name

Description

Current Hash
```

---

## ExperimentVersions

Store:

```text
Experiment Hash

Created Date

Definition Snapshot
```

---

## BenchmarkRuns

Store:

```text
Run Id

Experiment Id

Status

Start Time

End Time

Duration Metrics
```

---

## BenchmarkMetrics

Store:

```text
Duration

Logical Reads

Physical Reads

CPU

Elapsed Time
```

Per measured execution.

---

## ExecutionPlans

Store:

```text
Plan Hash

Estimated Cost

Operator Summary

Plan XML
```

---

## Environments

Store:

```text
SQL Version

Edition

.NET Version

Runner Version

Machine Name
```

---

# Repository Services

Add:

```csharp
IBenchmarkRepository
```

Responsibilities:

```text
Store Benchmark Runs

Query Benchmark History

Retrieve Execution Plans

Retrieve Experiment Versions
```

---

# Historical Queries

Support:

```text
Latest Benchmark

Benchmark History

Runs By Experiment

Runs By SQL Version

Runs By Environment
```

No UI required.

Console reporting is sufficient.

---

# New Commands

List benchmark history:

```bash
dugout history EXP001
```

Show recent runs:

```bash
dugout history EXP001 --latest 10
```

Compare runs:

```bash
dugout compare <run1> <run2>
```

Simple textual output only.

---

# Plan History

Support:

```text
Plan Hash Tracking

Plan Change Detection

Operator Change Tracking
```

Enable historical plan comparisons.

---

# Result Schema

Schema Version remains:

```text
Version 3
```

No breaking JSON changes should be required.

---

# Testing Requirements

Create tests for:

## Repository Storage

Verify benchmark runs persist.

---

## Repository Queries

Verify benchmark history retrieval.

---

## Plan Persistence

Verify execution plans persist correctly.

---

## Historical Comparison

Verify benchmark comparisons work.

---

## Backward Compatibility

Verify existing JSON result files remain usable.

---

# Documentation

Update:

```text
README.md
```

to include:

- Repository setup
- Database schema
- Historical queries
- Comparison commands

---

# Acceptance Test

Run:

```bash
dugout run EXP001
```

Expected:

✅ Result JSON created

✅ Result persisted to repository

✅ Repository query 