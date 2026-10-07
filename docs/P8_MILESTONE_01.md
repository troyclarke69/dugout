# Dugout - Milestone 08
## Advisor Engine

### Objective

Milestone 08 introduces an analysis layer that transforms benchmark results into findings and recommendations.

Previous milestones answer:

- What happened?
- Why did it happen?

Milestone 08 answers:

```text
What should I conclude?
```

The objective is to provide deterministic, evidence-based analysis using benchmark metrics, execution plans, historical trends, and regression data.

No AI is required for this milestone.

---

# Success Criteria

At completion of Milestone 08:

- Advisor rules execute automatically.
- Findings are generated from benchmark evidence.
- Recommendations are generated from benchmark evidence.
- Analysis is persisted to the repository.
- Existing functionality remains intact.
- Existing benchmarks remain compatible.
- All automated tests pass.

---

# Non-Goals

Do not implement:

- LLM Integration
- OpenAI Integration
- Claude Integration
- REST APIs
- Web UI
- Automatic Query Rewrites

These belong to future milestones.

---

# Primary Deliverable

Introduce:

```csharp
IAdvisorEngine
```

Responsible for:

- Benchmark analysis
- Trend analysis
- Regression analysis
- Execution plan analysis
- Recommendation generation

---

# Findings Engine

Generate findings from observed evidence.

Example:

```text
Finding

Logical Reads reduced by 80%.

Evidence

3229 -> 650 reads.
```

---

# Recommendation Engine

Generate deterministic recommendations.

Example:

```text
Recommendation

Performance improvement appears to be driven by reduced IO.
```

---

# Initial Rules

## Access Path Optimization

Conditions:

- Reads reduced > 50%
- Duration reduced > 30%
- Scan replaced by Seek

Output:

```text
Performance gain aligns with improved index access path.
```

---

## Regression

Conditions:

- Duration increased > 25%

Output:

```text
Performance regression detected.
```

---

## Plan Change

Conditions:

- Plan hash changed
- Duration increased

Output:

```text
Execution plan change correlates with observed regression.
```

---

## Resource Reduction

Conditions:

- CPU reduced
- Reads reduced

Output:

```text
Performance gain aligns with reduced resource utilization.
```

---

# Repository Changes

Add:

## AdvisorFindings

Store:

```text
RunId

Finding

Evidence

Severity
```

---

## AdvisorRecommendations

Store:

```text
RunId

Recommendation

Rule

Confidence
```

---

# Analysis Output

Example:

```text
Findings

✓ Reads reduced by 80%

✓ CPU reduced by 74%

✓ Index Scan replaced by Index Seek

Recommendations

✓ Improvement aligns with optimized index access.

✓ Resource consumption reduced significantly.
```

---

# Testing Requirements

Create tests for:

- Finding generation
- Recommendation generation
- Rule evaluation
- Repository persistence
- Historical analysis integration

---

# Documentation

Update:

README.md

Include:

- Findings
- Recommendations
- Advisor rules

---

# Acceptance Test

Run:

dugout run EXP001

Expected:

✅ Benchmark succeeds

✅ Findings generated

✅ Recommendations generated

✅ Analysis persisted

✅ Historical data evaluated

✅ Tests pass

---

# Outcome

Milestone 08 is complete when Dugout can produce evidence-based conclusions from benchmark results.

The platform evolves from:

Performance Analysis Platform

to

Performance Advisory Platform