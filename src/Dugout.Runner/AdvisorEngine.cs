using System.Globalization;
using Dugout.Contracts;

namespace Dugout.Runner;

public interface IAdvisorEngine
{
    BenchmarkAdvisorAnalysis Analyze(BenchmarkResultDocument run, IReadOnlyList<BenchmarkHistoryRecord> history);
}

public sealed class AdvisorEngine : IAdvisorEngine
{
    private readonly ITrendAnalyzer _trendAnalyzer;
    private readonly RegressionThresholds _thresholds;

    public AdvisorEngine(ITrendAnalyzer? trendAnalyzer = null, RegressionThresholds? thresholds = null)
    {
        _trendAnalyzer = trendAnalyzer ?? new TrendAnalyzer();
        _thresholds = thresholds ?? new RegressionThresholds();
        _thresholds.Validate();
    }

    public BenchmarkAdvisorAnalysis Analyze(BenchmarkResultDocument run, IReadOnlyList<BenchmarkHistoryRecord> history)
    {
        var analysis = new BenchmarkAdvisorAnalysis();
        if (!string.Equals(run.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            return analysis;
        }

        AnalyzeAccessPath(run, analysis);
        AnalyzeResourceReduction(run, analysis);

        var previousRun = history
            .Where(item => item.RunId != run.RunId && item.ExperimentId == run.ExperimentId && string.Equals(item.Status, "Success", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.StartedUtc)
            .FirstOrDefault();
        if (previousRun is not null)
        {
            AnalyzeHistoricalRegression(run, previousRun, analysis);
            AnalyzePlanChange(run, previousRun, analysis);
        }

        AnalyzeTrends(run, history, analysis);
        return analysis;
    }

    private static void AnalyzeAccessPath(BenchmarkResultDocument run, BenchmarkAdvisorAnalysis analysis)
    {
        var readReduction = GetImprovement(run.Baseline.LogicalReadsStatistics, run.Baseline.Runs.Select(item => item.LogicalReads is null ? null : (decimal?)item.LogicalReads.Value),
            run.Optimized.LogicalReadsStatistics, run.Optimized.Runs.Select(item => item.LogicalReads is null ? null : (decimal?)item.LogicalReads.Value));
        var durationReduction = GetImprovement(run.Baseline.Statistics, run.Baseline.Runs.Select(item => (decimal?)item.DurationMs),
            run.Optimized.Statistics, run.Optimized.Runs.Select(item => (decimal?)item.DurationMs));

        if (readReduction is not > 50m || durationReduction is not > 30m || !ReplacedScanWithSeek(run.BaselinePlan, run.OptimizedPlan))
        {
            return;
        }

        analysis.Findings.Add(new AdvisorFinding
        {
            Rule = "AccessPathOptimization",
            Finding = $"Logical reads reduced by {FormatPercent(readReduction.Value)}.",
            Evidence = $"{FormatMetric(run.Baseline.LogicalReadsStatistics, run.Baseline.Runs.Select(item => item.LogicalReads is null ? null : (decimal?)item.LogicalReads.Value))} -> {FormatMetric(run.Optimized.LogicalReadsStatistics, run.Optimized.Runs.Select(item => item.LogicalReads is null ? null : (decimal?)item.LogicalReads.Value))} reads; duration improved by {FormatPercent(durationReduction.Value)}; scan operator replaced by seek.",
            Severity = "Info"
        });
        analysis.Recommendations.Add(new AdvisorRecommendation
        {
            Rule = "AccessPathOptimization",
            Recommendation = "Performance gain aligns with improved index access path.",
            Confidence = 0.95m
        });
    }

    private static void AnalyzeResourceReduction(BenchmarkResultDocument run, BenchmarkAdvisorAnalysis analysis)
    {
        var readReduction = GetImprovement(run.Baseline.LogicalReadsStatistics, run.Baseline.Runs.Select(item => item.LogicalReads is null ? null : (decimal?)item.LogicalReads.Value),
            run.Optimized.LogicalReadsStatistics, run.Optimized.Runs.Select(item => item.LogicalReads is null ? null : (decimal?)item.LogicalReads.Value));
        var cpuReduction = GetImprovement(run.Baseline.CpuTimeMsStatistics, run.Baseline.Runs.Select(item => item.CpuTimeMs),
            run.Optimized.CpuTimeMsStatistics, run.Optimized.Runs.Select(item => item.CpuTimeMs));

        if (readReduction is not > 0m || cpuReduction is not > 0m)
        {
            return;
        }

        analysis.Findings.Add(new AdvisorFinding
        {
            Rule = "ResourceReduction",
            Finding = "CPU time and logical reads both decreased.",
            Evidence = $"CPU time improved by {FormatPercent(cpuReduction.Value)}; logical reads improved by {FormatPercent(readReduction.Value)}.",
            Severity = "Info"
        });
        analysis.Recommendations.Add(new AdvisorRecommendation
        {
            Rule = "ResourceReduction",
            Recommendation = "Performance gain aligns with reduced resource utilization.",
            Confidence = 0.9m
        });
    }

    private void AnalyzeHistoricalRegression(BenchmarkResultDocument run, BenchmarkHistoryRecord previous, BenchmarkAdvisorAnalysis analysis)
    {
        var change = RegressionAnalyzer.CalculateRegressionPercent(previous.OptimizedDurationMedianMs, MedianOrNull(run.Optimized.Statistics, run.Optimized.Runs.Select(item => (decimal?)item.DurationMs)));
        if (change is not > 25m)
        {
            return;
        }

        var severity = RegressionAnalyzer.Classify(change, _thresholds);
        analysis.Findings.Add(new AdvisorFinding
        {
            Rule = "Regression",
            Finding = "Performance regression detected.",
            Evidence = $"Optimized duration increased from {FormatValue(previous.OptimizedDurationMedianMs)} ms in {previous.RunId} to {FormatValue(MedianOrNull(run.Optimized.Statistics, run.Optimized.Runs.Select(item => (decimal?)item.DurationMs)))} ms ({FormatPercent(change.Value)}).",
            Severity = severity
        });
        analysis.Recommendations.Add(new AdvisorRecommendation
        {
            Rule = "Regression",
            Recommendation = "Investigate query, data, and environment changes since the previous successful run.",
            Confidence = 0.9m
        });
    }

    private void AnalyzePlanChange(BenchmarkResultDocument run, BenchmarkHistoryRecord previous, BenchmarkAdvisorAnalysis analysis)
    {
        var currentDuration = MedianOrNull(run.Optimized.Statistics, run.Optimized.Runs.Select(item => (decimal?)item.DurationMs));
        if (previous.OptimizedDurationMedianMs is not decimal previousDuration
            || currentDuration is not decimal duration
            || duration <= previousDuration
            || string.IsNullOrWhiteSpace(previous.OptimizedPlanHash)
            || string.IsNullOrWhiteSpace(run.OptimizedPlan?.PlanHash)
            || string.Equals(previous.OptimizedPlanHash, run.OptimizedPlan.PlanHash, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var increase = RegressionAnalyzer.CalculateRegressionPercent(previousDuration, duration);
        analysis.Findings.Add(new AdvisorFinding
        {
            Rule = "PlanChange",
            Finding = "Execution plan change correlates with the observed duration increase.",
            Evidence = $"Plan hash {ShortHash(previous.OptimizedPlanHash)} -> {ShortHash(run.OptimizedPlan.PlanHash)}; duration {FormatValue(previousDuration)} -> {FormatValue(duration)} ms ({FormatPercent(increase)}).",
            Severity = increase > 25m ? RegressionAnalyzer.Classify(increase, _thresholds) : "Warning"
        });
        analysis.Recommendations.Add(new AdvisorRecommendation
        {
            Rule = "PlanChange",
            Recommendation = "Inspect the changed execution plan and its access path for the source of the slowdown.",
            Confidence = 0.8m
        });
    }

    private void AnalyzeTrends(BenchmarkResultDocument run, IReadOnlyList<BenchmarkHistoryRecord> history, BenchmarkAdvisorAnalysis analysis)
    {
        var trendHistory = history
            .Where(item => item.RunId != run.RunId)
            .Append(new BenchmarkHistoryRecord
            {
                RunId = run.RunId,
                ExperimentId = run.ExperimentId,
                Status = run.Status,
                StartedUtc = run.StartedUtc,
                OptimizedDurationMedianMs = MedianOrNull(run.Optimized.Statistics, run.Optimized.Runs.Select(item => (decimal?)item.DurationMs)),
                OptimizedLogicalReadsMedian = MedianOrNull(run.Optimized.LogicalReadsStatistics, run.Optimized.Runs.Select(item => item.LogicalReads is null ? null : (decimal?)item.LogicalReads.Value)),
                OptimizedCpuTimeMedianMs = MedianOrNull(run.Optimized.CpuTimeMsStatistics, run.Optimized.Runs.Select(item => item.CpuTimeMs)),
                OptimizedSqlElapsedTimeMedianMs = MedianOrNull(run.Optimized.SqlElapsedTimeMsStatistics, run.Optimized.Runs.Select(item => item.SqlElapsedTimeMs)),
                OptimizedPlanHash = run.OptimizedPlan?.PlanHash
            });

        var trends = _trendAnalyzer.Analyze(run.ExperimentId, trendHistory.ToList(), _thresholds);
        foreach (var trend in trends.Trends.Where(item => item.Direction == "Increasing" && item.ChangePercent > _thresholds.MinorPercent))
        {
            analysis.Findings.Add(new AdvisorFinding
            {
                Rule = "HistoricalTrend",
                Finding = $"Historical {trend.Metric.ToLowerInvariant()} trend is increasing.",
                Evidence = $"{FormatValue(trend.FirstValue)} -> {FormatValue(trend.LatestValue)} across {trend.SampleCount} successful runs ({FormatPercent(trend.ChangePercent)}).",
                Severity = trend.ChangePercent > _thresholds.ModeratePercent ? "Warning" : "Info"
            });
            analysis.Recommendations.Add(new AdvisorRecommendation
            {
                Rule = "HistoricalTrend",
                Recommendation = $"Review recent runs for gradual {trend.Metric.ToLowerInvariant()} drift.",
                Confidence = 0.75m
            });
        }
    }

    private static bool ReplacedScanWithSeek(ExecutionPlanArtifact? baseline, ExecutionPlanArtifact? optimized)
    {
        if (baseline is null || optimized is null)
        {
            return false;
        }

        var removedScan = baseline.Operators.Any(item => item.Value > 0
            && item.Key.Contains("Scan", StringComparison.OrdinalIgnoreCase)
            && (!optimized.Operators.TryGetValue(item.Key, out var optimizedCount) || optimizedCount == 0));
        var addedSeek = optimized.Operators.Any(item => item.Value > 0
            && item.Key.Contains("Seek", StringComparison.OrdinalIgnoreCase)
            && (!baseline.Operators.TryGetValue(item.Key, out var baselineCount) || baselineCount == 0));
        return removedScan && addedSeek;
    }

    private static decimal? GetImprovement(
        BenchmarkStatistics baselineStatistics,
        IEnumerable<decimal?> baselineRaw,
        BenchmarkStatistics optimizedStatistics,
        IEnumerable<decimal?> optimizedRaw)
    {
        var baseline = MedianOrNull(baselineStatistics, baselineRaw);
        var optimized = MedianOrNull(optimizedStatistics, optimizedRaw);
        var regression = RegressionAnalyzer.CalculateRegressionPercent(baseline, optimized);
        return regression is null ? null : -regression.Value;
    }

    private static decimal? MedianOrNull(BenchmarkStatistics statistics, IEnumerable<decimal?> rawValues)
    {
        if (statistics.SampleCount > 0)
        {
            return statistics.Median;
        }

        var values = rawValues.Where(item => item.HasValue).Select(item => item!.Value).ToList();
        return values.Count == 0 ? null : BenchmarkStatisticsCalculator.CalculateMedian(values);
    }

    private static string FormatMetric(BenchmarkStatistics statistics, IEnumerable<decimal?> rawValues)
    {
        return FormatValue(MedianOrNull(statistics, rawValues));
    }

    private static string FormatValue(decimal? value) => value?.ToString("N1", CultureInfo.InvariantCulture) ?? "n/a";
    private static string FormatPercent(decimal? value) => value is null ? "n/a" : $"{value.Value.ToString("F1", CultureInfo.InvariantCulture)}%";
    private static string ShortHash(string hash) => hash[..Math.Min(hash.Length, 12)];
}
