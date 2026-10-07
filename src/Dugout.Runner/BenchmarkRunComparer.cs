using Dugout.Contracts;

namespace Dugout.Runner;

public static class BenchmarkRunComparer
{
    public static BenchmarkRunComparison Compare(BenchmarkResultDocument previous, BenchmarkResultDocument current)
    {
        return new BenchmarkRunComparison
        {
            PreviousRunId = previous.RunId,
            CurrentRunId = current.RunId,
            BaselineMetrics = CompareMetrics(previous.Baseline, current.Baseline),
            OptimizedMetrics = CompareMetrics(previous.Optimized, current.Optimized),
            BaselinePlanComparison = ComparePlans(previous.BaselinePlan, current.BaselinePlan),
            OptimizedPlanComparison = ComparePlans(previous.OptimizedPlan, current.OptimizedPlan)
        };
    }

    private static List<BenchmarkMetricComparison> CompareMetrics(BenchmarkRunsSummary previous, BenchmarkRunsSummary current)
    {
        return new List<BenchmarkMetricComparison>
        {
            CreateMetric("Duration (ms)", previous.Statistics.Median, current.Statistics.Median),
            CreateMetric("Logical reads", previous.LogicalReadsStatistics.Median, current.LogicalReadsStatistics.Median),
            CreateMetric("Physical reads", previous.PhysicalReadsStatistics.Median, current.PhysicalReadsStatistics.Median),
            CreateMetric("CPU time (ms)", previous.CpuTimeMsStatistics.Median, current.CpuTimeMsStatistics.Median),
            CreateMetric("SQL elapsed time (ms)", previous.SqlElapsedTimeMsStatistics.Median, current.SqlElapsedTimeMsStatistics.Median)
        };
    }

    private static BenchmarkMetricComparison CreateMetric(string name, decimal previous, decimal current)
    {
        return new BenchmarkMetricComparison
        {
            Metric = name,
            PreviousMedian = previous,
            CurrentMedian = current,
            ImprovementPercent = previous == 0m
                ? null
                : BenchmarkStatisticsCalculator.CalculateImprovement(previous, current)
        };
    }

    private static ExecutionPlanComparison? ComparePlans(ExecutionPlanArtifact? previous, ExecutionPlanArtifact? current)
    {
        return previous is null || current is null ? null : ExecutionPlanComparer.Compare(previous, current);
    }
}