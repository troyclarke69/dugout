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
    public void SqlMetricsCollector_Parse_ExtractsAndSumsIoMetrics()
    {
        var result = SqlMetricsCollector.Parse(new[]
        {
            "Table 'Orders'. Scan count 1, logical reads 15,742, physical reads 2, read-ahead reads 10.",
            "Table 'Worktable'. Scan count 3, logical reads 258, physical reads 0, read-ahead reads 0."
        });

        Assert.Equal(4, result.ScanCount);
        Assert.Equal(16_000, result.LogicalReads);
        Assert.Equal(2, result.PhysicalReads);
        Assert.Equal(258, result.WorktableLogicalReads);
    }

    [Fact]
    public void ExecutionPlanParser_ParseRuntimeMetrics_ExtractsMemoryAndParallelism()
    {
        var plan = """
            <ShowPlanXML><StmtSimple><QueryPlan DegreeOfParallelism="4">
              <MemoryGrantInfo RequestedMemory="512" GrantedMemory="384" MaxUsedMemory="96" />
              <RelOp NodeId="1" PhysicalOp="Parallelism" LogicalOp="Gather Streams" />
              <RelOp NodeId="2" PhysicalOp="Parallelism" LogicalOp="Repartition Streams" />
            </QueryPlan></StmtSimple></ShowPlanXML>
            """;

        var result = ExecutionPlanParser.ParseRuntimeMetrics(plan);

        Assert.Equal(512, result.RequestedMemoryKb);
        Assert.Equal(384, result.GrantedMemoryKb);
        Assert.Equal(96, result.UsedMemoryKb);
        Assert.Equal(4, result.DegreeOfParallelism);
        Assert.True(result.UsedParallelPlan);
        Assert.Equal(2, result.ParallelOperators);
    }

    [Fact]
    public void ExecutionPlanParser_ParseRuntimeMetrics_IdentifiesSerialPlans()
    {
        var result = ExecutionPlanParser.ParseRuntimeMetrics(
            "<ShowPlanXML><StmtSimple><QueryPlan DegreeOfParallelism=\"1\"><RelOp PhysicalOp=\"Index Seek\" /></QueryPlan></StmtSimple></ShowPlanXML>");

        Assert.Equal(1, result.DegreeOfParallelism);
        Assert.False(result.UsedParallelPlan);
        Assert.Equal(0, result.ParallelOperators);
    }

    [Fact]
    public void SqlMetricsCollector_Parse_ExtractsTimeMetrics()
    {
        var result = SqlMetricsCollector.Parse(new[]
        {
            "SQL Server Execution Times: CPU time = 156 ms, elapsed time = 189 ms."
        });

        Assert.Equal(156m, result.CpuTimeMs);
        Assert.Equal(189m, result.SqlElapsedTimeMs);
    }

    [Fact]
    public void ExecutionPlanParser_ExtractsOperatorsOccurrencesAndMetadata()
    {
        var plan = """
            <ShowPlanXML xmlns="urn:plans"><BatchSequence><Batch><Statements>
              <StmtSimple StatementSubTreeCost="4.25"><QueryPlan><RelOp NodeId="0" PhysicalOp="Clustered Index Scan" LogicalOp="Clustered Index Scan" EstimateRows="100" EstimatedTotalSubtreeCost="4.25" />
              <RelOp NodeId="1" PhysicalOp="Sort" LogicalOp="Sort" EstimateRows="10" EstimatedTotalSubtreeCost="0.5" /></QueryPlan></StmtSimple>
            </Statements></Batch></BatchSequence></ShowPlanXML>
            """;

        var result = ExecutionPlanParser.Parse(plan);

        Assert.Equal(64, result.PlanHash.Length);
        Assert.Equal(4.25m, result.EstimatedCost);
        Assert.Equal(100m, result.EstimatedRows);
        Assert.Equal(4.25m, result.EstimatedSubtreeCost);
        Assert.Equal(1, result.Operators["Clustered Index Scan"]);
        Assert.Equal(1, result.Operators["Sort"]);
        Assert.Equal(2, result.OperatorOccurrences.Count);
        Assert.Equal(1, result.OperatorOccurrences[1].NodeId);
    }

    [Fact]
    public void ExecutionPlanParser_GeneratesStableHashForEquivalentXmlWhitespace()
    {
        var compact = "<ShowPlanXML><StmtSimple StatementSubTreeCost=\"1\"><QueryPlan><RelOp NodeId=\"0\" PhysicalOp=\"Index Seek\" /></QueryPlan></StmtSimple></ShowPlanXML>";
        var formatted = "<ShowPlanXML>\n  <StmtSimple StatementSubTreeCost=\"1\"><QueryPlan><RelOp NodeId=\"0\" PhysicalOp=\"Index Seek\" /></QueryPlan></StmtSimple>\n</ShowPlanXML>";

        Assert.Equal(ExecutionPlanParser.Parse(compact).PlanHash, ExecutionPlanParser.Parse(formatted).PlanHash);
    }

    [Fact]
    public void ExecutionPlanParser_UsesStableSqlServerPlanHashWhenCompileMetadataChanges()
    {
        var first = "<ShowPlanXML><StmtSimple QueryPlanHash=\"0x1234\" CompileTime=\"1\"><QueryPlan><RelOp NodeId=\"0\" PhysicalOp=\"Index Seek\" /></QueryPlan></StmtSimple></ShowPlanXML>";
        var second = "<ShowPlanXML><StmtSimple QueryPlanHash=\"0x1234\" CompileTime=\"9\"><QueryPlan><RelOp NodeId=\"0\" PhysicalOp=\"Index Seek\" /></QueryPlan></StmtSimple></ShowPlanXML>";

        Assert.Equal(ExecutionPlanParser.Parse(first).PlanHash, ExecutionPlanParser.Parse(second).PlanHash);
    }

    [Fact]
    public void BenchmarkRunComparer_ReportsMetricRegressionsAndPlanChanges()
    {
        var previous = new BenchmarkResultDocument
        {
            RunId = "RUN0001",
            Baseline = new BenchmarkRunsSummary
            {
                Statistics = new BenchmarkStatistics { Median = 100 },
                LogicalReadsStatistics = new BenchmarkStatistics { Median = 1_000 }
            },
            BaselinePlan = CreatePlan("Index Seek")
        };
        var current = new BenchmarkResultDocument
        {
            RunId = "RUN0002",
            Baseline = new BenchmarkRunsSummary
            {
                Statistics = new BenchmarkStatistics { Median = 120 },
                LogicalReadsStatistics = new BenchmarkStatistics { Median = 1_200 }
            },
            BaselinePlan = CreatePlan("Index Scan")
        };

        var comparison = BenchmarkRunComparer.Compare(previous, current);

        Assert.Equal("RUN0001", comparison.PreviousRunId);
        Assert.Equal(-20m, comparison.BaselineMetrics[0].ImprovementPercent);
        Assert.Equal(-20m, comparison.BaselineMetrics[1].ImprovementPercent);
        Assert.False(comparison.BaselinePlanComparison!.PlanHashesMatch);
        Assert.Contains(comparison.BaselinePlanComparison.OperatorChanges, change => change.PhysicalOperator == "Index Seek" && change.BaselineCount == 1 && change.OptimizedCount == 0);
        Assert.Contains(comparison.BaselinePlanComparison.OperatorChanges, change => change.PhysicalOperator == "Index Scan" && change.BaselineCount == 0 && change.OptimizedCount == 1);
    }

    [Fact]
    public void AdvisorEngine_ReportsAccessPathAndResourceReductionsWithEvidence()
    {
        var run = new BenchmarkResultDocument
        {
            RunId = "RUN0002",
            ExperimentId = "EXP001",
            Status = "Success",
            Baseline = CreateSummary(duration: 100m, reads: 1_000m, cpu: 100m),
            Optimized = CreateSummary(duration: 60m, reads: 400m, cpu: 50m),
            BaselinePlan = CreatePlan("Clustered Index Scan"),
            OptimizedPlan = CreatePlan("Index Seek")
        };

        var analysis = new AdvisorEngine().Analyze(run, Array.Empty<BenchmarkHistoryRecord>());

        Assert.Contains(analysis.Findings, finding => finding.Rule == "AccessPathOptimization" && finding.Evidence.Contains("1,000.0 -> 400.0 reads", StringComparison.Ordinal));
        Assert.Contains(analysis.Findings, finding => finding.Rule == "ResourceReduction");
        Assert.Contains(analysis.Recommendations, recommendation => recommendation.Rule == "AccessPathOptimization" && recommendation.Confidence > 0);
        Assert.Contains(analysis.Recommendations, recommendation => recommendation.Rule == "ResourceReduction");
    }

    [Fact]
    public void AdvisorEngine_UsesHistoricalTrendAndPlanChangeEvidence()
    {
        var previousPlan = CreatePlan("Index Seek");
        var run = new BenchmarkResultDocument
        {
            RunId = "RUN0002",
            ExperimentId = "EXP001",
            Status = "Success",
            StartedUtc = DateTimeOffset.Parse("2026-10-02T00:00:00Z"),
            Optimized = CreateSummary(duration: 130m, reads: 120m, cpu: 40m),
            OptimizedPlan = CreatePlan("Index Scan")
        };
        var history = new[]
        {
            new BenchmarkHistoryRecord
            {
                RunId = "RUN0001",
                ExperimentId = "EXP001",
                Status = "Success",
                StartedUtc = DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
                OptimizedDurationMedianMs = 100m,
                OptimizedLogicalReadsMedian = 100m,
                OptimizedCpuTimeMedianMs = 35m,
                OptimizedPlanHash = previousPlan.PlanHash
            }
        };

        var analysis = new AdvisorEngine().Analyze(run, history);

        Assert.Contains(analysis.Findings, finding => finding.Rule == "Regression" && finding.Severity == "Moderate");
        Assert.Contains(analysis.Findings, finding => finding.Rule == "PlanChange");
        Assert.Contains(analysis.Findings, finding => finding.Rule == "HistoricalTrend");
        Assert.Contains(analysis.Recommendations, recommendation => recommendation.Rule == "Regression");
    }

    [Fact]
    public void RegressionAnalyzer_ClassifiesConfiguredThresholdBoundaries()
    {
        var analyzer = new RegressionAnalyzer();
        var thresholds = new RegressionThresholds();
        var reference = new Dictionary<string, decimal?> { ["Duration (ms)"] = 100m };

        Assert.Equal("Healthy", Analyze(110m).Metrics[0].Classification);
        Assert.Equal("Minor", Analyze(110.01m).Metrics[0].Classification);
        Assert.Equal("Minor", Analyze(125m).Metrics[0].Classification);
        Assert.Equal("Moderate", Analyze(125.01m).Metrics[0].Classification);
        Assert.Equal("Moderate", Analyze(150m).Metrics[0].Classification);
        Assert.Equal("Severe", Analyze(150.01m).Metrics[0].Classification);

        BenchmarkComparison Analyze(decimal value) => analyzer.Analyze(
            "EXP001", "RUN0001", "RUN0002", reference,
            new Dictionary<string, decimal?> { ["Duration (ms)"] = value }, thresholds);
    }

    [Fact]
    public void TrendAnalyzer_DetectsIncreasingStableAndDecreasingMetrics()
    {
        var analyzer = new TrendAnalyzer();
        var history = new[]
        {
            new BenchmarkHistoryRecord
            {
                RunId = "RUN0001", Status = "Success", StartedUtc = DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
                OptimizedDurationMedianMs = 100m, OptimizedLogicalReadsMedian = 1_000m, OptimizedCpuTimeMedianMs = 50m
            },
            new BenchmarkHistoryRecord
            {
                RunId = "RUN0002", Status = "Success", StartedUtc = DateTimeOffset.Parse("2026-10-02T00:00:00Z"),
                OptimizedDurationMedianMs = 120m, OptimizedLogicalReadsMedian = 1_050m, OptimizedCpuTimeMedianMs = 40m
            }
        };

        var trends = analyzer.Analyze("EXP001", history, new RegressionThresholds());

        Assert.Equal("Increasing", trends.Trends.Single(trend => trend.Metric == "Duration (ms)").Direction);
        Assert.Equal("Stable", trends.Trends.Single(trend => trend.Metric == "Logical reads").Direction);
        Assert.Equal("Decreasing", trends.Trends.Single(trend => trend.Metric == "CPU time (ms)").Direction);
        Assert.Equal(2, trends.Trends.Single(trend => trend.Metric == "Duration (ms)").SampleCount);
    }

    [Fact]
    public void BenchmarkMetricValues_UsesRawSamplesWhenLegacyStatisticsHaveNoSampleCount()
    {
        var result = new BenchmarkResultDocument
        {
            Optimized = new BenchmarkRunsSummary
            {
                Runs = new List<BenchmarkRunRecord>
                {
                    new() { DurationMs = 10m, LogicalReads = 100, CpuTimeMs = 5m, SqlElapsedTimeMs = 7m },
                    new() { DurationMs = 20m, LogicalReads = 200, CpuTimeMs = 9m, SqlElapsedTimeMs = 11m }
                }
            }
        };

        var metrics = BenchmarkMetricValues.FromResult(result);

        Assert.Equal(15m, metrics["Duration (ms)"]);
        Assert.Equal(150m, metrics["Logical reads"]);
        Assert.Equal(7m, metrics["CPU time (ms)"]);
        Assert.Equal(9m, metrics["SQL elapsed time (ms)"]);
    }

    [Fact]
    public async Task FakeRepository_PersistsBaselineAndComparisonRecords()
    {
        var repository = new FakeBenchmarkRepository();
        var baseline = new BenchmarkBaselineRecord
        {
            ExperimentId = "EXP001",
            RunId = "RUN0001",
            EstablishedUtc = DateTimeOffset.UtcNow,
            Metrics = new Dictionary<string, decimal?> { ["Duration (ms)"] = 85m }
        };
        await repository.SetBaselineAsync(baseline);
        var comparison = new BenchmarkComparison { ExperimentId = "EXP001", RunA = "RUN0001", RunB = "RUN0002", Health = "Warning" };

        var comparisonId = await repository.StoreComparisonAsync(comparison);

        Assert.Equal(baseline.RunId, (await repository.GetBaselineAsync("EXP001"))?.RunId);
        Assert.Equal("Warning", (await repository.GetComparisonAsync(comparisonId))?.Health);
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
        Assert.Contains("SELECT 1;", experiment.DefinitionSnapshot);
        Assert.Contains("baseline.sql", experiment.DefinitionSnapshot);
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
    public void ExperimentRepository_List_FiltersByCategoryDifficultyAndTagCaseInsensitively()
    {
        var experimentsRoot = CreateTempDirectory();
        CreateExperiment(experimentsRoot, "EXP001", "SARGability", "Beginner", "Date", "Predicate");
        CreateExperiment(experimentsRoot, "EXP002", "Indexing", "Intermediate", "Lookup", "Index");
        CreateExperiment(experimentsRoot, "EXP003", "Indexing", "Advanced", "Lookup", "Plan");

        var results = ExperimentRepository.List(
            experimentsRoot,
            category: "indexing",
            difficulty: "INTERMEDIATE",
            tag: "lookup");

        var experiment = Assert.Single(results);
        Assert.Equal("EXP002", experiment.Id);
        Assert.Equal("Intermediate", experiment.Difficulty);
        Assert.Equal(new[] { "Lookup", "Index" }, experiment.Tags);
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
                var json = await File.ReadAllTextAsync(filePath);
                Assert.Contains("\"schemaVersion\": 4", json);
        }

        [Fact]
        public async Task ResultStore_LoadAsync_ReadsSchemaVersionOneResults()
        {
                var filePath = Path.Combine(CreateTempDirectory(), "legacy.json");
                await File.WriteAllTextAsync(filePath, """
                        {
                            "schemaVersion": 1,
                            "runId": "LEGACY01",
                            "experimentId": "EXP001",
                            "baseline": { "runs": [{ "durationMs": 12.5, "rowsReturned": 1 }] }
                        }
                        """);

                var result = await ResultStore.LoadAsync(filePath);

                Assert.Equal(1, result.SchemaVersion);
                Assert.Equal(12.5m, result.Baseline.Runs[0].DurationMs);
                Assert.Null(result.Baseline.Runs[0].LogicalReads);
    }

            [Fact]
            public async Task ResultStore_LoadAsync_ReadsSchemaVersionTwoResults()
            {
                var filePath = Path.Combine(CreateTempDirectory(), "legacy-v2.json");
                await File.WriteAllTextAsync(filePath, """
                    {
                      "schemaVersion": 2,
                      "runId": "LEGACY02",
                      "experimentId": "EXP001",
                      "baseline": { "runs": [{ "durationMs": 12.5, "rowsReturned": 1, "logicalReads": 24 }] }
                    }
                    """);

                var result = await ResultStore.LoadAsync(filePath);

                Assert.Equal(2, result.SchemaVersion);
                Assert.Equal(24, result.Baseline.Runs[0].LogicalReads);
                Assert.Null(result.BaselinePlan);
            }

            [Fact]
            public async Task ResultStore_LoadAsync_ReadsSchemaVersionThreeResults()
            {
                var filePath = Path.Combine(CreateTempDirectory(), "legacy-v3.json");
                await File.WriteAllTextAsync(filePath, """
                    {
                      "schemaVersion": 3,
                      "runId": "LEGACY03",
                      "experimentId": "EXP001",
                      "baseline": { "runs": [{ "durationMs": 12.5, "rowsReturned": 1 }] },
                      "baselinePlan": { "planHash": "abc", "planXml": "<ShowPlanXML />" }
                    }
                    """);

                var result = await ResultStore.LoadAsync(filePath);

                Assert.Equal(3, result.SchemaVersion);
                Assert.Equal("abc", result.BaselinePlan?.PlanHash);
                Assert.Null(result.Baseline.Runs[0].RequestedMemoryKb);
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
    public async Task BenchmarkPipeline_StoresAndAggregatesSqlMetrics()
    {
        var experimentDirectory = CreateTempDirectory();
        var resultsRoot = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\"}");
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 5 AS TotalAmount;");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT (2 + 3) AS TotalAmount;");
        var executor = new FakeQueryExecutor
        {
            PlanFactory = sql => sql.Contains("SELECT 5", StringComparison.Ordinal)
                ? CreatePlan("Index Scan")
                : CreatePlan("Index Seek"),
            MetricsFactory = execution => execution switch
            {
                1 => CreateMetrics(100, 50, 60),
                2 => CreateMetrics(50, 25, 30),
                3 => CreateMetrics(200, 70, 80),
                _ => CreateMetrics(70, 35, 50)
            }
        };

        var experiment = ExperimentRepository.Load(experimentPath);
        var result = await new BenchmarkPipeline(executor).RunAsync(experiment, experimentDirectory, resultsRoot, 0, 2);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(100, result.Baseline.Runs[0].LogicalReads);
        Assert.Equal(150m, result.Baseline.LogicalReadsStatistics.Average);
        Assert.Equal(60m, result.Optimized.LogicalReadsStatistics.Median);
        Assert.Equal(300L, result.Baseline.Runs[0].RequestedMemoryKb);
        Assert.Equal(75m, result.Baseline.UsedMemoryKbStatistics.Median);
        Assert.Equal(4m, result.Baseline.DegreeOfParallelismStatistics.Median);
        Assert.Equal(12L, result.Baseline.Runs[0].TempDbPages);
        Assert.Equal(60m, result.Improvement.LogicalReadsPercent);
        Assert.Equal(60m, result.Baseline.CpuTimeMsStatistics.Average);
        Assert.Equal(50m, result.Improvement.CpuTimePercent);
        Assert.InRange(result.Improvement.SqlElapsedTimePercent!.Value, 42.857m, 42.858m);
        Assert.NotNull(result.BaselinePlan);
        Assert.NotNull(result.OptimizedPlan);
        Assert.False(result.PlanComparison!.PlanHashesMatch);
        Assert.Contains(result.PlanComparison.OperatorChanges, change => change.PhysicalOperator == "Index Scan" && change.BaselineCount == 1 && change.OptimizedCount == 0);
        Assert.Contains(result.PlanComparison.OperatorChanges, change => change.PhysicalOperator == "Index Seek" && change.BaselineCount == 0 && change.OptimizedCount == 1);
        Assert.Contains(result.AdvisorFindings, finding => finding.Rule == "ResourceReduction");
        Assert.Contains(result.AdvisorRecommendations, recommendation => recommendation.Rule == "ResourceReduction");

        var persisted = await ResultStore.LoadAsync(Path.Combine(resultsRoot, experiment.Id, $"{result.RunId}.json"));
        Assert.Equal(4, persisted.SchemaVersion);
        Assert.Equal(100, persisted.Baseline.Runs[0].LogicalReads);
        Assert.Equal(50m, persisted.Baseline.Runs[0].CpuTimeMs);
        Assert.Equal(result.BaselinePlan.PlanHash, persisted.BaselinePlan?.PlanHash);
        Assert.Equal(result.BaselinePlan.PlanXml, persisted.BaselinePlan?.PlanXml);
        Assert.Equal(result.BaselinePlan.Operators, persisted.BaselinePlan?.Operators);
        Assert.Equal(result.PlanComparison?.OperatorChanges.Count, persisted.PlanComparison?.OperatorChanges.Count);
        Assert.Equal(300L, persisted.Baseline.Runs[0].RequestedMemoryKb);
        Assert.Contains(persisted.AdvisorFindings, finding => finding.Rule == "ResourceReduction");
        Assert.Contains(persisted.AdvisorRecommendations, recommendation => recommendation.Rule == "ResourceReduction");
    }

    [Fact]
    public async Task BenchmarkPipeline_PersistsRepositoryRunAndJsonExport()
    {
        var experimentDirectory = CreateTempDirectory();
        var resultsRoot = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\"}");
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 5 AS TotalAmount;");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 5 AS TotalAmount;");
        var experiment = ExperimentRepository.Load(experimentPath);
        var repository = new FakeBenchmarkRepository();
        var advisor = new TestAdvisorEngine(new BenchmarkAdvisorAnalysis
        {
            Findings = new List<AdvisorFinding>
            {
                new() { Rule = "TestRule", Finding = "Test finding", Evidence = "Test evidence", Severity = "Info" }
            },
            Recommendations = new List<AdvisorRecommendation>
            {
                new() { Rule = "TestRule", Recommendation = "Test recommendation", Confidence = 0.8m }
            }
        });

        var result = await new BenchmarkPipeline(new FakeQueryExecutor(), repository, advisor)
            .RunAsync(experiment, experimentDirectory, resultsRoot, 0, 1);

        Assert.Equal(0, result.ExitCode);
        Assert.True(repository.Initialized);
        Assert.Same(result, repository.StoredResult);
        Assert.True(File.Exists(Path.Combine(resultsRoot, experiment.Id, $"{result.RunId}.json")));
        Assert.Single(repository.StoredResult!.AdvisorFindings);
        Assert.Single(repository.StoredResult.AdvisorRecommendations);
        var jsonResult = await ResultStore.LoadAsync(Path.Combine(resultsRoot, experiment.Id, $"{result.RunId}.json"));
        Assert.Equal("TestRule", Assert.Single(jsonResult.AdvisorFindings).Rule);
        Assert.Equal("TestRule", Assert.Single(jsonResult.AdvisorRecommendations).Rule);
    }

    [Fact]
    public async Task BenchmarkPipeline_RetainsJsonExportWhenRepositoryPersistenceFails()
    {
        var experimentDirectory = CreateTempDirectory();
        var resultsRoot = CreateTempDirectory();
        var experimentPath = Path.Combine(experimentDirectory, "experiment.json");
        File.WriteAllText(experimentPath, "{\"schemaVersion\":1,\"id\":\"EXP001\",\"name\":\"Test\",\"baselineScript\":\"baseline.sql\",\"optimizedScript\":\"optimized.sql\"}");
        File.WriteAllText(Path.Combine(experimentDirectory, "baseline.sql"), "SELECT 5 AS TotalAmount;");
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 5 AS TotalAmount;");
        var experiment = ExperimentRepository.Load(experimentPath);
        var repository = new FakeBenchmarkRepository { StoreFailure = new InvalidOperationException("Repository unavailable.") };

        var result = await new BenchmarkPipeline(new FakeQueryExecutor(), repository)
            .RunAsync(experiment, experimentDirectory, resultsRoot, 0, 1);

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("Persisting Results Repository", result.Failure?.Stage);
        Assert.Equal("Repository unavailable.", result.Failure?.Reason);
        var persistedJson = await ResultStore.LoadAsync(Path.Combine(resultsRoot, experiment.Id, $"{result.RunId}.json"));
        Assert.Equal("Failed", persistedJson.Status);
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
        File.WriteAllText(Path.Combine(experimentDirectory, "optimized.sql"), "SELECT 5 AS TotalAmount;");

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

    private static void CreateExperiment(string experimentsRoot, string id, string category, string difficulty, params string[] tags)
    {
        var directory = Path.Combine(experimentsRoot, id);
        Directory.CreateDirectory(directory);
        var definition = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            id,
            name = $"{id} Example",
            category,
            difficulty,
            tags,
            learningObjectives = new[] { "Compare equivalent query forms." },
            baselineScript = "baseline.sql",
            optimizedScript = "optimized.sql"
        });
        File.WriteAllText(Path.Combine(directory, "experiment.json"), definition);
        File.WriteAllText(Path.Combine(directory, "baseline.sql"), "SELECT 1;");
        File.WriteAllText(Path.Combine(directory, "optimized.sql"), "SELECT 1;");
    }

    private static SqlExecutionMetrics CreateMetrics(long logicalReads, decimal cpuTimeMs, decimal elapsedTimeMs)
    {
        return new SqlExecutionMetrics
        {
            RowsReturned = 1,
            LogicalReads = logicalReads,
            PhysicalReads = 0,
            ScanCount = 1,
            CpuTimeMs = cpuTimeMs,
            SqlElapsedTimeMs = elapsedTimeMs,
            RequestedMemoryKb = 300,
            GrantedMemoryKb = 200,
            UsedMemoryKb = 75,
            TempDbPages = 12,
            TempDbAllocatedPages = 16,
            WorktableLogicalReads = 9,
            DegreeOfParallelism = 4,
            UsedParallelPlan = true,
            ParallelOperators = 2
        };
    }

    private static BenchmarkRunsSummary CreateSummary(decimal duration, decimal reads, decimal cpu)
    {
        return new BenchmarkRunsSummary
        {
            Statistics = new BenchmarkStatistics { SampleCount = 1, Median = duration },
            LogicalReadsStatistics = new BenchmarkStatistics { SampleCount = 1, Median = reads },
            CpuTimeMsStatistics = new BenchmarkStatistics { SampleCount = 1, Median = cpu }
        };
    }

    private static ExecutionPlanArtifact CreatePlan(string physicalOperator)
    {
        var planXml = $"<ShowPlanXML><StmtSimple StatementSubTreeCost=\"1\"><QueryPlan><RelOp NodeId=\"0\" PhysicalOp=\"{physicalOperator}\" LogicalOp=\"{physicalOperator}\" EstimateRows=\"1\" EstimatedTotalSubtreeCost=\"1\" /></QueryPlan></StmtSimple></ShowPlanXML>";
        return ExecutionPlanParser.Parse(planXml);
    }

    private sealed class FakeBenchmarkRepository : IBenchmarkRepository
    {
        public bool Initialized { get; private set; }
        public Exception? StoreFailure { get; set; }
        public BenchmarkResultDocument? StoredResult { get; private set; }
        public BenchmarkBaselineRecord? Baseline { get; private set; }
        public Dictionary<Guid, BenchmarkComparison> Comparisons { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Initialized = true;
            return Task.CompletedTask;
        }

        public Task StoreBenchmarkRunAsync(ExperimentDefinition experiment, BenchmarkResultDocument result, CancellationToken cancellationToken = default)
        {
            if (StoreFailure is not null)
            {
                return Task.FromException(StoreFailure);
            }

            StoredResult = result;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BenchmarkHistoryRecord>> GetHistoryAsync(int latest = 20, string? experimentId = null, string? sqlServerVersion = null, string? machineName = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BenchmarkHistoryRecord>>(Array.Empty<BenchmarkHistoryRecord>());
        }

        public Task<BenchmarkResultDocument?> GetRunAsync(string runId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BenchmarkResultDocument?>(StoredResult?.RunId == runId ? StoredResult : null);
        }

        public Task<BenchmarkResultDocument?> GetLatestSuccessfulRunAsync(string experimentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BenchmarkResultDocument?>(StoredResult?.ExperimentId == experimentId && StoredResult.Status == "Success" ? StoredResult : null);
        }

        public Task SetBaselineAsync(BenchmarkBaselineRecord baseline, CancellationToken cancellationToken = default)
        {
            Baseline = baseline;
            return Task.CompletedTask;
        }

        public Task<BenchmarkBaselineRecord?> GetBaselineAsync(string experimentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BenchmarkBaselineRecord?>(Baseline?.ExperimentId == experimentId ? Baseline : null);
        }

        public Task<BenchmarkBaselineRecord?> GetLatestBaselineAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Baseline);
        }

        public Task<Guid> StoreComparisonAsync(BenchmarkComparison comparison, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            Comparisons[id] = comparison;
            return Task.FromResult(id);
        }

        public Task<BenchmarkComparison?> GetComparisonAsync(Guid comparisonId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Comparisons.TryGetValue(comparisonId, out var comparison) ? comparison : null);
        }

        public Task<ExecutionPlanArtifact?> GetExecutionPlanAsync(string runId, string variant, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ExecutionPlanArtifact?>(null);
        }

        public Task<IReadOnlyList<BenchmarkResourceRecord>> GetResourceTrendsAsync(string experimentId, int latest = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BenchmarkResourceRecord>>(Array.Empty<BenchmarkResourceRecord>());
        }

        public Task<IReadOnlyList<ExperimentVersionRecord>> GetExperimentVersionsAsync(string experimentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ExperimentVersionRecord>>(Array.Empty<ExperimentVersionRecord>());
        }
    }

    private sealed class TestAdvisorEngine : IAdvisorEngine
    {
        private readonly BenchmarkAdvisorAnalysis _analysis;

        public TestAdvisorEngine(BenchmarkAdvisorAnalysis analysis)
        {
            _analysis = analysis;
        }

        public BenchmarkAdvisorAnalysis Analyze(BenchmarkResultDocument run, IReadOnlyList<BenchmarkHistoryRecord> history) => _analysis;
    }

    private sealed class FakeQueryExecutor : IQueryExecutor
    {
        private int _queryExecutionCount;
        private int _metricsExecutionCount;

        public bool DatasetValid { get; set; } = true;
        public Exception? OpenFailure { get; set; }
        public bool ThrowOnTimedExecution { get; set; }
        public Func<int, SqlExecutionMetrics>? MetricsFactory { get; set; }
        public Func<string, ExecutionPlanArtifact>? PlanFactory { get; set; }
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

        public Task<SqlExecutionMetrics> ExecuteQueryWithMetricsAsync(string sql, CancellationToken cancellationToken = default)
        {
            if (ThrowOnTimedExecution)
            {
                return Task.FromException<SqlExecutionMetrics>(new InvalidOperationException("Measured query failed."));
            }

            var execution = Interlocked.Increment(ref _metricsExecutionCount);
            return Task.FromResult(MetricsFactory?.Invoke(execution) ?? new SqlExecutionMetrics { RowsReturned = 1 });
        }

        public Task<ExecutionPlanArtifact> CaptureExecutionPlanAsync(string sql, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PlanFactory?.Invoke(sql) ?? CreatePlan("Constant Scan"));
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
