import { expect, test, type Page, type Route } from "@playwright/test";

const experiments = [
  { id: "EXP001", name: "Date predicate", description: "Compare date access paths.", category: "SARGability", difficulty: "Beginner", tags: ["Date", "Predicate"], learningObjectives: ["Compare scans and seeks"] },
  { id: "EXP002", name: "Missing lookup index", description: "Measure lookup cost.", category: "Indexing", difficulty: "Intermediate", tags: ["Lookup", "Index"], learningObjectives: ["Inspect logical reads"] },
];

const runs = [
  { runId: "RUN0002", experimentId: "EXP002", experimentName: "Missing lookup index", status: "Success", startedUtc: "2026-10-02T12:00:00Z", completedUtc: "2026-10-02T12:01:00Z", optimizedDurationMedianMs: 120, optimizedLogicalReadsMedian: 240, optimizedCpuTimeMedianMs: 24, optimizedSqlElapsedTimeMedianMs: 32, sqlServerVersion: "SQL Server 2022", machineName: "BENCH-01", baselinePlanHash: "basehash2", optimizedPlanHash: "opthash2" },
  { runId: "RUN0001", experimentId: "EXP002", experimentName: "Missing lookup index", status: "Success", startedUtc: "2026-10-01T12:00:00Z", completedUtc: "2026-10-01T12:01:00Z", optimizedDurationMedianMs: 100, optimizedLogicalReadsMedian: 200, optimizedCpuTimeMedianMs: 20, optimizedSqlElapsedTimeMedianMs: 28, sqlServerVersion: "SQL Server 2022", machineName: "BENCH-01", baselinePlanHash: "basehash1", optimizedPlanHash: "opthash1" },
];

const plan = {
  planHash: "a9d3c4e5f6071829",
  estimatedCost: 3.25,
  estimatedRows: 120,
  estimatedSubtreeCost: 3.25,
  operators: { "Index Seek": 1, "Key Lookup": 2 },
  planXml: "<ShowplanXML><RelOp PhysicalOp=\"Index Seek\" /></ShowplanXML>",
};

const detail = {
  runId: "RUN0002", experimentId: "EXP002", experimentHash: "hash-2", status: "Success", exitCode: 0,
  startedUtc: runs[0].startedUtc, completedUtc: runs[0].completedUtc,
  baseline: metricSummary(150, 400, 45), optimized: metricSummary(120, 240, 24),
  validationPassed: true, validationDetails: "Baseline and optimized results matched.", failureReason: null,
  sqlServerVersion: "SQL Server 2022", sqlServerEdition: "Developer", machineName: "BENCH-01",
  findings: [{ rule: "Regression", finding: "Duration increased across the last two runs.", evidence: "100 ms -> 120 ms (+20%).", severity: "Warning" }],
  recommendations: [{ rule: "Regression", recommendation: "Review recent query and environment changes.", confidence: 0.9 }],
};

async function mockApi(page: Page) {
  await page.route("**/api/**", async (route: Route) => {
    const path = new URL(route.request().url()).pathname;
    if (path === "/api/experiments") return route.fulfill({ json: experiments });
    if (path === "/api/runs") return route.fulfill({ json: runs });
    if (path === "/api/history/EXP002") return route.fulfill({ json: runs });
    if (path === "/api/baseline/EXP002") return route.fulfill({ json: { experimentId: "EXP002", runId: "RUN0001", establishedUtc: runs[1].startedUtc, metrics: { "Duration (ms)": 100, "Logical reads": 200, "CPU time (ms)": 20 } } });
    if (path === "/api/compare/RUN0002/RUN0001" || path === "/api/compare/RUN0001/RUN0002") {
      return route.fulfill({ json: { previousRunId: "RUN0001", currentRunId: "RUN0002", baselineMetrics: [], optimizedMetrics: [{ metric: "Duration (ms)", previousMedian: 100, currentMedian: 120, improvementPercent: -20 }, { metric: "Logical reads", previousMedian: 200, currentMedian: 240, improvementPercent: -20 }, { metric: "CPU time (ms)", previousMedian: 20, currentMedian: 24, improvementPercent: -20 }], baselinePlan: { planHashesMatch: false, operatorChanges: [{ physicalOperator: "Clustered Index Scan", previousCount: 1, currentCount: 0 }] }, optimizedPlan: { planHashesMatch: false, operatorChanges: [{ physicalOperator: "Key Lookup", previousCount: 0, currentCount: 2 }] } } });
    }
    if (path.endsWith("/analysis")) return route.fulfill({ json: { runId: "RUN0002", findings: detail.findings, recommendations: detail.recommendations } });
    if (path.endsWith("/plans")) return route.fulfill({ json: { runId: "RUN0002", baseline: plan, optimized: plan } });
    if (path === "/api/runs/RUN0001" || path === "/api/runs/RUN0002") return route.fulfill({ json: detail });
    if (path.startsWith("/api/runs/")) return route.fulfill({ json: detail });
    return route.fulfill({ status: 404, json: { title: "Not Found" } });
  });
}

function metricSummary(duration: number, reads: number, cpu: number) {
  return {
    sampleCount: 10, durationMedianMs: duration, logicalReadsMedian: reads, physicalReadsMedian: 2,
    cpuTimeMedianMs: cpu, sqlElapsedTimeMedianMs: cpu + 8, requestedMemoryMedianKb: 512,
    grantedMemoryMedianKb: 384, usedMemoryMedianKb: 96, tempDbPagesMedian: 2,
    worktableLogicalReadsMedian: 0, degreeOfParallelismMedian: 1, parallelPlanCount: 0, parallelPlanSampleCount: 10,
  };
}

test.beforeEach(async ({ page }) => {
  await mockApi(page);
  await page.goto("/");
});

test("experiment search and filters narrow the library", async ({ page }) => {
  await page.getByRole("button", { name: "Experiments" }).click();
  await page.getByRole("textbox", { name: "Search experiments and runs" }).fill("Lookup");
  await expect(page.locator(".experiment-card")).toHaveCount(1);
  await expect(page.getByRole("heading", { name: "Missing lookup index" })).toBeVisible();
  await page.getByLabel("Filter by difficulty").selectOption("Beginner");
  await expect(page.locator(".experiment-card")).toHaveCount(0);
});

test("global search finds tags, categories, experiments, and runs", async ({ page }) => {
  const search = page.getByRole("textbox", { name: "Search experiments and runs" });
  await search.fill("Lookup");
  await expect(page.locator(".search-result-section").first().getByText("Missing lookup index")).toBeVisible();
  await page.locator(".search-result-section").first().getByRole("button", { name: /EXP002/ }).click();
  await expect(page.locator(".experiment-card")).toHaveCount(1);

  await page.getByRole("button", { name: "Overview" }).click();
  await search.fill("RUN0002");
  await page.getByRole("region", { name: "Search results" }).getByRole("button", { name: /RUN0002/ }).click();
  await expect(page.getByRole("heading", { name: "Benchmark runs" })).toBeVisible();
});

test("mobile search and navigation stay within the viewport", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  await page.getByRole("textbox", { name: "Search experiments and runs" }).fill("Lookup");
  const bounds = await page.locator(".search-results").boundingBox();
  const documentWidth = await page.locator("html").evaluate((element) => element.clientWidth);

  expect(bounds).not.toBeNull();
  expect(bounds!.x).toBeGreaterThanOrEqual(0);
  expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(documentWidth);
  await expect(page.getByRole("button", { name: "Overview" })).toBeVisible();
});

test("historical trends render successful run data and baseline", async ({ page }) => {
  await page.getByRole("button", { name: "Historical trends" }).click();
  await page.getByLabel("Choose experiment for trends").selectOption("EXP002");
  await expect(page.locator(".trend-line")).toBeVisible();
  await expect(page.getByText("Baseline RUN0001")).toBeVisible();
  await page.getByRole("tab", { name: "Logical reads" }).click();
  await expect(page.locator("svg[aria-label^='reads trend']")).toBeVisible();
});

test("comparison displays metric and execution plan differences", async ({ page }) => {
  await page.getByRole("button", { name: "Compare runs" }).click();
  await expect(page.locator(".comparison-card")).toHaveCount(3);
  await expect(page.getByText("Plan changed").first()).toBeVisible();
  await expect(page.getByText("Key Lookup").first()).toBeVisible();
});

test("advisor findings show evidence and recommendations", async ({ page }) => {
  await page.locator(".sidebar").getByRole("button", { name: "Advisor" }).click();
  await expect(page.getByText("Duration increased across the last two runs.")).toBeVisible();
  await expect(page.getByText("100 ms -> 120 ms (+20%).")).toBeVisible();
  await expect(page.getByText("Review recent query and environment changes.")).toBeVisible();
});

test("execution plans reveal operator summary and raw XML", async ({ page }) => {
  await page.getByRole("button", { name: "Execution plans" }).click();
  await expect(page.getByText("Key Lookup", { exact: true }).first()).toBeVisible();
  await page.locator(".xml-disclosure summary").first().click();
  await expect(page.locator(".xml-disclosure pre").first()).toContainText("Index Seek");
});
