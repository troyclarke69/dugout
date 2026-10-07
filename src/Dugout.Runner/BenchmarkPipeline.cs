using System.Diagnostics;
using Dugout.Contracts;
using Dugout.Storage;

namespace Dugout.Runner;

public sealed class BenchmarkPipeline
{
    private readonly IQueryExecutor _queryExecutor;
    private readonly IBenchmarkRepository? _benchmarkRepository;
    private readonly IAdvisorEngine _advisorEngine;

    public BenchmarkPipeline(IQueryExecutor queryExecutor, IBenchmarkRepository? benchmarkRepository = null, IAdvisorEngine? advisorEngine = null)
    {
        _queryExecutor = queryExecutor;
        _benchmarkRepository = benchmarkRepository;
        _advisorEngine = advisorEngine ?? new AdvisorEngine();
    }

    public async Task<BenchmarkResultDocument> RunAsync(
        ExperimentDefinition experiment,
        string experimentDirectory,
        string resultsRoot,
        int warmupRuns,
        int measuredRuns,
        CancellationToken cancellationToken = default,
        Action<string>? progress = null)
    {
        var startedUtc = DateTimeOffset.UtcNow;
        var currentStage = "Opening Connection";
        var result = new BenchmarkResultDocument
        {
            RunId = Guid.NewGuid().ToString("N")[..12],
            ExperimentId = experiment.Id,
            ExperimentHash = experiment.ExperimentHash,
            StartedUtc = startedUtc,
            CompletedUtc = startedUtc,
            Status = "Running",
            ExitCode = 0,
            Options = new BenchmarkOptions
            {
                WarmupRuns = warmupRuns,
                MeasuredRuns = measuredRuns
            },
            Validation = new ValidationSummary { Passed = false, Details = "Pending" }
        };

        string? failureStage = null;
        var repositoryInitialized = false;

        void ReportStage(string stage)
        {
            currentStage = stage;
            progress?.Invoke(stage);
        }

        try
        {
            ReportStage("Validating Definition");
            var definitionValidation = ExperimentValidator.ValidateDefinition(experiment, experimentDirectory);
            if (!definitionValidation.IsValid)
            {
                result.Status = "Failed";
                result.ExitCode = 1;
                result.Validation = new ValidationSummary
                {
                    Passed = false,
                    Details = string.Join("; ", definitionValidation.Errors)
                };
                result.Failure = new FailureSummary
                {
                    Stage = "Validating Definition",
                    Reason = string.Join("; ", definitionValidation.Errors),
                    TimestampUtc = DateTimeOffset.UtcNow
                };
                return result;
            }

            ReportStage("Opening Connection");
            await _queryExecutor.OpenAsync(cancellationToken);

            ReportStage("Validating Dataset");
            result.Environment = await _queryExecutor.GetEnvironmentAsync(typeof(BenchmarkPipeline).Assembly.GetName().Version?.ToString() ?? "0.0.0", cancellationToken);

            var datasetValidation = await _queryExecutor.ValidateDatasetAsync(cancellationToken);
            if (!datasetValidation.IsValid)
            {
                result.Status = "Failed";
                result.ExitCode = 3;
                result.Validation = new ValidationSummary
                {
                    Passed = false,
                    Details = datasetValidation.Message ?? "Dataset validation failed."
                };
                result.Failure = new FailureSummary
                {
                    Stage = "Validating Dataset",
                    Reason = datasetValidation.Message ?? "Dataset validation failed.",
                    TimestampUtc = DateTimeOffset.UtcNow
                };
                return result;
            }

            if (_benchmarkRepository is not null)
            {
                ReportStage("Initializing Results Repository");
                await _benchmarkRepository.InitializeAsync(cancellationToken);
                repositoryInitialized = true;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(experiment.SetupScript))
                {
                    ReportStage("Preparing Dataset");
                    var setupPath = Path.Combine(experimentDirectory, experiment.SetupScript.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
                    await _queryExecutor.ExecuteNonQueryAsync(await File.ReadAllTextAsync(setupPath, cancellationToken), cancellationToken);
                }

                ReportStage("Validating Results");
                var baselineProbeSql = await File.ReadAllTextAsync(Path.Combine(experimentDirectory, experiment.BaselineScript.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)), cancellationToken);
                var optimizedProbeSql = await File.ReadAllTextAsync(Path.Combine(experimentDirectory, experiment.OptimizedScript.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)), cancellationToken);

                var baselineProbe = await _queryExecutor.ExecuteQueryAsync(baselineProbeSql, cancellationToken);
                var optimizedProbe = await _queryExecutor.ExecuteQueryAsync(optimizedProbeSql, cancellationToken);

                if (!ResultSetsEquivalent(baselineProbe, optimizedProbe))
                {
                    result.Status = "Failed";
                    result.ExitCode = 2;
                    result.Validation = new ValidationSummary
                    {
                        Passed = false,
                        Details = "Baseline and optimized pre-check result sets differ."
                    };
                    result.Failure = new FailureSummary
                    {
                        Stage = "Validating Results",
                        Reason = "Baseline and optimized pre-check result sets differ.",
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                    result.Improvement = new ImprovementSummary
                    {
                        DurationPercent = null,
                        Reason = "Benchmark validation failed before warmups."
                    };
                    return result;
                }

                ReportStage("Capturing Execution Plans");
                result.BaselinePlan = await _queryExecutor.CaptureExecutionPlanAsync(baselineProbeSql, cancellationToken);
                result.OptimizedPlan = await _queryExecutor.CaptureExecutionPlanAsync(optimizedProbeSql, cancellationToken);
                result.PlanComparison = ExecutionPlanComparer.Compare(result.BaselinePlan, result.OptimizedPlan);

                ReportStage("Running Baseline Warmups");
                for (var i = 0; i < warmupRuns; i++)
                {
                    await _queryExecutor.ExecuteQueryAndCountAsync(baselineProbeSql, cancellationToken);
                }

                ReportStage("Running Optimized Warmups");
                for (var i = 0; i < warmupRuns; i++)
                {
                    await _queryExecutor.ExecuteQueryAndCountAsync(optimizedProbeSql, cancellationToken);
                }

                var baselineRuns = new List<BenchmarkRunRecord>();
                var optimizedRuns = new List<BenchmarkRunRecord>();
                var sequenceNumber = 0;

                ReportStage("Executing Benchmarks");
                for (var i = 0; i < measuredRuns; i++)
                {
                    baselineRuns.Add(await MeasureQueryAsync(_queryExecutor, baselineProbeSql, ++sequenceNumber, $"Baseline {i + 1}", cancellationToken));
                    optimizedRuns.Add(await MeasureQueryAsync(_queryExecutor, optimizedProbeSql, ++sequenceNumber, $"Optimized {i + 1}", cancellationToken));
                }

                ReportStage("Calculating Statistics");
                result.Baseline = new BenchmarkRunsSummary
                {
                    Runs = baselineRuns,
                    Statistics = BuildStatisticsSummary(baselineRuns.Select(r => (decimal?)r.DurationMs)),
                    LogicalReadsStatistics = BuildStatisticsSummary(baselineRuns.Select(r => ToDecimal(r.LogicalReads))),
                    PhysicalReadsStatistics = BuildStatisticsSummary(baselineRuns.Select(r => ToDecimal(r.PhysicalReads))),
                    ScanCountStatistics = BuildStatisticsSummary(baselineRuns.Select(r => ToDecimal(r.ScanCount))),
                    CpuTimeMsStatistics = BuildStatisticsSummary(baselineRuns.Select(r => r.CpuTimeMs)),
                    SqlElapsedTimeMsStatistics = BuildStatisticsSummary(baselineRuns.Select(r => r.SqlElapsedTimeMs)),
                    RequestedMemoryKbStatistics = BuildStatisticsSummary(baselineRuns.Select(r => ToDecimal(r.RequestedMemoryKb))),
                    GrantedMemoryKbStatistics = BuildStatisticsSummary(baselineRuns.Select(r => ToDecimal(r.GrantedMemoryKb))),
                    UsedMemoryKbStatistics = BuildStatisticsSummary(baselineRuns.Select(r => ToDecimal(r.UsedMemoryKb))),
                    TempDbPagesStatistics = BuildStatisticsSummary(baselineRuns.Select(r => ToDecimal(r.TempDbPages))),
                    TempDbAllocatedPagesStatistics = BuildStatisticsSummary(baselineRuns.Select(r => ToDecimal(r.TempDbAllocatedPages))),
                    WorktableLogicalReadsStatistics = BuildStatisticsSummary(baselineRuns.Select(r => ToDecimal(r.WorktableLogicalReads))),
                    DegreeOfParallelismStatistics = BuildStatisticsSummary(baselineRuns.Select(r => r.DegreeOfParallelism is null ? null : (decimal?)r.DegreeOfParallelism.Value)),
                    ParallelOperatorsStatistics = BuildStatisticsSummary(baselineRuns.Select(r => r.ParallelOperators is null ? null : (decimal?)r.ParallelOperators.Value))
                };

                result.Optimized = new BenchmarkRunsSummary
                {
                    Runs = optimizedRuns,
                    Statistics = BuildStatisticsSummary(optimizedRuns.Select(r => (decimal?)r.DurationMs)),
                    LogicalReadsStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => ToDecimal(r.LogicalReads))),
                    PhysicalReadsStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => ToDecimal(r.PhysicalReads))),
                    ScanCountStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => ToDecimal(r.ScanCount))),
                    CpuTimeMsStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => r.CpuTimeMs)),
                    SqlElapsedTimeMsStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => r.SqlElapsedTimeMs)),
                    RequestedMemoryKbStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => ToDecimal(r.RequestedMemoryKb))),
                    GrantedMemoryKbStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => ToDecimal(r.GrantedMemoryKb))),
                    UsedMemoryKbStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => ToDecimal(r.UsedMemoryKb))),
                    TempDbPagesStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => ToDecimal(r.TempDbPages))),
                    TempDbAllocatedPagesStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => ToDecimal(r.TempDbAllocatedPages))),
                    WorktableLogicalReadsStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => ToDecimal(r.WorktableLogicalReads))),
                    DegreeOfParallelismStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => r.DegreeOfParallelism is null ? null : (decimal?)r.DegreeOfParallelism.Value)),
                    ParallelOperatorsStatistics = BuildStatisticsSummary(optimizedRuns.Select(r => r.ParallelOperators is null ? null : (decimal?)r.ParallelOperators.Value))
                };

                ReportStage("Validating Results");
                result.Validation = new ValidationSummary
                {
                    Passed = true,
                    Details = "Baseline and optimized pre-warmup result sets matched."
                };
                if (!string.IsNullOrWhiteSpace(experiment.ValidationScript))
                {
                    var validationScriptPath = Path.Combine(experimentDirectory, experiment.ValidationScript.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
                    var validationScript = await File.ReadAllTextAsync(validationScriptPath, cancellationToken);
                    var validationValue = await _queryExecutor.ExecuteScalarAsync(validationScript, cancellationToken);
                    var passed = IsExplicitPassValue(validationValue);

                    result.Validation = new ValidationSummary
                    {
                        Passed = passed,
                        Details = passed ? "Validation script returned explicit pass value." : $"Validation script returned '{validationValue ?? "null"}', which is not an explicit pass value."
                    };
                }
                if (!result.Validation.Passed)
                {
                    result.Status = "Failed";
                    result.ExitCode = 2;
                    result.Failure = new FailureSummary
                    {
                        Stage = "Validating Results",
                        Reason = result.Validation.Details,
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                    result.Improvement = new ImprovementSummary { DurationPercent = null, Reason = "Benchmark validation failed." };
                    return result;
                }

                var baselineMedian = result.Baseline.Statistics.Median;
                var optimizedMedian = result.Optimized.Statistics.Median;
                if (baselineMedian == 0m)
                {
                    result.Improvement = new ImprovementSummary
                    {
                        DurationPercent = null,
                        Reason = "Baseline median is zero; improvement percentage is undefined."
                    };
                }
                else
                {
                    result.Improvement = new ImprovementSummary
                    {
                        DurationPercent = BenchmarkStatisticsCalculator.CalculateImprovement(baselineMedian, optimizedMedian),
                        Reason = "Calculated from median durations."
                    };
                }

                result.Improvement.LogicalReadsPercent = CalculateMetricImprovement(result.Baseline.LogicalReadsStatistics.Median, result.Optimized.LogicalReadsStatistics.Median);
                result.Improvement.CpuTimePercent = CalculateMetricImprovement(result.Baseline.CpuTimeMsStatistics.Median, result.Optimized.CpuTimeMsStatistics.Median);
                result.Improvement.SqlElapsedTimePercent = CalculateMetricImprovement(result.Baseline.SqlElapsedTimeMsStatistics.Median, result.Optimized.SqlElapsedTimeMsStatistics.Median);

                result.Status = "Success";
                result.ExitCode = 0;
                ReportStage("Generating Advisor Analysis");
                var history = repositoryInitialized && _benchmarkRepository is not null
                    ? await _benchmarkRepository.GetHistoryAsync(latest: 100, experimentId: experiment.Id, cancellationToken: cancellationToken)
                    : Array.Empty<BenchmarkHistoryRecord>();
                var advisorAnalysis = _advisorEngine.Analyze(result, history);
                result.AdvisorFindings = advisorAnalysis.Findings;
                result.AdvisorRecommendations = advisorAnalysis.Recommendations;
            }
            catch
            {
                failureStage = currentStage;
                throw;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(experiment.CleanupScript))
                {
                    ReportStage("Cleaning Up");
                    var cleanupPath = Path.Combine(experimentDirectory, experiment.CleanupScript.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
                    var cleanupSql = await File.ReadAllTextAsync(cleanupPath, CancellationToken.None);
                    await _queryExecutor.ExecuteNonQueryAsync(cleanupSql, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            result.Status = "Failed";
            result.ExitCode = 3;
            result.Validation = new ValidationSummary
            {
                Passed = false,
                Details = ex.Message
            };
            result.Failure = new FailureSummary
            {
                Stage = failureStage ?? currentStage,
                Reason = ex.Message,
                TimestampUtc = DateTimeOffset.UtcNow
            };
            result.Improvement = new ImprovementSummary
            {
                DurationPercent = null,
                Reason = "Execution failure prevented benchmark completion."
            };
        }
        finally
        {
            result.CompletedUtc = DateTimeOffset.UtcNow;
            result.ExperimentHash = experiment.ExperimentHash;

            if (repositoryInitialized && _benchmarkRepository is not null)
            {
                ReportStage("Persisting Results Repository");
                try
                {
                    await _benchmarkRepository.StoreBenchmarkRunAsync(experiment, result, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    result.Status = "Failed";
                    result.ExitCode = 3;
                    result.Failure = new FailureSummary
                    {
                        Stage = "Persisting Results Repository",
                        Reason = exception.Message,
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                }
            }

            ReportStage("Saving Results");
            await ResultStore.SaveAsync(resultsRoot, result);
        }

        return result;
    }

    private static async Task<BenchmarkRunRecord> MeasureQueryAsync(IQueryExecutor queryExecutor, string sql, int sequenceNumber, string executionOrder, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var metrics = await queryExecutor.ExecuteQueryWithMetricsAsync(sql, cancellationToken);
        stopwatch.Stop();

        return new BenchmarkRunRecord
        {
            SequenceNumber = sequenceNumber,
            DurationMs = Convert.ToDecimal(stopwatch.Elapsed.TotalMilliseconds),
            RowsReturned = metrics.RowsReturned,
            LogicalReads = metrics.LogicalReads,
            PhysicalReads = metrics.PhysicalReads,
            ScanCount = metrics.ScanCount,
            CpuTimeMs = metrics.CpuTimeMs,
            SqlElapsedTimeMs = metrics.SqlElapsedTimeMs,
            RequestedMemoryKb = metrics.RequestedMemoryKb,
            GrantedMemoryKb = metrics.GrantedMemoryKb,
            UsedMemoryKb = metrics.UsedMemoryKb,
            TempDbPages = metrics.TempDbPages,
            TempDbAllocatedPages = metrics.TempDbAllocatedPages,
            WorktableLogicalReads = metrics.WorktableLogicalReads,
            DegreeOfParallelism = metrics.DegreeOfParallelism,
            UsedParallelPlan = metrics.UsedParallelPlan,
            ParallelOperators = metrics.ParallelOperators,
            ExecutionOrder = executionOrder
        };
    }

    private static BenchmarkStatistics BuildStatisticsSummary(IEnumerable<decimal?> metrics)
    {
        var values = metrics.Where(metric => metric.HasValue).Select(metric => metric!.Value).ToList();
        var stats = BenchmarkStatisticsCalculator.CalculateStatistics(values);

        return new BenchmarkStatistics
        {
            SampleCount = values.Count,
            Min = stats.Min,
            Max = stats.Max,
            Average = stats.Average,
            Median = stats.Median,
            StandardDeviation = stats.StandardDeviation
        };
    }

    private static decimal? ToDecimal(long? value) => value is null ? null : value.Value;

    private static decimal? CalculateMetricImprovement(decimal baselineMedian, decimal optimizedMedian)
    {
        return baselineMedian == 0m
            ? null
            : BenchmarkStatisticsCalculator.CalculateImprovement(baselineMedian, optimizedMedian);
    }

    private static bool IsExplicitPassValue(object? value)
    {
        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is int intValue)
        {
            return intValue == 1;
        }

        if (value is long longValue)
        {
            return longValue == 1;
        }

        if (value is string stringValue)
        {
            return string.Equals(stringValue.Trim(), "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stringValue.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool ResultSetsEquivalent(IReadOnlyList<Dictionary<string, object?>> left, IReadOnlyList<Dictionary<string, object?>> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!RowsEquivalent(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RowsEquivalent(Dictionary<string, object?> left, Dictionary<string, object?> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var key in left.Keys)
        {
            if (!right.TryGetValue(key, out var otherValue) || !ValuesEquivalent(left[key], otherValue))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValuesEquivalent(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left is decimal leftDecimal && right is decimal rightDecimal)
        {
            return leftDecimal == rightDecimal;
        }

        return string.Equals(Convert.ToString(left, System.Globalization.CultureInfo.InvariantCulture), Convert.ToString(right, System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
