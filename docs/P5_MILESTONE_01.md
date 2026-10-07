# Dugout - Milestone 05
## Advanced SQL Performance Metrics

### Objective

Milestone 05 expands Dugout's diagnostic capabilities beyond
STATISTICS IO, STATISTICS TIME, and execution plans.

The objective is to capture additional SQL Server performance
signals that help identify the cause of performance problems.

Milestone 04 answered:

"How has benchmark performance changed over time?"

Milestone 05 answers:

"What SQL Server resources were consumed during execution?"

---

# Success Criteria

At completion of Milestone 05:

- Additional SQL metrics are captured.
- Metrics are persisted to the repository.
- Existing experiments remain functional.
- Existing execution plan functionality remains intact.
- Historical reporting continues to work.
- All tests pass.

---

# Non-Goals

Do not implement:

- AI Recommendations
- REST APIs
- Web UI
- Query Tuning Suggestions
- Distributed Execution
- Multi-Database Support

---

# Primary Deliverable

Extend benchmark metrics to include:

- Memory Grants
- TempDB Usage
- Parallelism Information

These metrics should be captured alongside
existing benchmark data.

---

# Memory Grant Collection

Capture:

```text
Requested Memory

Granted Memory

Used Memory
```

Purpose:

Identify over-grants and under-grants.

---

# TempDB Metrics

Capture:

```text
TempDB Allocations

TempDB Usage

Worktable Activity
```

Purpose:

Detect expensive sorting and spill scenarios.

---

# Parallelism Metrics

Capture:

```text
Degree Of Parallelism

Parallel Plan Indicator

Parallel Operators
```

Purpose:

Understand the impact of parallel execution.

---

# New Metrics Model

Extend benchmark metrics:

```csharp
BenchmarkMetrics
{
    DurationMs
    LogicalReads
    CpuTimeMs

    RequestedMemoryKb
    GrantedMemoryKb
    UsedMemoryKb

    TempDbPages

    DegreeOfParallelism
    UsedParallelPlan
}
```

---

# Benchmark Repository

Persist all new metrics.

Historical queries should support:

- Memory Usage Trends
- TempDB Trends
- Parallelism Trends

---

# Result Schema

Increment:

Version 3
→
Version 4

Maintain backward compatibility.

---

# New Repository Queries

Support:

```text
Top Memory Consumers

Highest TempDB Usage

Parallel vs Serial Runs
```

---

# Testing Requirements

Create tests for:

- Memory metric collection
- TempDB metric collection
- Parallelism metric collection
- Repository persistence
- Schema Version 4 support
- Backward compatibility

---

# Documentation

Update:

README.md

Include:

- New metrics
- Collection methods
- Example benchmark reports

---

# Acceptance Test

Run:

dugout run EXP001

Expected:

✅ Benchmark executes successfully

✅ Existing metrics collected

✅ Execution plan collected

✅ Extended metrics collected

✅ Repository persistence succeeds

✅ SchemaVersion = 4

✅ Tests pass

---

# Outcome

Milestone 05 is complete when Dugout can
measure and persist advanced SQL Server
resource utilization metrics in addition
to execution plans and diagnostic counters.

The platform evolves from:

SQL Performance Repository

to

SQL Performance Engineering Platform
``