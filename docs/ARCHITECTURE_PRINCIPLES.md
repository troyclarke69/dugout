# Dugout Architecture Principles

## Core Principles

### 1. Benchmark Accuracy Over Convenience

The primary objective is producing trustworthy benchmark results.

Developer convenience should never compromise benchmark validity.

---

### 2. Repeatability Over Speed

A slower benchmark with consistent results is preferred over a faster benchmark with inconsistent results.

---

### 3. Measured Results Over Assumptions

Every performance claim must be supported by collected metrics.

Benchmark conclusions should not rely on intuition.

---

### 4. SQL Server Is The Subject

Dugout exists to explore and measure SQL Server performance characteristics.

The runner should minimise non-database interference with collected results.

---

### 5. The Benchmark Runner Is The Product

The runner is the primary asset.

Future APIs, dashboards, UIs, and AI features are consumers of the benchmark engine.

Development priorities should favour benchmark quality over feature development.

---

## Experiment Principles

### 6. Every Experiment Must Be Reproducible

A benchmark must operate from a known and controlled database state.

---

### 7. No Hidden State

Experiments must not depend on undocumented setup, manual intervention, or prior executions.

---

### 8. Experiments Must Be Self-Contained

Every experiment should define:

- SetupScript
- ValidationScript
- BaselineScript
- OptimizedScript
- CleanupScript

---

### 9. Raw Data Must Be Preserved

Whenever practical, store original benchmark data in addition to calculated summaries.

Future analysis should not require rerunning historical experiments.

---

## Engineering Principles

### 10. Simplicity Before Extensibility

Do not build abstractions for hypothetical requirements.

Implement only what the current milestone requires.

---

### 11. Statistical Validity Matters

Benchmarks must:

- Warm up
- Execute multiple iterations
- Record minimum values
- Record maximum values
- Record average values
- Record median values

Single execution timings must never be used as final benchmark results.

---

### 12. Observability Is A Feature

Benchmark execution should be transparent.

At any time it should be possible to determine:

- What ran
- When it ran
- How it ran
- What was measured
- What results were produced