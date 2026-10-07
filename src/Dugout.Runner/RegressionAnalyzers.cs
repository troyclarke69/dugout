using Dugout.Contracts;

namespace Dugout.Runner;

public interface IRegressionAnalyzer
{
    BenchmarkComparison Analyze(
        string experimentId,
        string runA,
        string runB,
        IReadOnlyDictionary<string, decimal?> referenceMetrics,
        IReadOnlyDictionary<string, decimal?> currentMetrics,
        RegressionThresholds thresholds);
}

public interface ITrendAnalyzer
{
    BenchmarkTrendReport Analyze(
        string experimentId,
        IEnumerable<BenchmarkHistoryRecord> history,
        RegressionThresholds thresholds);
}

public sealed class RegressionAnalyzer : IRegressionAnalyzer
{
    public BenchmarkComparison Analyze(
        string experimentId,
        string runA,
        string runB,
        IReadOnlyDictionary<string, decimal?> referenceMetrics,
        IReadOnlyDictionary<string, decimal?> currentMetrics,
        RegressionThresholds thresholds)
    {
        thresholds.Validate();
        var comparison = new BenchmarkComparison
        {
            ExperimentId = experimentId,
            RunA = runA,
            RunB = runB,
            ComparedUtc = DateTimeOffset.UtcNow
        };

        foreach (var (metric, reference) in referenceMetrics)
        {
            currentMetrics.TryGetValue(metric, out var current);
            var changePercent = CalculateRegressionPercent(reference, current);
            var classification = Classify(changePercent, thresholds);
            comparison.Metrics.Add(new BenchmarkComparisonMetric
            {
                Metric = metric,
                ReferenceValue = reference,
                CurrentValue = current,
                ChangePercent = changePercent,
                Classification = classification
            });
        }

        comparison.RegressionDetected = comparison.Metrics.Any(metric => metric.Classification is "Minor" or "Moderate" or "Severe");
        comparison.Health = comparison.Metrics.Any(metric => metric.Classification is "Moderate" or "Severe")
            ? "Regression"
            : comparison.Metrics.Any(metric => metric.Classification == "Minor") ? "Warning" : "Healthy";
        return comparison;
    }

    internal static decimal? CalculateRegressionPercent(decimal? reference, decimal? current)
    {
        if (reference is null || current is null)
        {
            return null;
        }

        if (reference == 0m)
        {
            return current == 0m ? 0m : null;
        }

        return (current.Value - reference.Value) / Math.Abs(reference.Value) * 100m;
    }

    internal static string Classify(decimal? changePercent, RegressionThresholds thresholds)
    {
        if (changePercent is null || changePercent <= thresholds.MinorPercent)
        {
            return "Healthy";
        }

        if (changePercent > thresholds.SeverePercent)
        {
            return "Severe";
        }

        if (changePercent > thresholds.ModeratePercent)
        {
            return "Moderate";
        }

        return "Minor";
    }
}

public sealed class TrendAnalyzer : ITrendAnalyzer
{
    public BenchmarkTrendReport Analyze(
        string experimentId,
        IEnumerable<BenchmarkHistoryRecord> history,
        RegressionThresholds thresholds)
    {
        thresholds.Validate();
        var runs = history
            .Where(run => string.Equals(run.Status, "Success", StringComparison.OrdinalIgnoreCase))
            .OrderBy(run => run.StartedUtc)
            .ToList();
        var report = new BenchmarkTrendReport { ExperimentId = experimentId };
        AddTrend(report, "Duration (ms)", runs.Select(run => run.OptimizedDurationMedianMs), thresholds);
        AddTrend(report, "Logical reads", runs.Select(run => run.OptimizedLogicalReadsMedian), thresholds);
        AddTrend(report, "CPU time (ms)", runs.Select(run => run.OptimizedCpuTimeMedianMs), thresholds);
        AddTrend(report, "SQL elapsed time (ms)", runs.Select(run => run.OptimizedSqlElapsedTimeMedianMs), thresholds);
        return report;
    }

    private static void AddTrend(
        BenchmarkTrendReport report,
        string metric,
        IEnumerable<decimal?> samples,
        RegressionThresholds thresholds)
    {
        var values = samples.Where(value => value.HasValue).Select(value => value!.Value).ToList();
        if (values.Count < 2)
        {
            return;
        }

        var changePercent = RegressionAnalyzer.CalculateRegressionPercent(values[0], values[^1]);
        var direction = changePercent is null
            ? "Unavailable"
            : changePercent > thresholds.MinorPercent ? "Increasing"
            : changePercent < -thresholds.MinorPercent ? "Decreasing"
            : "Stable";
        report.Trends.Add(new BenchmarkTrend
        {
            Metric = metric,
            Direction = direction,
            SampleCount = values.Count,
            FirstValue = values[0],
            LatestValue = values[^1],
            ChangePercent = changePercent
        });
    }
}

public static class BenchmarkMetricValues
{
    public static Dictionary<string, decimal?> FromResult(BenchmarkResultDocument result)
    {
        return FromSummary(result.Optimized);
    }

    public static Dictionary<string, decimal?> FromSummary(BenchmarkRunsSummary summary)
    {
        return new Dictionary<string, decimal?>(StringComparer.Ordinal)
        {
            ["Duration (ms)"] = MedianOrRaw(summary.Statistics, summary.Runs.Select(run => (decimal?)run.DurationMs)),
            ["Logical reads"] = MedianOrRaw(summary.LogicalReadsStatistics, summary.Runs.Select(run => run.LogicalReads is null ? null : (decimal?)run.LogicalReads.Value)),
            ["CPU time (ms)"] = MedianOrRaw(summary.CpuTimeMsStatistics, summary.Runs.Select(run => run.CpuTimeMs)),
            ["SQL elapsed time (ms)"] = MedianOrRaw(summary.SqlElapsedTimeMsStatistics, summary.Runs.Select(run => run.SqlElapsedTimeMs))
        };
    }

    private static bool HasSamples(BenchmarkStatistics statistics) => statistics.SampleCount > 0;

    private static decimal? MedianOrRaw(BenchmarkStatistics statistics, IEnumerable<decimal?> rawValues)
    {
        if (HasSamples(statistics))
        {
            return statistics.Median;
        }

        var values = rawValues.Where(value => value.HasValue).Select(value => value!.Value).ToList();
        return values.Count == 0 ? null : BenchmarkStatisticsCalculator.CalculateMedian(values);
    }
}
