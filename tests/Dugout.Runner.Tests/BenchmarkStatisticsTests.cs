using System.Text.Json;
using Dugout.Contracts;
using Dugout.Runner;
using Dugout.Storage;

namespace Dugout.Runner.Tests;

public class BenchmarkStatisticsCalculatorTests
{
    [Fact]
    public void CalculateMedian_ReturnsAverageOfMiddleValues_WhenValuesAreEven()
    {
        var values = new[] { 10m, 20m, 30m, 40m };

        var result = BenchmarkStatisticsCalculator.CalculateMedian(values);

        Assert.Equal(25m, result);
    }

    [Fact]
    public void CalculateStandardDeviation_ReturnsExpectedValue()
    {
        var values = new[] { 1m, 2m, 3m, 4m, 5m };

        var result = BenchmarkStatisticsCalculator.CalculateStandardDeviation(values);

        Assert.Equal(1.58113883008419m, result);
    }

    [Fact]
    public void CalculateImprovement_UsesBaselineAndOptimizedMedianValues()
    {
        var baselineMedian = 100m;
        var optimizedMedian = 60m;

        var result = BenchmarkStatisticsCalculator.CalculateImprovement(baselineMedian, optimizedMedian);

        Assert.Equal(40m, result);
    }

    [Fact]
    public void CalculateImprovement_ReturnsNegativePercentageForRegression()
    {
        var result = BenchmarkStatisticsCalculator.CalculateImprovement(100m, 120m);

        Assert.Equal(-20m, result);
    }

    [Fact]
    public void ExperimentValidator_AcceptsExpectedPhaseOneExperimentIds()
    {
        Assert.True(ExperimentValidator.IsValidExperimentId("EXP001"));
        Assert.True(ExperimentValidator.IsValidExperimentId("EXP050"));
    }

    [Fact]
    public void ExperimentValidator_RejectsInvalidDefinition()
    {
        var temperatureDirectory = CreateTempDirectory();
        var baselineScript = Path.Combine(temperatureDirectory, "baseline.sql");
        var optimizedScript = Path.Combine(temperatureDirectory, "optimized.sql");
        File.WriteAllText(baselineScript, "SELECT 1;");
        File.WriteAllText(optimizedScript, "SELECT 1;");

        var experiment = new ExperimentDefinition
        {
            Id = "BAD",
            Name = "Bad",
            BaselineScript = "baseline.sql",
            OptimizedScript = "optimized.sql"
        };

        var validation = ExperimentValidator.ValidateDefinition(experiment, temperatureDirectory);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("EXP###"));
    }

    [Fact]
    public void ExperimentRepository_Load_ComputesHashFromExperimentAndReferencedScripts()
    {
        var experimentDirectory = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        var baselinePath = Path.Combine(experimentDirectory, "baseline.sql");
        var optimizedPath = Path.Combine(experimentDirectory, "optimized.sql");
        var setupPath = Path.Combine(experimentDirectory, "setup.sql");

        File.WriteAllText(experimentPath, "{\r\n  \"schemaVersion\": 1,\r\n  \"id\": \"EXP001\",\r\n  \"name\": \"Test\",\r\n  \"baselineScript\": \"baseline.sql\",\r\n  \"optimizedScript\": \"optimized.sql\",\r\n  \"setupScript\": \"setup.sql\"\r\n}\r\n");
        File.WriteAllText(baselinePath, "SELECT 1;\r\n");
        File.WriteAllText(optimizedPath, "SELECT 2;\r\n");
        File.WriteAllText(setupPath, "DROP TABLE IF EXISTS x;\r\n");

        var experiment = ExperimentRepository.Load(experimentPath);

        Assert.False(string.IsNullOrWhiteSpace(experiment.ExperimentHash));
    }

    [Fact]
    public void ExperimentRepository_List_SkipsUnreadableDefinitionsAndReportsWarning()
    {
        var experimentsRoot = CreateTempDirectory();
        var validDirectory = Path.Combine(experimentsRoot, "EXP001");
        var invalidDirectory = Path.Combine(experimentsRoot, "EXP002");
        Directory.CreateDirectory(validDirectory);
        Directory.CreateDirectory(invalidDirectory);
        File.WriteAllText(Path.Combine(validDirectory, "experiment.json"), "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Valid\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\"}");
        File.WriteAllText(Path.Combine(validDirectory, "baseline.sql"), "SELECT 1;");
        File.WriteAllText(Path.Combine(validDirectory, "optimized.sql"), "SELECT 1;");
        File.WriteAllText(Path.Combine(invalidDirectory, "experiment.json"), "{");
        var warnings = new List<string>();

        var experiments = ExperimentRepository.List(experimentsRoot, warnings.Add);

        Assert.Single(experiments);
        Assert.Equal("EXP001", experiments[0].Id);
        Assert.Single(warnings);
        Assert.Contains("EXP002", warnings[0]);
    }

    [Fact]
    public void ExperimentRepository_Load_ChangesHashWhenReferencedScriptChanges()
    {
        var experimentDirectory = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\"}");
        var baselinePath = Path.Combine(experimentDirectory, "baseline.sql");
        File.WriteAllText(baselinePath, "SELECT 1;");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 2;");

        var originalHash = ExperimentRepository.Load(experimentPath).ExperimentHash;
        File.WriteAllText(baselinePath, "SELECT 3;");
        var changedHash = ExperimentRepository.Load(experimentPath).ExperimentHash;

        Assert.NotEqual(originalHash, changedHash);
    }

    [Fact]
    public void ExperimentRepository_Load_NormalizesLineEndingsBeforeHashing()
    {
        var experimentDirectory = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        var experimentLf = "{\n  \"schemaVersion\": 1,\n  \"id\": \"EXP001\",\n  \"name\": \"Test\",\n  \"baselineScript\": \"baseline.sql\",\n  \"optimizedScript\": \"optimized.sql\"\n}\n";
        var experimentCrLf = experimentLf.Replace("\n", "\r\n");
        File.WriteAllText(experimentPath, experimentLf);
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 1;\n");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 2;\n");

        var lfHash = ExperimentRepository.Load(experimentPath).ExperimentHash;
        File.WriteAllText(experimentPath, experimentCrLf);
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 1;\r\n");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 2;\r\n");
        var crLfHash = ExperimentRepository.Load(experimentPath).ExperimentHash;

        Assert.Equal(lfHash, crLfHash);
    }

    [Fact]
    public async Task ResultStore_SaveAsync_PersistsDocument()
    {
        var root = CreateTempDirectory();
        var document = new BenchmarkResultDocument
        {
            RunId = "RUN0001",
            ExperimentId = "EXP001",
            Status = "Success",
            StartedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow,
            Validation = new ValidationSummary { Passed = true, Details = "ok" }
        };

        await ResultStore.SaveAsync(root, document);

        var filePath = Path.Combine(root, document.ExperimentId, $"{document.RunId}.json");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task BenchmarkPipeline_ExecutesValidBenchmarkAndPersistsResult()
    {
        var experimentDirectory = CreateTempDirectory();
        var resultsRoot = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        var baselinePath = Path.Combine(experimentDirectory, "baseline.sql");
        var optimizedPath = Path.Combine(experimentDirectory, "optimized.sql");
        var validationPath = Path.Combine(experimentDirectory, "validate.sql");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\",\"validationScript\":\"validate.sql\"}");
        File.WriteAllText(baselinePath, "SELECT 5 AS TotalAmount;");
        File.WriteAllText(optimizedPath, "SELECT 5 AS TotalAmount;");
        File.WriteAllText(validationPath, "SELECT 1;");

        var experiment = ExperimentRepository.Load(experimentPath);
        var executor = new FakeQueryExecutor();
        var pipeline = new BenchmarkPipeline(executor);

        var result = await pipeline.RunAsync(experiment, experimentDirectory, resultsRoot, 1, 2);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Success", result.Status);
        Assert.Equal(2, result.Baseline.Runs.Count);
        Assert.Equal(2, result.Optimized.Runs.Count);
    }

    [Fact]
    public async Task BenchmarkPipeline_SucceedsWithoutValidationScriptWhenProbeResultsMatch()
    {
        var experimentDirectory = CreateTempDirectory();
        var resultsRoot = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\"}");
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 5 AS TotalAmount;");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 5 AS TotalAmount;");

        var experiment = ExperimentRepository.Load(experimentPath);
        var result = await new BenchmarkPipeline(new FakeQueryExecutor()).RunAsync(experiment, experimentDirectory, resultsRoot, 1, 1);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Validation.Passed);
    }

    [Fact]
    public async Task BenchmarkPipeline_PersistsConnectionOpenFailure()
    {
        var experimentDirectory = CreateTempDirectory();
        var resultsRoot = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\"}");
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 1;");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 1;");
        var experiment = ExperimentRepository.Load(experimentPath);

        var result = await new BenchmarkPipeline(new FakeQueryExecutor { OpenFailure = new InvalidOperationException("Connection failed.") })
            .RunAsync(experiment, experimentDirectory, resultsRoot, 1, 1);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("Opening Connection", result.Failure?.Stage);
        Assert.Equal("Connection failed.", result.Failure?.Reason);
        Assert.True(File.Exists(Path.Combine(resultsRoot, experiment.Id, $"{result.RunId}.json")));
    }

    [Fact]
    public async Task BenchmarkPipeline_RunsCleanupWhenMeasuredQueryThrows()
    {
        var experimentDirectory = CreateTempDirectory();
        var resultsRoot = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\",\"cleanupScript\":\"cleanup.sql\"}");
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 5;");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 5;");
        File.WriteAllText(Path.Combine(experimentDirectory, "cleanup.sql"), "CLEANUP;");
        var executor = new FakeQueryExecutor { ThrowOnTimedExecution = true };
        var experiment = ExperimentRepository.Load(experimentPath);

        var result = await new BenchmarkPipeline(executor).RunAsync(experiment, experimentDirectory, resultsRoot, 0, 1);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("Executing Benchmarks", result.Failure?.Stage);
        Assert.Contains("CLEANUP;", executor.NonQueryCommands);
        Assert.Equal(CancellationToken.None, executor.CleanupCancellationToken);
    }

    [Fact]
    public async Task BenchmarkPipeline_RejectsDatasetValidationFailure()
    {
        var experimentDirectory = CreateTempDirectory();
        var resultsRoot = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\"}");
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 1;");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 1;");

        var experiment = ExperimentRepository.Load(experimentPath);
        var pipeline = new BenchmarkPipeline(new FakeQueryExecutor { DatasetValid = false });

        var result = await pipeline.RunAsync(experiment, experimentDirectory, resultsRoot, 1, 2);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("Failed", result.Status);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task BenchmarkPipeline_FailsWhenBaselineAndOptimizedResultsDifferBeforeWarmups()
    {
        var experimentDirectory = CreateTempDirectory();
        var resultsRoot = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\"}");
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 5 AS TotalAmount;");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 6 AS TotalAmount;");

        var experiment = ExperimentRepository.Load(experimentPath);
        var pipeline = new BenchmarkPipeline(new FakeQueryExecutor
        {
            RowResultSets = new Dictionary<string, IReadOnlyList<Dictionary<string, object?>>>
            {
                ["baseline"] = new List<Dictionary<string, object?>> { new() { ["TotalAmount"] = 5 } },
                ["optimized"] = new List<Dictionary<string, object?>> { new() { ["TotalAmount"] = 6 } }
            }
        });

        var result = await pipeline.RunAsync(experiment, experimentDirectory, resultsRoot, 1, 2);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("Failed", result.Status);
        Assert.Empty(result.Baseline.Runs);
        Assert.Equal("Validating Results", result.Failure?.Stage);
    }

    [Fact]
    public async Task BenchmarkPipeline_FailureHandling_WhenValidationScriptReturnsFalse()
    {
        var experimentDirectory = CreateTempDirectory();
        var resultsRoot = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\",\"validationScript\":\"validate.sql\"}");
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 5 AS TotalAmount;");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 5 AS TotalAmount;");
        File.WriteAllText(Path.Combine(experimentDirectory, "validate.sql"), "SELECT 0;");

        var experiment = ExperimentRepository.Load(experimentPath);
        var pipeline = new BenchmarkPipeline(new FakeQueryExecutor
        {
            ScalarResult = 0,
            RowResultSets = new Dictionary<string, IReadOnlyList<Dictionary<string, object?>>>
            {
                ["baseline"] = new List<Dictionary<string, object?>> { new() { ["TotalAmount"] = 5 } },
                ["optimized"] = new List<Dictionary<string, object?>> { new() { ["TotalAmount"] = 5 } }
            }
        });

        var result = await pipeline.RunAsync(experiment, experimentDirectory, resultsRoot, 1, 1);

        Assert.Equal(2, result.ExitCode);
        Assert.False(result.Validation.Passed);
        Assert.Single(result.Baseline.Runs);
        Assert.Single(result.Optimized.Runs);
        Assert.Equal(result.Baseline.Runs[0].DurationMs, result.Baseline.Statistics.Min);
        Assert.Equal(result.Optimized.Runs[0].DurationMs, result.Optimized.Statistics.Max);

        var resultPath = Path.Combine(resultsRoot, experiment.Id, $"{result.RunId}.json");
        var persistedResult = JsonSerializer.Deserialize<BenchmarkResultDocument>(
            await File.ReadAllTextAsync(resultPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(persistedResult);
        Assert.Single(persistedResult.Baseline.Runs);
        Assert.Single(persistedResult.Optimized.Runs);
        Assert.Equal(result.Baseline.Statistics.Min, persistedResult.Baseline.Statistics.Min);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeQueryExecutor : IQueryExecutor
    {
        private int _queryExecutionCount;

        public bool DatasetValid { get; set; } = true;
        public Exception? OpenFailure { get; set; }
        public bool ThrowOnTimedExecution { get; set; }
        public object? ScalarResult { get; set; } = 1;
        public Dictionary<string, IReadOnlyList<Dictionary<string, object?>>> RowResultSets { get; set; } = new();
        public List<string> NonQueryCommands { get; } = new();
        public CancellationToken? CleanupCancellationToken { get; private set; }

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            return OpenFailure is null ? Task.CompletedTask : Task.FromException(OpenFailure);
        }

        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<(bool IsValid, string? Message)> ValidateDatasetAsync(CancellationToken cancellationToken = default)
        {
            if (!DatasetValid)
            {
                return Task.FromResult<(bool IsValid, string? Message)>((false, "Dataset validation failed."));
            }

            return Task.FromResult<(bool IsValid, string? Message)>((true, "Database validated."));
        }

        public Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default)
        {
            var callIndex = Interlocked.Increment(ref _queryExecutionCount);

            if (RowResultSets.Count > 0)
            {
                if (callIndex % 2 == 1)
                {
                    return Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>(RowResultSets.TryGetValue("baseline", out var baseline) ? baseline : new List<Dictionary<string, object?>> { new() { ["TotalAmount"] = 5 } });
                }

                return Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>(RowResultSets.TryGetValue("optimized", out var optimized) ? optimized : new List<Dictionary<string, object?>> { new() { ["TotalAmount"] = 5 } });
            }

            return Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>(new List<Dictionary<string, object?>> { new() { ["TotalAmount"] = 5 } });
        }

        public Task<object?> ExecuteScalarAsync(string sql, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object?>(ScalarResult);
        }

        public Task<long> ExecuteQueryAndCountAsync(string sql, CancellationToken cancellationToken = default)
        {
            if (ThrowOnTimedExecution)
            {
                return Task.FromException<long>(new InvalidOperationException("Measured query failed."));
            }

            return Task.FromResult(1L);
        }

        public Task ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken = default)
        {
            NonQueryCommands.Add(sql);
            if (sql.StartsWith("CLEANUP", StringComparison.Ordinal))
            {
                CleanupCancellationToken = cancellationToken;
            }

            return Task.CompletedTask;
        }

        public Task<EnvironmentSummary> GetEnvironmentAsync(string runnerVersion, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EnvironmentSummary
            {
                SqlServerVersion = "Fake SQL Server",
                SqlServerEdition = "Developer",
                RunnerVersion = runnerVersion,
                DotnetVersion = Environment.Version.ToString(),
                MachineName = Environment.MachineName
            });
        }

        public void Dispose()
        {
        }
    }
}
