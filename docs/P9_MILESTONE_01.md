# Dugout - Milestone 09
## Public API

### Objective

Milestone 09 exposes Dugout functionality through a public REST API.

The API becomes the primary integration layer for dashboards, automation, CI/CD pipelines, and future AI capabilities.

---

# Success Criteria

At completion of Milestone 09:

- REST API is implemented.
- Repository data is exposed.
- Benchmark execution is available via HTTP.
- OpenAPI documentation is generated.
- Existing CLI functionality remains intact.
- All tests pass.

---

# Non-Goals

Do not implement:

- Authentication
- Authorization
- Multi-Tenancy
- GraphQL
- Web UI
- Cloud Deployment

---

# Primary Deliverable

Create:

```text
Dugout.Api
```

Using:

```text
ASP.NET Core Minimal APIs
```

---

# Experiment Endpoints

## List Experiments

```http
GET /api/experiments
```

---

## Get Experiment

```http
GET /api/experiments/{id}
```

---

# Benchmark Endpoints

## Run Experiment

```http
POST /api/experiments/{id}/run
```

---

## List Runs

```http
GET /api/runs
```

---

## Get Run

```http
GET /api/runs/{id}
```

---

# Historical Endpoints

## History

```http
GET /api/history/{experimentId}
```

---

## Baseline

```http
GET /api/baseline/{experimentId}
```

---

## Compare Runs

```http
GET /api/compare/{runA}/{runB}
```

---

# Advisor Endpoints

## Analysis

```http
GET /api/runs/{id}/analysis
```

---

# Plan Endpoints

## Execution Plans

```http
GET /api/runs/{id}/plans
```

---

# OpenAPI

Generate:

```text
Swagger

OpenAPI Specification
```

for all endpoints.

---

# Testing Requirements

Create tests for:

- API endpoints
- Run execution APIs
- Repository APIs
- Comparison APIs
- Advisor APIs

---

# Documentation

Update:

README.md

Include:

- API setup
- Endpoint documentation
- Swagger access

---

# Acceptance Test

Expected:

✅ API starts successfully

✅ OpenAPI generated

✅ Experiments accessible

✅ Runs accessible

✅ Comparisons accessible

✅ Advisor results accessible

✅ Tests pass

---

# Outcome

Milestone 09 is complete when Dugout functionality is fully accessible through a documented HTTP API.

The platform evolves from:

Performance Advisory Platform

to

Performance Engineering Service Platform