# Dugout - Milestone 06
## Experiment Framework & Benchmark Library

### Objective

Milestone 06 expands Dugout from a benchmark platform into a structured SQL Server learning and experimentation environment.

Previous milestones focused on benchmark execution, diagnostics, plan analysis, and historical storage.

Milestone 06 focuses on:

```text
Benchmark Breadth
```

The objective is to create a reusable experiment framework and build a curated benchmark library covering common SQL Server performance patterns.

---

# Success Criteria

At completion of Milestone 06:

- Experiment metadata is supported.
- Experiments are categorized.
- Experiments can be searched and filtered.
- At least 20 benchmark experiments exist.
- Existing benchmarks remain functional.
- Historical tracking remains functional.
- Repository integration remains functional.
- All tests pass.

---

# Non-Goals

Do not implement:

- AI Analysis
- REST APIs
- Web UI
- Multi-Database Support
- Distributed Execution
- Automatic Query Tuning

---

# Primary Deliverable

Create a documented benchmark library.

Current:

```text
EXP001
```

Future:

```text
20+ Experiments
```

covering common SQL Server performance topics.

---

# Experiment Categories

Support:

```text
SARGability

Indexing

Joins

Aggregations

Parameter Sniffing

Data Types

Sorting

TempDB

CTEs

Window Functions
```

---

# Experiment Metadata

Extend experiment definition.

Example:

```json
{
  "id": "EXP005",
  "name": "Missing Covering Index",
  "category": "Indexing",
  "difficulty": "Intermediate",
  "tags": [
    "Index",
    "Lookup",
    "Performance"
  ]
}
```

---

# Difficulty Levels

Support:

```text
Beginner

Intermediate

Advanced
```

---

# Categories

Support:

```text
Indexing

Query Design

Cardinality

Execution Plans

Resource Usage

Parameter Sniffing
```

---

# Required Experiments

## SARGability

EXP001

Non-SARGable Date Filter

---

## Indexing

Missing Covering Index

Clustered vs Heap

Key Lookup Elimination

Filtered Index

Composite Index Ordering

---

## Joins

Nested Loops

Merge Join

Hash Match

Join Order

---

## Aggregations

GROUP BY Optimization

Stream Aggregate

Hash Aggregate

---

## Parameter Sniffing

Skewed Data

Parameter Sensitivity

Plan Reuse

---

## Resource Usage

Memory Grant

TempDB Spill

Parallel Query

---

# Experiment Documentation

Every experiment must provide:

```text
Description

Problem Statement

Expected Outcome

Performance Theory

Diagnostic Expectations

Plan Expectations
```

---

# Repository Changes

Support:

```text
Experiment Categories

Difficulty

Tags

Learning Objectives
```

Store inside repository database.

---

# New Commands

List experiments:

```bash
dugout list
```

Filter by category:

```bash
dugout list --category Indexing
```

Filter by difficulty:

```bash
dugout list --difficulty Advanced
```

Search by tag:

```bash
dugout list --tag ParameterSniffing
```

---

# Documentation

Create:

docs/experiments/

Examples:

```text
EXP001.md

EXP002.md

EXP003.md
```

Each experiment should become a miniature learning guide.

---

# Testing Requirements

Verify:

- Metadata loading
- Category filtering
- Difficulty filtering
- Tag filtering
- Repository persistence

All tests must pass.

---

# Acceptance Test

Expected:

✅ 20+ benchmark experiments

✅ Metadata support

✅ Category support

✅ Difficulty support

✅ Search support

✅ Repository integration

✅ Documentation created

✅ Tests pass

---

# Outcome

Milestone 06 is complete when Dugout evolves from a benchmark platform into a repeatable SQL Server performance learning environment.

The platform evolves from:

```text
Performance Engineering Platform
```

to:

```text
Performance Engineering Knowledge Base
```