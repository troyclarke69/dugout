export type Experiment = {
  id: string;
  name: string;
  description: string | null;
  category: string;
  difficulty: string;
  tags: string[];
  learningObjectives: string[];
};

export type RunSummary = {
  runId: string;
  experimentId: string;
  experimentName: string;
  status: string;
  startedUtc: string;
  completedUtc: string;
  optimizedDurationMedianMs: number | null;
  optimizedLogicalReadsMedian: number | null;
  optimizedCpuTimeMedianMs: number | null;
  optimizedSqlElapsedTimeMedianMs: number | null;
  sqlServerVersion: string;
  machineName: string;
  baselinePlanHash: string | null;
  optimizedPlanHash: string | null;
};

export type MetricSummary = {
  sampleCount: number;
  durationMedianMs: number | null;
  logicalReadsMedian: number | null;
  physicalReadsMedian: number | null;
  cpuTimeMedianMs: number | null;
  sqlElapsedTimeMedianMs: number | null;
  requestedMemoryMedianKb: number | null;
  grantedMemoryMedianKb: number | null;
  usedMemoryMedianKb: number | null;
  tempDbPagesMedian: number | null;
  worktableLogicalReadsMedian: number | null;
  degreeOfParallelismMedian: number | null;
  parallelPlanCount: number;
  parallelPlanSampleCount: number;
};

export type AdvisorFinding = {
  rule: string;
  finding: string;
  evidence: string;
  severity: string;
};

export type AdvisorRecommendation = {
  rule: string;
  recommendation: string;
  confidence: number;
};

export type AdvisorAnalysisResponse = {
  runId: string;
  findings: AdvisorFinding[];
  recommendations: AdvisorRecommendation[];
};

export type RunDetail = {
  runId: string;
  experimentId: string;
  experimentHash: string;
  status: string;
  exitCode: number;
  startedUtc: string;
  completedUtc: string;
  baseline: MetricSummary;
  optimized: MetricSummary;
  validationPassed: boolean;
  validationDetails: string;
  failureReason: string | null;
  sqlServerVersion: string;
  sqlServerEdition: string;
  machineName: string;
  findings: AdvisorFinding[];
  recommendations: AdvisorRecommendation[];
};

export type Baseline = {
  experimentId: string;
  runId: string;
  establishedUtc: string;
  metrics: Record<string, number | null>;
};

export type MetricComparison = {
  metric: string;
  previousMedian: number;
  currentMedian: number;
  improvementPercent: number | null;
};

export type PlanComparison = {
  planHashesMatch: boolean;
  operatorChanges: Array<{
    physicalOperator: string;
    previousCount: number;
    currentCount: number;
  }>;
};

export type RunComparison = {
  previousRunId: string;
  currentRunId: string;
  baselineMetrics: MetricComparison[];
  optimizedMetrics: MetricComparison[];
  baselinePlan: PlanComparison | null;
  optimizedPlan: PlanComparison | null;
};

export type ExecutionPlan = {
  planHash: string;
  estimatedCost: number | null;
  estimatedRows: number | null;
  estimatedSubtreeCost: number | null;
  operators: Record<string, number>;
  planXml: string;
};

export type ExecutionPlans = {
  runId: string;
  baseline: ExecutionPlan | null;
  optimized: ExecutionPlan | null;
};

export type TrendMetric = "duration" | "reads" | "cpu";
