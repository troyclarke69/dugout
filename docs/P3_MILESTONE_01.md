# Dugout - Milestone 03
## Execution Plans & Plan Intelligence

### Objective

Milestone 03 introduces execution plan collection and plan intelligence.

Milestone 02 answered:

```text
Why is the query faster?
```

using diagnostic metrics such as:

- Logical Reads
- CPU Time
- SQL Elapsed Time

Milestone 03 answers:

```text
What changed in the optimizer?
```

The objective is to capture execution plans and expose optimizer behaviour responsible for benchmark outcomes.

---

# Success Criteria

At completion of Milestone 03:

- Execution plans are captured automatically.
- Baseline and optimized plans are persisted.
- Plan metadata is collected.
- Plan hashes are generated.
- Operator information is extracted.
- Existing benchmark functionality remains intact.
- Existing metric collection remains intact.
- EXP001 demonstrates observable plan differences.
- All automated tests pass.

---

# Non-Goals

Do not implement:

- AI Analysis
- AI Recommendations
- Execution Plan Visualisation
- Web UI
- Query Store Integration
- Wait Statistics
- Memory Grants
- TempDB Analysis
- Historical Benchmark Comparisons
- Results Repository Database

These belong to future milestones.

---

# Primary Deliverable

Add execution plan collection to every benchmark run.

Current benchmark output:

```text
Duration
Reads
CPU
Elapsed Time
```

New benchmark output:

```text
Duration
Reads
CPU
Elapsed Time

Execution Plan
Plan Metadata
Plan Hash
Plan Operators
```

---

# Execution Plan Collection

Capture:

```sql
SHOWPLAN_XML
```

or an equivalent SQL Server mechanism.

Persist:

```text
Baseline Plan XML
Optimized Plan XML
```

for every benchmark run.

---

# Plan Metadata

Capture:

```text
Plan Hash
Estimated Cost
Estimated Rows
Estimated Subtree Cost
```

where available.

---

# Operator Extraction

Identify and count operators appearing in the plan.

Examples:

```text
Index Seek

Index Scan

Table Scan

Key Lookup

Sort

Hash Match

Merge Join

Nested Loops
```

Store operator counts and occurrences.

No visualisation is required.

---

# Result Schema

Increment schema version:

```text
Version 2
→
Version 3
```

Maintain compatibility with prior result formats.

---

# New Models

Add:

```csharp
ExecutionPlanArtifact
{
    string PlanHash;
    string PlanXml;
    Dictionary<string,int> Operators;
}
```

---

# Result File Changes

Add plan information:

```json
{
  "baselinePlan": {
    "planHash": "",
    "operators": {}
  },
  "optimizedPlan": {
    "planHash": "",
    "operators": {}
  }
}
```

Raw plan XML may be stored separately if required.

Raw execution plans must never be discarded.

---

# Plan Comparison

Provide simple plan comparison output.

Example:

```text
Baseline

Index Scan

Optimized

Index Seek

Detected Change

Scan -> Seek
```

Only report observed differences.

Do not attempt interpretation.

---

# EXP001 Validation

Expected findings:

```text
Baseline Plan

Scan-Oriented Strategy

Optimized Plan

Seek-Oriented Strategy
```

Exact operators are not mandated.

The benchmark should demonstrate observable optimizer changes.

---

# Testing Requirements

Create automated tests for:

## Plan Capture

Verify XML plans are collected.

---

## Plan Parsing

Verify operators are extracted correctly.

---

## Plan Hashing

Verify identical plans produce identical hashes.

---

## Result Serialization

Verify execution plan data is persisted correctly.

---

## Backward Compatibility

Verify Version 1 and Version 2 result files remain readable.

---

# Documentation

Update:

```text
README.md
```

to include:

- Execution plan collection
- Operator extraction
- Sample benchmark results
- Sample plan comparison output

---

# Acceptance Test

Run:

```bash
dugout run EXP001
```

Expected:

✅ Validation passes

✅ Diagnostic metrics collected

✅ Plans collected

✅ Plan metadata collected

✅ Plan hashes generated

✅ Operator information extracted

✅ Result file persisted

✅ SchemaVersion = 3

✅ Tests pass

---

# Outcome

Milestone 03 is complete when Dugout can connect benchmark metrics to SQL Server optimizer behaviour through execution plan analysis.

The platform evolves from:

```text
SQL Diagnostics Platform
```

to:

```text
SQL Optimizer Analysis Platform
```