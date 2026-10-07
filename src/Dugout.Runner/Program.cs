using Dugout.Contracts;
using Dugout.Storage;
using Microsoft.Data.SqlClient;

namespace Dugout.Runner;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellationSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            return args[0] switch
            {
                "list" => await ListExperimentsAsync(args),
                "run" => await RunExperimentAsync(args, cancellationSource.Token),
                "baseline" => await EstablishBaselineAsync(args, cancellationSource.Token),
                "history" => await ShowHistoryAsync(args, cancellationSource.Token),
                "compare" => await CompareRunsAsync(args, cancellationSource.Token),
                "resources" => await ShowResourcesAsync(args, cancellationSource.Token),
                _ => ShowUsageAndReturnFailure()
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 3;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static int ShowUsageAndReturnFailure()
    {
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dugout list");
        Console.WriteLine("  dugout run EXP001");
        Console.WriteLine("  dugout run EXP001 --warmups 5 --iterations 25");
        Console.WriteLine("  dugout baseline EXP001");
        Console.WriteLine("  dugout history EXP001 --latest 10");
        Console.WriteLine("  dugout history EXP001 --sql-version <version> --environment <machine>");
        Console.WriteLine("  dugout compare <run1> <run2> [--minor-threshold 10 --moderate-threshold 25 --severe-threshold 50]");
        Console.WriteLine("  dugout compare baseline latest [EXP001]");
        Console.WriteLine("  dugout resources EXP001 --latest 100");
        Console.WriteLine("  dugout list --category Indexing --difficulty Intermediate --tag Lookup");
    }

    private static async Task<int> ListExperimentsAsync(string[] args)
    {
        var category = GetStringOption(args, "--category", out var categoryError);
        var difficulty = GetStringOption(args, "--difficulty", out var difficultyError);
        var tag = GetStringOption(args, "--tag", out var tagError);
        if (categoryError is not null || difficultyError is not null || tagError is not null)
        {
            Console.Error.WriteLine(categoryError ?? difficultyError ?? tagError);
            return 1;
        }

        var experimentsRoot = GetExperimentsRoot();
        var experiments = ExperimentRepository.List(experimentsRoot, Console.Error.WriteLine, category, difficulty, tag);

        if (experiments.Count == 0)
        {
            Console.WriteLine("No experiments found.");
            return 0;
        }

        foreach (var experiment in experiments)
        {
            Console.WriteLine($"{experiment.Id} - {experiment.Name} [{experiment.Category}, {experiment.Difficulty}] ({string.Join(", ", experiment.Tags)})");
        }

        return 0;
    }

    private static async Task<int> RunExperimentAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("An experiment ID is required. Example: dugout run EXP001");
            return 1;
        }

        var experimentId = args[1];
        var warmupCount = GetPositiveIntOption(args, "--warmups", 3, "warmups", out var warmupError);
        if (warmupError is not null)
        {
            Console.Error.WriteLine(warmupError);
            return 1;
        }

        var iterationCount = GetPositiveIntOption(args, "--iterations", 10, "iterations", out var iterationError);
        if (iterationError is not null)
        {
            Console.Error.WriteLine(iterationError);
            return 1;
        }

        var experimentsRoot = GetExperimentsRoot();
        var resultsRoot = GetResultsRoot();
        var experimentDirectory = Path.Combine(experimentsRoot, experimentId);
        var experimentFilePath = Path.Combine(experimentDirectory, "experiment.json");

        if (!File.Exists(experimentFilePath))
        {
            Console.Error.WriteLine($"Experiment '{experimentId}' was not found at '{experimentFilePath}'.");
            return 1;
        }

        var connectionString = Environment.GetEnvironmentVariable("DUGOUT_CONNECTION");
        Console.WriteLine("Loading Experiment");
        ExperimentDefinition experiment;
        try
        {
            experiment = ExperimentRepository.Load(experimentFilePath);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unable to load experiment '{experimentId}': {exception.Message}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("The DUGOUT_CONNECTION environment variable is required. Set it to a SQL Server connection string.");
            return 1;
        }

        using var executor = new SqlQueryExecutor(connectionString);
        var repository = CreateResultsRepository();

        try
        {
            var pipeline = new BenchmarkPipeline(executor, repository);
            var result = await pipeline.RunAsync(experiment, experimentDirectory, resultsRoot, warmupCount, iterationCount, cancellationToken, Console.WriteLine);
            var resultFilePath = Path.Combine(resultsRoot, experimentId, $"{result.RunId}.json");
            PrintSummary(result, resultFilePath);
            Console.WriteLine("Benchmark Complete");
            return result.ExitCode;
        }
        finally
        {
            await executor.CloseAsync(CancellationToken.None);
        }
    }

    private static async Task<int> ShowHistoryAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("An experiment ID is required. Example: dugout history EXP001 --latest 10");
            return 1;
        }

        var latest = GetPositiveIntOption(args, "--latest", 20, "latest", out var latestError);
        if (latestError is not null)
        {
            Console.Error.WriteLine(latestError);
            return 1;
        }

        var sqlVersion = GetStringOption(args, "--sql-version", out var sqlVersionError);
        var machineName = GetStringOption(args, "--environment", out var environmentError);
        var thresholdsValid = TryGetRegressionThresholds(args, out var thresholds, out var thresholdError);
        if (sqlVersionError is not null || environmentError is not null || !thresholdsValid)
        {
            Console.Error.WriteLine(sqlVersionError ?? environmentError ?? thresholdError);
            return 1;
        }

        var repository = CreateResultsRepository();
        if (repository is null)
        {
            Console.Error.WriteLine("Set DUGOUT_CONNECTION or DUGOUT_RESULTS_CONNECTION before querying history.");
            return 1;
        }

        await repository.InitializeAsync(cancellationToken);
        var history = await repository.GetHistoryAsync(latest, args[1], sqlVersion, machineName, cancellationToken);
        if (history.Count == 0)
        {
            Console.WriteLine("No benchmark runs matched the requested filters.");
            return 0;
        }

        Console.WriteLine("RunId         Started (UTC)             Status     Duration (ms)  Logical Reads  SQL Version");
        foreach (var run in history)
        {
            var version = FirstLine(run.SqlServerVersion);
            Console.WriteLine($"{run.RunId,-13} {run.StartedUtc:yyyy-MM-dd HH:mm:ss}  {run.Status,-9} {run.OptimizedDurationMedianMs,13:N3} {run.OptimizedLogicalReadsMedian,14:N0}  {version}");
            Console.WriteLine($"  Experiment {run.ExperimentId} ({run.ExperimentName}), hash {run.ExperimentHash}, machine {run.MachineName}");
            Console.WriteLine($"  Plans: baseline {ShortHash(run.BaselinePlanHash)}, optimized {ShortHash(run.OptimizedPlanHash)}");
        }

        var trends = new TrendAnalyzer().Analyze(args[1], history, thresholds);
        Console.WriteLine();
        Console.WriteLine("Historical trends (optimized medians)");
        if (trends.Trends.Count == 0)
        {
            Console.WriteLine("  At least two successful runs with available metrics are needed.");
        }
        else
        {
            foreach (var trend in trends.Trends)
            {
                var change = trend.ChangePercent is null ? "n/a" : $"{trend.ChangePercent.Value:+0.0;-0.0;0.0}%";
                Console.WriteLine($"  {trend.Metric,-24} {trend.Direction,-12} {change} ({trend.SampleCount} runs)");
            }
        }

        return 0;
    }

    private static async Task<int> EstablishBaselineAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("An experiment ID is required. Example: dugout baseline EXP001");
            return 1;
        }

        var repository = CreateResultsRepository();
        if (repository is null)
        {
            Console.Error.WriteLine("Set DUGOUT_CONNECTION or DUGOUT_RESULTS_CONNECTION before establishing a baseline.");
            return 1;
        }

        await repository.InitializeAsync(cancellationToken);
        var run = await repository.GetLatestSuccessfulRunAsync(args[1], cancellationToken);
        if (run is null)
        {
            Console.Error.WriteLine($"No successful run was found for experiment '{args[1]}'. Run it before establishing a baseline.");
            return 1;
        }

        var baseline = new BenchmarkBaselineRecord
        {
            ExperimentId = run.ExperimentId,
            RunId = run.RunId,
            EstablishedUtc = DateTimeOffset.UtcNow,
            Metrics = BenchmarkMetricValues.FromResult(run)
        };
        await repository.SetBaselineAsync(baseline, cancellationToken);
        Console.WriteLine($"Baseline established for {baseline.ExperimentId} from run {baseline.RunId} at {baseline.EstablishedUtc:yyyy-MM-dd HH:mm:ss} UTC.");
        PrintMetricValues(baseline.Metrics);
        return 0;
    }

    private static async Task<int> CompareRunsAsync(string[] args, CancellationToken cancellationToken)
    {
        var baselineMode = args.Length >= 3
            && string.Equals(args[1], "baseline", StringComparison.OrdinalIgnoreCase)
            && string.Equals(args[2], "latest", StringComparison.OrdinalIgnoreCase);
        if (args.Length < 3 || (!baselineMode && args.Length > 3 && !args.Skip(3).Any(item => item.StartsWith("--", StringComparison.Ordinal))))
        {
            Console.Error.WriteLine("Use 'dugout compare <run1> <run2>' or 'dugout compare baseline latest [EXP001]'.");
            return 1;
        }

        if (!TryGetRegressionThresholds(args, out var thresholds, out var thresholdError))
        {
            Console.Error.WriteLine(thresholdError);
            return 1;
        }

        var repository = CreateResultsRepository();
        if (repository is null)
        {
            Console.Error.WriteLine("Set DUGOUT_CONNECTION or DUGOUT_RESULTS_CONNECTION before comparing runs.");
            return 1;
        }

        await repository.InitializeAsync(cancellationToken);
        BenchmarkBaselineRecord? baseline = null;
        BenchmarkResultDocument? previous;
        BenchmarkResultDocument? current;
        if (baselineMode)
        {
            var experimentId = args.Length > 3 && !args[3].StartsWith("--", StringComparison.Ordinal) ? args[3] : null;
            baseline = experimentId is null
                ? await repository.GetLatestBaselineAsync(cancellationToken)
                : await repository.GetBaselineAsync(experimentId, cancellationToken);
            if (baseline is null)
            {
                Console.Error.WriteLine(experimentId is null
                    ? "No performance baseline has been established. Run 'dugout baseline EXP001' first."
                    : $"No performance baseline has been established for '{experimentId}'.");
                return 1;
            }

            previous = await repository.GetRunAsync(baseline.RunId, cancellationToken);
            current = await repository.GetLatestSuccessfulRunAsync(baseline.ExperimentId, cancellationToken);
        }
        else
        {
            previous = await repository.GetRunAsync(args[1], cancellationToken);
            current = await repository.GetRunAsync(args[2], cancellationToken);
        }

        if (previous is null || current is null)
        {
            Console.Error.WriteLine(previous is null
                ? $"Run '{args[1]}' was not found in DugoutResults."
                : $"Run '{args[2]}' was not found in DugoutResults.");
            return 1;
        }

        if (!string.Equals(previous.ExperimentId, current.ExperimentId, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Runs from different experiments cannot be compared.");
            return 1;
        }

        previous.BaselinePlan = await repository.GetExecutionPlanAsync(previous.RunId, "Baseline", cancellationToken);
        previous.OptimizedPlan = await repository.GetExecutionPlanAsync(previous.RunId, "Optimized", cancellationToken);
        current.BaselinePlan = await repository.GetExecutionPlanAsync(current.RunId, "Baseline", cancellationToken);
        current.OptimizedPlan = await repository.GetExecutionPlanAsync(current.RunId, "Optimized", cancellationToken);

        var comparison = BenchmarkRunComparer.Compare(previous, current);
        Console.WriteLine($"Historical comparison: {comparison.PreviousRunId} -> {comparison.CurrentRunId}");
        Console.WriteLine($"Experiment: {previous.ExperimentId} ({previous.ExperimentHash}) -> {current.ExperimentId} ({current.ExperimentHash})");
        Console.WriteLine($"SQL Server: {FirstLine(previous.Environment.SqlServerVersion)} -> {FirstLine(current.Environment.SqlServerVersion)}");
        Console.WriteLine($"Environment: {previous.Environment.MachineName} -> {current.Environment.MachineName}");
        PrintHistoricalMetrics("Baseline", comparison.BaselineMetrics);
        PrintHistoricalMetrics("Optimized", comparison.OptimizedMetrics);
        PrintHistoricalPlan("Baseline plan", comparison.BaselinePlanComparison);
        PrintHistoricalPlan("Optimized plan", comparison.OptimizedPlanComparison);
        var referenceMetrics = baseline?.Metrics ?? BenchmarkMetricValues.FromResult(previous);
        var regression = new RegressionAnalyzer().Analyze(
            previous.ExperimentId,
            previous.RunId,
            current.RunId,
            referenceMetrics,
            BenchmarkMetricValues.FromResult(current),
            thresholds);
        PrintRegressionComparison(regression);
        var comparisonId = await repository.StoreComparisonAsync(regression, cancellationToken);
        Console.WriteLine($"Stored comparison: {comparisonId}");
        return 0;
    }

    private static void PrintRegressionComparison(BenchmarkComparison comparison)
    {
        Console.WriteLine();
        Console.WriteLine("Regression analysis");
        foreach (var metric in comparison.Metrics)
        {
            var change = metric.ChangePercent is null ? "n/a" : $"{metric.ChangePercent.Value:+0.0;-0.0;0.0}%";
            Console.WriteLine($"  {metric.Metric,-24} {change,8}  {metric.Classification}");
        }

        Console.WriteLine($"Benchmark health: {comparison.Health}");
        Console.WriteLine($"Regression detected: {comparison.RegressionDetected}");
    }

    private static void PrintMetricValues(IReadOnlyDictionary<string, decimal?> metrics)
    {
        foreach (var (name, value) in metrics)
        {
            Console.WriteLine($"  {name,-24} {value?.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}");
        }
    }

    private static async Task<int> ShowResourcesAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("An experiment ID is required. Example: dugout resources EXP001 --latest 100");
            return 1;
        }

        var latest = GetPositiveIntOption(args, "--latest", 100, "latest", out var latestError);
        if (latestError is not null)
        {
            Console.Error.WriteLine(latestError);
            return 1;
        }

        var repository = CreateResultsRepository();
        if (repository is null)
        {
            Console.Error.WriteLine("Set DUGOUT_CONNECTION or DUGOUT_RESULTS_CONNECTION before querying resource history.");
            return 1;
        }

        await repository.InitializeAsync(cancellationToken);
        var resources = await repository.GetResourceTrendsAsync(args[1], latest, cancellationToken);
        if (resources.Count == 0)
        {
            Console.WriteLine("No resource metrics matched the requested experiment.");
            return 0;
        }

        Console.WriteLine($"Resource history for {args[1]} ({resources.Count} measured executions)");
        Console.WriteLine();
        Console.WriteLine("Top memory consumers (by used grant)");
        foreach (var row in resources.OrderByDescending(item => item.UsedMemoryKb ?? -1).Take(10))
        {
            Console.WriteLine($"  {row.RunId} {row.Variant,-9} requested {FormatNullable(row.RequestedMemoryKb)} KB, granted {FormatNullable(row.GrantedMemoryKb)} KB, used {FormatNullable(row.UsedMemoryKb)} KB");
        }

        Console.WriteLine();
        Console.WriteLine("Highest TempDB usage (net pages)");
        foreach (var row in resources.OrderByDescending(item => item.TempDbPages ?? -1).Take(10))
        {
            Console.WriteLine($"  {row.RunId} {row.Variant,-9} {FormatNullable(row.TempDbPages)} pages ({FormatNullable(row.TempDbAllocatedPages)} allocated), worktable reads {FormatNullable(row.WorktableLogicalReads)}");
        }

        var parallel = resources.Count(item => item.UsedParallelPlan == true);
        var serial = resources.Count(item => item.UsedParallelPlan == false);
        var unavailable = resources.Count - parallel - serial;
        Console.WriteLine();
        Console.WriteLine("Parallel vs serial runs");
        Console.WriteLine($"  Parallel plans: {parallel}; serial plans: {serial}; unavailable: {unavailable}");
        foreach (var row in resources.Where(item => item.UsedParallelPlan.HasValue).Take(10))
        {
            Console.WriteLine($"  {row.RunId} {row.Variant,-9} {(row.UsedParallelPlan == true ? "parallel" : "serial")}, DOP {row.DegreeOfParallelism?.ToString() ?? "n/a"}, parallel operators {row.ParallelOperators?.ToString() ?? "n/a"}");
        }

        return 0;
    }

    private static string FormatNullable(long? value)
    {
        return value?.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a";
    }

    private static void PrintHistoricalMetrics(string name, IReadOnlyList<BenchmarkMetricComparison> metrics)
    {
        Console.WriteLine();
        Console.WriteLine($"{name} medians");
        Console.WriteLine("Metric                    Previous       Current   Improvement");
        foreach (var metric in metrics)
        {
            var improvement = metric.ImprovementPercent is null ? "n/a" : $"{metric.ImprovementPercent.Value:F2}%";
            Console.WriteLine($"{metric.Metric,-24} {metric.PreviousMedian,10:N2} {metric.CurrentMedian,13:N2} {improvement,14}");
        }
    }

    private static void PrintHistoricalPlan(string name, ExecutionPlanComparison? comparison)
    {
        Console.WriteLine();
        Console.WriteLine(name);
        if (comparison is null)
        {
            Console.WriteLine("  Plan data unavailable in one or both runs.");
            return;
        }

        Console.WriteLine($"  Hashes: {(comparison.PlanHashesMatch ? "match" : "differ")}");
        if (comparison.OperatorChanges.Count == 0)
        {
            Console.WriteLine("  Operator counts unchanged.");
            return;
        }

        foreach (var change in comparison.OperatorChanges)
        {
            Console.WriteLine($"  {change.PhysicalOperator}: {change.BaselineCount} -> {change.OptimizedCount}");
        }
    }

    private static IBenchmarkRepository? CreateResultsRepository()
    {
        var resultsConnectionString = Environment.GetEnvironmentVariable("DUGOUT_RESULTS_CONNECTION");
        if (!string.IsNullOrWhiteSpace(resultsConnectionString))
        {
            return new SqlBenchmarkRepository(resultsConnectionString);
        }

        var benchmarkConnectionString = Environment.GetEnvironmentVariable("DUGOUT_CONNECTION");
        if (string.IsNullOrWhiteSpace(benchmarkConnectionString))
        {
            return null;
        }

        var builder = new SqlConnectionStringBuilder(benchmarkConnectionString)
        {
            InitialCatalog = "DugoutResults"
        };
        return new SqlBenchmarkRepository(builder.ConnectionString);
    }

    private static string? GetStringOption(string[] args, string optionName, out string? error)
    {
        error = null;
        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"The {optionName} option requires a value.";
                return null;
            }

            return args[index + 1];
        }

        return null;
    }

    private static string ShortHash(string? hash)
    {
        return string.IsNullOrWhiteSpace(hash) ? "n/a" : hash[..Math.Min(hash.Length, 12)];
    }

    private static string FirstLine(string value)
    {
        return value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
    }

    private static void PrintSummary(BenchmarkResultDocument result, string resultFilePath)
    {
        Console.WriteLine();
        Console.WriteLine("Benchmark Summary");
        Console.WriteLine("Variant     Min        Avg        Median     Max        Stdev");
        Console.WriteLine("---------- --------- --------- --------- --------- ---------");
        Console.WriteLine($"Baseline    {result.Baseline.Statistics.Min:F3}    {result.Baseline.Statistics.Average:F3}    {result.Baseline.Statistics.Median:F3}    {result.Baseline.Statistics.Max:F3}    {result.Baseline.Statistics.StandardDeviation:F3}");
        Console.WriteLine($"Optimized   {result.Optimized.Statistics.Min:F3}    {result.Optimized.Statistics.Average:F3}    {result.Optimized.Statistics.Median:F3}    {result.Optimized.Statistics.Max:F3}    {result.Optimized.Statistics.StandardDeviation:F3}");
        Console.WriteLine($"Improvement: {(result.Improvement.DurationPercent is null ? "n/a" : result.Improvement.DurationPercent.Value.ToString("F3") + "%")}");
        Console.WriteLine();
        Console.WriteLine("Diagnostic Summary (median)");
        Console.WriteLine("Metric                 Baseline   Optimized   Improvement");
        PrintDiagnosticMetric("Logical Reads", result.Baseline.LogicalReadsStatistics.Median, result.Optimized.LogicalReadsStatistics.Median, result.Improvement.LogicalReadsPercent);
        PrintDiagnosticMetric("Physical Reads", result.Baseline.PhysicalReadsStatistics.Median, result.Optimized.PhysicalReadsStatistics.Median, null);
        PrintDiagnosticMetric("Scan Count", result.Baseline.ScanCountStatistics.Median, result.Optimized.ScanCountStatistics.Median, null);
        PrintDiagnosticMetric("CPU Time (ms)", result.Baseline.CpuTimeMsStatistics.Median, result.Optimized.CpuTimeMsStatistics.Median, result.Improvement.CpuTimePercent);
        PrintDiagnosticMetric("SQL Elapsed (ms)", result.Baseline.SqlElapsedTimeMsStatistics.Median, result.Optimized.SqlElapsedTimeMsStatistics.Median, result.Improvement.SqlElapsedTimePercent);
        PrintResourceMetric("Requested memory (KB)", result.Baseline.RequestedMemoryKbStatistics, result.Optimized.RequestedMemoryKbStatistics);
        PrintResourceMetric("Granted memory (KB)", result.Baseline.GrantedMemoryKbStatistics, result.Optimized.GrantedMemoryKbStatistics);
        PrintResourceMetric("Used memory (KB)", result.Baseline.UsedMemoryKbStatistics, result.Optimized.UsedMemoryKbStatistics);
        PrintResourceMetric("TempDB net pages", result.Baseline.TempDbPagesStatistics, result.Optimized.TempDbPagesStatistics);
        PrintResourceMetric("TempDB allocated pages", result.Baseline.TempDbAllocatedPagesStatistics, result.Optimized.TempDbAllocatedPagesStatistics);
        PrintResourceMetric("Worktable logical reads", result.Baseline.WorktableLogicalReadsStatistics, result.Optimized.WorktableLogicalReadsStatistics);
        PrintResourceMetric("Degree of parallelism", result.Baseline.DegreeOfParallelismStatistics, result.Optimized.DegreeOfParallelismStatistics);
        PrintResourceMetric("Parallel operators", result.Baseline.ParallelOperatorsStatistics, result.Optimized.ParallelOperatorsStatistics);
        PrintParallelMetric("Parallel plan", result.Baseline.Runs, result.Optimized.Runs);
        PrintExecutionPlans(result);
        PrintAdvisorAnalysis(result);
        Console.WriteLine($"Validation: {(result.Validation.Passed ? "Passed" : "Failed")}");
        if (result.Status == "Failed" && result.Failure is not null)
        {
            Console.WriteLine($"Failure stage: {result.Failure.Stage}");
            Console.WriteLine($"Failure reason: {result.Failure.Reason}");
        }

        Console.WriteLine($"Result file: {resultFilePath}");
    }

    private static void PrintAdvisorAnalysis(BenchmarkResultDocument result)
    {
        Console.WriteLine();
        Console.WriteLine("Advisor Findings");
        if (result.AdvisorFindings.Count == 0)
        {
            Console.WriteLine("  No applicable evidence-based findings.");
        }
        else
        {
            foreach (var finding in result.AdvisorFindings)
            {
                Console.WriteLine($"  [{finding.Severity}] {finding.Finding}");
                Console.WriteLine($"    Evidence: {finding.Evidence}");
            }
        }

        Console.WriteLine("Advisor Recommendations");
        if (result.AdvisorRecommendations.Count == 0)
        {
            Console.WriteLine("  No recommendations for the available evidence.");
        }
        else
        {
            foreach (var recommendation in result.AdvisorRecommendations)
            {
                Console.WriteLine($"  [{recommendation.Confidence:P0}] {recommendation.Recommendation}");
            }
        }
    }

    private static void PrintDiagnosticMetric(string name, decimal baseline, decimal optimized, decimal? improvement)
    {
        var improvementText = improvement is null ? "n/a" : $"{improvement.Value:F1}%";
        Console.WriteLine($"{name,-21} {baseline,9:N1} {optimized,11:N1} {improvementText,13}");
    }

    private static void PrintResourceMetric(string name, BenchmarkStatistics baseline, BenchmarkStatistics optimized)
    {
        var baselineValue = baseline.SampleCount == 0 ? "n/a" : baseline.Median.ToString("N1", System.Globalization.CultureInfo.InvariantCulture);
        var optimizedValue = optimized.SampleCount == 0 ? "n/a" : optimized.Median.ToString("N1", System.Globalization.CultureInfo.InvariantCulture);
        Console.WriteLine($"{name,-21} {baselineValue,9} {optimizedValue,11}");
    }

    private static void PrintParallelMetric(string name, IReadOnlyList<BenchmarkRunRecord> baseline, IReadOnlyList<BenchmarkRunRecord> optimized)
    {
        var baselineParallel = baseline.Count(run => run.UsedParallelPlan == true);
        var optimizedParallel = optimized.Count(run => run.UsedParallelPlan == true);
        var baselineKnown = baseline.Count(run => run.UsedParallelPlan.HasValue);
        var optimizedKnown = optimized.Count(run => run.UsedParallelPlan.HasValue);
        Console.WriteLine($"{name,-21} {baselineParallel}/{baselineKnown,-7} {optimizedParallel}/{optimizedKnown,-9}");
    }

    private static void PrintExecutionPlans(BenchmarkResultDocument result)
    {
        if (result.BaselinePlan is null || result.OptimizedPlan is null || result.PlanComparison is null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Execution Plans");
        PrintPlan("Baseline", result.BaselinePlan);
        PrintPlan("Optimized", result.OptimizedPlan);
        Console.WriteLine($"Plan hashes: {(result.PlanComparison.PlanHashesMatch ? "match" : "differ")}");
        Console.WriteLine("Observed operator count changes:");
        if (result.PlanComparison.OperatorChanges.Count == 0)
        {
            Console.WriteLine("  None");
            return;
        }

        foreach (var change in result.PlanComparison.OperatorChanges)
        {
            Console.WriteLine($"  {change.PhysicalOperator}: {change.BaselineCount} -> {change.OptimizedCount}");
        }
    }

    private static void PrintPlan(string name, ExecutionPlanArtifact plan)
    {
        Console.WriteLine($"{name}: {plan.PlanHash}");
        Console.WriteLine($"  Estimated cost: {plan.EstimatedCost?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}");
        Console.WriteLine($"  Estimated rows: {plan.EstimatedRows?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}");
        Console.WriteLine($"  Estimated subtree cost: {plan.EstimatedSubtreeCost?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}");
        Console.WriteLine($"  Operators: {string.Join(", ", plan.Operators.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key} ({pair.Value})"))}");
    }

    private static bool TryGetRegressionThresholds(string[] args, out RegressionThresholds thresholds, out string? error)
    {
        var configuredThresholds = new RegressionThresholds();
        thresholds = configuredThresholds;
        error = null;
        var options = new (string Name, Action<decimal> SetValue)[]
        {
            ("--minor-threshold", value => configuredThresholds.MinorPercent = value),
            ("--moderate-threshold", value => configuredThresholds.ModeratePercent = value),
            ("--severe-threshold", value => configuredThresholds.SeverePercent = value)
        };

        foreach (var (name, setValue) in options)
        {
            for (var index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 1 >= args.Length
                    || args[index + 1].StartsWith("--", StringComparison.Ordinal)
                    || !decimal.TryParse(args[index + 1], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    error = $"The {name} option requires a numeric value.";
                    return false;
                }

                setValue(value);
            }
        }

        try
        {
            configuredThresholds.Validate();
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static int GetPositiveIntOption(string[] args, string optionName, int defaultValue, string optionLabel, out string? error)
    {
        error = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(args[i + 1], out var value) || value <= 0)
            {
                error = $"The --{optionLabel} value must be a positive integer.";
                return defaultValue;
            }

            return value;
        }

        return defaultValue;
    }

    private static string GetExperimentsRoot()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "experiments");
    }

    private static string GetResultsRoot()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "results");
    }
}

