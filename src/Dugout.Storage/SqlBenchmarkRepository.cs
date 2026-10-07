using System.Data;
using System.Text.Json;
using Dugout.Contracts;
using Microsoft.Data.SqlClient;

namespace Dugout.Storage;

public sealed class SqlBenchmarkRepository : IBenchmarkRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private const string SchemaSql = """
        IF OBJECT_ID(N'dbo.Experiments', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Experiments (
                ExperimentId nvarchar(64) NOT NULL PRIMARY KEY,
                Name nvarchar(256) NOT NULL,
                Description nvarchar(max) NULL,
                Category nvarchar(64) NOT NULL CONSTRAINT DF_Experiments_Category DEFAULT N'',
                Difficulty nvarchar(32) NOT NULL CONSTRAINT DF_Experiments_Difficulty DEFAULT N'Beginner',
                TagsJson nvarchar(max) NOT NULL CONSTRAINT DF_Experiments_Tags DEFAULT N'[]',
                LearningObjectivesJson nvarchar(max) NOT NULL CONSTRAINT DF_Experiments_Objectives DEFAULT N'[]',
                CurrentHash char(64) NULL,
                UpdatedUtc datetimeoffset(7) NOT NULL
            );
        END;
        IF COL_LENGTH(N'dbo.Experiments', N'Category') IS NULL ALTER TABLE dbo.Experiments ADD Category nvarchar(64) NOT NULL CONSTRAINT DF_Experiments_Category_M05 DEFAULT N'';
        IF COL_LENGTH(N'dbo.Experiments', N'Difficulty') IS NULL ALTER TABLE dbo.Experiments ADD Difficulty nvarchar(32) NOT NULL CONSTRAINT DF_Experiments_Difficulty_M05 DEFAULT N'Beginner';
        IF COL_LENGTH(N'dbo.Experiments', N'TagsJson') IS NULL ALTER TABLE dbo.Experiments ADD TagsJson nvarchar(max) NOT NULL CONSTRAINT DF_Experiments_Tags_M05 DEFAULT N'[]';
        IF COL_LENGTH(N'dbo.Experiments', N'LearningObjectivesJson') IS NULL ALTER TABLE dbo.Experiments ADD LearningObjectivesJson nvarchar(max) NOT NULL CONSTRAINT DF_Experiments_Objectives_M05 DEFAULT N'[]';
        IF OBJECT_ID(N'dbo.ExperimentVersions', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.ExperimentVersions (
                ExperimentId nvarchar(64) NOT NULL,
                ExperimentHash char(64) NOT NULL,
                CreatedUtc datetimeoffset(7) NOT NULL,
                DefinitionSnapshot nvarchar(max) NOT NULL,
                CONSTRAINT PK_ExperimentVersions PRIMARY KEY (ExperimentId, ExperimentHash),
                CONSTRAINT FK_ExperimentVersions_Experiments FOREIGN KEY (ExperimentId) REFERENCES dbo.Experiments(ExperimentId)
            );
        END;
        IF OBJECT_ID(N'dbo.Environments', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Environments (
                EnvironmentId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                SqlServerVersion nvarchar(512) NOT NULL,
                SqlServerEdition nvarchar(256) NOT NULL,
                DotnetVersion nvarchar(64) NOT NULL,
                RunnerVersion nvarchar(64) NOT NULL,
                MachineName nvarchar(256) NOT NULL
            );
        END;
        IF OBJECT_ID(N'dbo.BenchmarkRuns', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.BenchmarkRuns (
                RunId varchar(32) NOT NULL PRIMARY KEY,
                ExperimentId nvarchar(64) NOT NULL,
                ExperimentHash char(64) NULL,
                Status nvarchar(32) NOT NULL,
                ExitCode int NOT NULL,
                StartedUtc datetimeoffset(7) NOT NULL,
                CompletedUtc datetimeoffset(7) NOT NULL,
                EnvironmentId bigint NULL,
                BaselineDurationMedianMs decimal(28,8) NULL,
                OptimizedDurationMedianMs decimal(28,8) NULL,
                BaselineLogicalReadsMedian decimal(28,8) NULL,
                OptimizedLogicalReadsMedian decimal(28,8) NULL,
                ResultJson nvarchar(max) NOT NULL,
                CONSTRAINT FK_BenchmarkRuns_Experiments FOREIGN KEY (ExperimentId) REFERENCES dbo.Experiments(ExperimentId),
                CONSTRAINT FK_BenchmarkRuns_Environments FOREIGN KEY (EnvironmentId) REFERENCES dbo.Environments(EnvironmentId)
            );
            CREATE INDEX IX_BenchmarkRuns_Experiment_StartedUtc ON dbo.BenchmarkRuns(ExperimentId, StartedUtc DESC);
            CREATE INDEX IX_BenchmarkRuns_ExperimentHash ON dbo.BenchmarkRuns(ExperimentHash);
            CREATE INDEX IX_BenchmarkRuns_Environment ON dbo.BenchmarkRuns(EnvironmentId);
        END;
        IF OBJECT_ID(N'dbo.BenchmarkBaselines', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.BenchmarkBaselines (
                ExperimentId nvarchar(64) NOT NULL PRIMARY KEY,
                RunId varchar(32) NOT NULL,
                EstablishedUtc datetimeoffset(7) NOT NULL,
                MetricsJson nvarchar(max) NOT NULL,
                CONSTRAINT FK_BenchmarkBaselines_Experiments FOREIGN KEY (ExperimentId) REFERENCES dbo.Experiments(ExperimentId),
                CONSTRAINT FK_BenchmarkBaselines_Runs FOREIGN KEY (RunId) REFERENCES dbo.BenchmarkRuns(RunId)
            );
        END;
        IF OBJECT_ID(N'dbo.AdvisorFindings', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.AdvisorFindings (
                FindingId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                RunId varchar(32) NOT NULL,
                RuleName nvarchar(64) NOT NULL,
                Finding nvarchar(512) NOT NULL,
                Evidence nvarchar(max) NOT NULL,
                Severity nvarchar(32) NOT NULL,
                CONSTRAINT FK_AdvisorFindings_BenchmarkRuns FOREIGN KEY (RunId) REFERENCES dbo.BenchmarkRuns(RunId) ON DELETE CASCADE
            );
            CREATE INDEX IX_AdvisorFindings_RunId ON dbo.AdvisorFindings(RunId);
        END;
        IF OBJECT_ID(N'dbo.AdvisorRecommendations', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.AdvisorRecommendations (
                RecommendationId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                RunId varchar(32) NOT NULL,
                Recommendation nvarchar(1000) NOT NULL,
                RuleName nvarchar(64) NOT NULL,
                Confidence decimal(5,4) NOT NULL,
                CONSTRAINT FK_AdvisorRecommendations_BenchmarkRuns FOREIGN KEY (RunId) REFERENCES dbo.BenchmarkRuns(RunId) ON DELETE CASCADE
            );
            CREATE INDEX IX_AdvisorRecommendations_RunId ON dbo.AdvisorRecommendations(RunId);
        END;
        IF OBJECT_ID(N'dbo.BenchmarkComparisons', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.BenchmarkComparisons (
                ComparisonId uniqueidentifier NOT NULL PRIMARY KEY,
                ExperimentId nvarchar(64) NOT NULL,
                RunA varchar(32) NOT NULL,
                RunB varchar(32) NOT NULL,
                ComparedUtc datetimeoffset(7) NOT NULL,
                RegressionDetected bit NOT NULL,
                Health nvarchar(32) NOT NULL,
                ComparisonJson nvarchar(max) NOT NULL,
                CONSTRAINT FK_BenchmarkComparisons_Experiments FOREIGN KEY (ExperimentId) REFERENCES dbo.Experiments(ExperimentId),
                CONSTRAINT FK_BenchmarkComparisons_RunA FOREIGN KEY (RunA) REFERENCES dbo.BenchmarkRuns(RunId),
                CONSTRAINT FK_BenchmarkComparisons_RunB FOREIGN KEY (RunB) REFERENCES dbo.BenchmarkRuns(RunId)
            );
            CREATE INDEX IX_BenchmarkComparisons_Experiment_ComparedUtc ON dbo.BenchmarkComparisons(ExperimentId, ComparedUtc DESC);
        END;
        IF OBJECT_ID(N'dbo.BenchmarkMetrics', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.BenchmarkMetrics (
                RunId varchar(32) NOT NULL,
                Variant nvarchar(16) NOT NULL,
                SequenceNumber int NOT NULL,
                DurationMs decimal(28,8) NOT NULL,
                RowsReturned bigint NOT NULL,
                LogicalReads bigint NULL,
                PhysicalReads bigint NULL,
                ScanCount bigint NULL,
                CpuTimeMs decimal(28,8) NULL,
                SqlElapsedTimeMs decimal(28,8) NULL,
                RequestedMemoryKb bigint NULL,
                GrantedMemoryKb bigint NULL,
                UsedMemoryKb bigint NULL,
                TempDbPages bigint NULL,
                TempDbAllocatedPages bigint NULL,
                WorktableLogicalReads bigint NULL,
                DegreeOfParallelism int NULL,
                UsedParallelPlan bit NULL,
                ParallelOperators int NULL,
                CONSTRAINT PK_BenchmarkMetrics PRIMARY KEY (RunId, Variant, SequenceNumber),
                CONSTRAINT FK_BenchmarkMetrics_BenchmarkRuns FOREIGN KEY (RunId) REFERENCES dbo.BenchmarkRuns(RunId) ON DELETE CASCADE
            );
        END;
        IF COL_LENGTH(N'dbo.BenchmarkMetrics', N'RequestedMemoryKb') IS NULL ALTER TABLE dbo.BenchmarkMetrics ADD RequestedMemoryKb bigint NULL;
        IF COL_LENGTH(N'dbo.BenchmarkMetrics', N'GrantedMemoryKb') IS NULL ALTER TABLE dbo.BenchmarkMetrics ADD GrantedMemoryKb bigint NULL;
        IF COL_LENGTH(N'dbo.BenchmarkMetrics', N'UsedMemoryKb') IS NULL ALTER TABLE dbo.BenchmarkMetrics ADD UsedMemoryKb bigint NULL;
        IF COL_LENGTH(N'dbo.BenchmarkMetrics', N'TempDbPages') IS NULL ALTER TABLE dbo.BenchmarkMetrics ADD TempDbPages bigint NULL;
        IF COL_LENGTH(N'dbo.BenchmarkMetrics', N'TempDbAllocatedPages') IS NULL ALTER TABLE dbo.BenchmarkMetrics ADD TempDbAllocatedPages bigint NULL;
        IF COL_LENGTH(N'dbo.BenchmarkMetrics', N'WorktableLogicalReads') IS NULL ALTER TABLE dbo.BenchmarkMetrics ADD WorktableLogicalReads bigint NULL;
        IF COL_LENGTH(N'dbo.BenchmarkMetrics', N'DegreeOfParallelism') IS NULL ALTER TABLE dbo.BenchmarkMetrics ADD DegreeOfParallelism int NULL;
        IF COL_LENGTH(N'dbo.BenchmarkMetrics', N'UsedParallelPlan') IS NULL ALTER TABLE dbo.BenchmarkMetrics ADD UsedParallelPlan bit NULL;
        IF COL_LENGTH(N'dbo.BenchmarkMetrics', N'ParallelOperators') IS NULL ALTER TABLE dbo.BenchmarkMetrics ADD ParallelOperators int NULL;
        IF OBJECT_ID(N'dbo.ExecutionPlans', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.ExecutionPlans (
                RunId varchar(32) NOT NULL,
                Variant nvarchar(16) NOT NULL,
                PlanHash char(64) NOT NULL,
                EstimatedCost decimal(28,12) NULL,
                EstimatedRows decimal(28,8) NULL,
                EstimatedSubtreeCost decimal(28,12) NULL,
                OperatorsJson nvarchar(max) NOT NULL,
                OperatorOccurrencesJson nvarchar(max) NOT NULL,
                PlanXml nvarchar(max) NOT NULL,
                CONSTRAINT PK_ExecutionPlans PRIMARY KEY (RunId, Variant),
                CONSTRAINT FK_ExecutionPlans_BenchmarkRuns FOREIGN KEY (RunId) REFERENCES dbo.BenchmarkRuns(RunId) ON DELETE CASCADE
            );
            CREATE INDEX IX_ExecutionPlans_PlanHash ON dbo.ExecutionPlans(PlanHash);
        END;
        """;

    private readonly string _connectionString;

    public SqlBenchmarkRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var databaseName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("The results connection string must specify a database name.");
        }

        var adminBuilder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };
        await using (var adminConnection = new SqlConnection(adminBuilder.ConnectionString))
        {
            await adminConnection.OpenAsync(cancellationToken);
            await using var createDatabase = new SqlCommand(
                "IF DB_ID(@DatabaseName) IS NULL BEGIN DECLARE @Sql nvarchar(300) = N'CREATE DATABASE ' + QUOTENAME(@DatabaseName); EXEC(@Sql); END;",
                adminConnection);
            createDatabase.Parameters.Add("@DatabaseName", SqlDbType.NVarChar, 128).Value = databaseName;
            await createDatabase.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var schemaCommand = new SqlCommand(SchemaSql, connection) { CommandTimeout = 60 };
        await schemaCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task StoreBenchmarkRunAsync(
        ExperimentDefinition experiment,
        BenchmarkResultDocument result,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var experimentHash = NullIfWhiteSpace(result.ExperimentHash);
            var definitionSnapshot = string.IsNullOrWhiteSpace(experiment.DefinitionSnapshot)
                ? JsonSerializer.Serialize(experiment, SerializerOptions)
                : experiment.DefinitionSnapshot;
            await ExecuteAsync(connection, transaction, """
                IF EXISTS (SELECT 1 FROM dbo.Experiments WITH (UPDLOCK, HOLDLOCK) WHERE ExperimentId = @ExperimentId)
                    UPDATE dbo.Experiments SET Name = @Name, Description = @Description, Category = @Category, Difficulty = @Difficulty, TagsJson = @TagsJson, LearningObjectivesJson = @LearningObjectivesJson, CurrentHash = @CurrentHash, UpdatedUtc = @UpdatedUtc WHERE ExperimentId = @ExperimentId;
                ELSE
                    INSERT dbo.Experiments (ExperimentId, Name, Description, Category, Difficulty, TagsJson, LearningObjectivesJson, CurrentHash, UpdatedUtc) VALUES (@ExperimentId, @Name, @Description, @Category, @Difficulty, @TagsJson, @LearningObjectivesJson, @CurrentHash, @UpdatedUtc);
                IF @CurrentHash IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM dbo.ExperimentVersions WITH (UPDLOCK, HOLDLOCK) WHERE ExperimentId = @ExperimentId AND ExperimentHash = @CurrentHash)
                        UPDATE dbo.ExperimentVersions SET DefinitionSnapshot = @DefinitionSnapshot WHERE ExperimentId = @ExperimentId AND ExperimentHash = @CurrentHash;
                    ELSE
                        INSERT dbo.ExperimentVersions (ExperimentId, ExperimentHash, CreatedUtc, DefinitionSnapshot) VALUES (@ExperimentId, @CurrentHash, @UpdatedUtc, @DefinitionSnapshot);
                END;
                """,
                cancellationToken,
                ("@ExperimentId", SqlDbType.NVarChar, 64, experiment.Id),
                ("@Name", SqlDbType.NVarChar, 256, experiment.Name),
                ("@Description", SqlDbType.NVarChar, -1, (object?)experiment.Description ?? DBNull.Value),
                ("@Category", SqlDbType.NVarChar, 64, experiment.Category),
                ("@Difficulty", SqlDbType.NVarChar, 32, experiment.Difficulty),
                ("@TagsJson", SqlDbType.NVarChar, -1, JsonSerializer.Serialize(experiment.Tags, SerializerOptions)),
                ("@LearningObjectivesJson", SqlDbType.NVarChar, -1, JsonSerializer.Serialize(experiment.LearningObjectives, SerializerOptions)),
                ("@CurrentHash", SqlDbType.Char, 64, (object?)experimentHash ?? DBNull.Value),
                ("@UpdatedUtc", SqlDbType.DateTimeOffset, 0, DateTimeOffset.UtcNow),
                ("@DefinitionSnapshot", SqlDbType.NVarChar, -1, definitionSnapshot));

            var environmentId = await InsertEnvironmentAsync(connection, transaction, result.Environment, cancellationToken);

            await ExecuteAsync(connection, transaction, """
                INSERT dbo.BenchmarkRuns (
                    RunId, ExperimentId, ExperimentHash, Status, ExitCode, StartedUtc, CompletedUtc, EnvironmentId,
                    BaselineDurationMedianMs, OptimizedDurationMedianMs, BaselineLogicalReadsMedian, OptimizedLogicalReadsMedian, ResultJson)
                VALUES (
                    @RunId, @ExperimentId, @ExperimentHash, @Status, @ExitCode, @StartedUtc, @CompletedUtc, @EnvironmentId,
                    @BaselineDurationMedianMs, @OptimizedDurationMedianMs, @BaselineLogicalReadsMedian, @OptimizedLogicalReadsMedian, @ResultJson);
                """,
                cancellationToken,
                ("@RunId", SqlDbType.VarChar, 32, result.RunId),
                ("@ExperimentId", SqlDbType.NVarChar, 64, experiment.Id),
                ("@ExperimentHash", SqlDbType.Char, 64, (object?)experimentHash ?? DBNull.Value),
                ("@Status", SqlDbType.NVarChar, 32, result.Status),
                ("@ExitCode", SqlDbType.Int, 0, result.ExitCode),
                ("@StartedUtc", SqlDbType.DateTimeOffset, 0, result.StartedUtc),
                ("@CompletedUtc", SqlDbType.DateTimeOffset, 0, result.CompletedUtc),
                ("@EnvironmentId", SqlDbType.BigInt, 0, (object?)environmentId ?? DBNull.Value),
                ("@BaselineDurationMedianMs", SqlDbType.Decimal, 0, result.Baseline.Statistics.Median),
                ("@OptimizedDurationMedianMs", SqlDbType.Decimal, 0, result.Optimized.Statistics.Median),
                ("@BaselineLogicalReadsMedian", SqlDbType.Decimal, 0, result.Baseline.LogicalReadsStatistics.Median),
                ("@OptimizedLogicalReadsMedian", SqlDbType.Decimal, 0, result.Optimized.LogicalReadsStatistics.Median),
                ("@ResultJson", SqlDbType.NVarChar, -1, JsonSerializer.Serialize(result, SerializerOptions)));

            await InsertMetricsAsync(connection, transaction, result.RunId, "Baseline", result.Baseline.Runs, cancellationToken);
            await InsertMetricsAsync(connection, transaction, result.RunId, "Optimized", result.Optimized.Runs, cancellationToken);
            await InsertPlanAsync(connection, transaction, result.RunId, "Baseline", result.BaselinePlan, cancellationToken);
            await InsertPlanAsync(connection, transaction, result.RunId, "Optimized", result.OptimizedPlan, cancellationToken);
            await InsertAdvisorAnalysisAsync(connection, transaction, result.RunId, result.AdvisorFindings, result.AdvisorRecommendations, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<BenchmarkHistoryRecord>> GetHistoryAsync(
        int latest = 20,
        string? experimentId = null,
        string? sqlServerVersion = null,
        string? machineName = null,
        CancellationToken cancellationToken = default)
    {
        if (latest <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(latest), "The history limit must be positive.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT TOP (@Latest)
                r.RunId, r.ExperimentId, r.ExperimentHash, e.Name, r.Status, r.StartedUtc, r.CompletedUtc,
                env.SqlServerVersion, env.SqlServerEdition, env.MachineName,
                r.BaselineDurationMedianMs, r.OptimizedDurationMedianMs,
                r.BaselineLogicalReadsMedian,
                CASE
                    WHEN TRY_CONVERT(int, JSON_VALUE(r.ResultJson, '$.optimized.logicalReadsStatistics.sampleCount')) > 0 THEN r.OptimizedLogicalReadsMedian
                    WHEN JSON_VALUE(r.ResultJson, '$.optimized.logicalReadsStatistics.sampleCount') IS NULL THEN r.OptimizedLogicalReadsMedian
                END,
                CASE
                    WHEN TRY_CONVERT(int, JSON_VALUE(r.ResultJson, '$.optimized.cpuTimeMsStatistics.sampleCount')) > 0
                        OR JSON_VALUE(r.ResultJson, '$.optimized.cpuTimeMsStatistics.sampleCount') IS NULL
                    THEN TRY_CONVERT(decimal(28,8), JSON_VALUE(r.ResultJson, '$.optimized.cpuTimeMsStatistics.median'))
                END,
                CASE
                    WHEN TRY_CONVERT(int, JSON_VALUE(r.ResultJson, '$.optimized.sqlElapsedTimeMsStatistics.sampleCount')) > 0
                        OR JSON_VALUE(r.ResultJson, '$.optimized.sqlElapsedTimeMsStatistics.sampleCount') IS NULL
                    THEN TRY_CONVERT(decimal(28,8), JSON_VALUE(r.ResultJson, '$.optimized.sqlElapsedTimeMsStatistics.median'))
                END,
                bp.PlanHash, op.PlanHash
            FROM dbo.BenchmarkRuns AS r
            JOIN dbo.Experiments AS e ON e.ExperimentId = r.ExperimentId
            LEFT JOIN dbo.Environments AS env ON env.EnvironmentId = r.EnvironmentId
            LEFT JOIN dbo.ExecutionPlans AS bp ON bp.RunId = r.RunId AND bp.Variant = N'Baseline'
            LEFT JOIN dbo.ExecutionPlans AS op ON op.RunId = r.RunId AND op.Variant = N'Optimized'
            WHERE (@ExperimentId IS NULL OR r.ExperimentId = @ExperimentId)
                            AND (@SqlServerVersion IS NULL OR LEFT(env.SqlServerVersion, LEN(@SqlServerVersion)) = @SqlServerVersion)
              AND (@MachineName IS NULL OR env.MachineName = @MachineName)
            ORDER BY r.StartedUtc DESC;
            """, connection);
        command.Parameters.Add("@Latest", SqlDbType.Int).Value = latest;
        command.Parameters.Add("@ExperimentId", SqlDbType.NVarChar, 64).Value = (object?)NullIfWhiteSpace(experimentId) ?? DBNull.Value;
        command.Parameters.Add("@SqlServerVersion", SqlDbType.NVarChar, 512).Value = (object?)NullIfWhiteSpace(sqlServerVersion) ?? DBNull.Value;
        command.Parameters.Add("@MachineName", SqlDbType.NVarChar, 256).Value = (object?)NullIfWhiteSpace(machineName) ?? DBNull.Value;

        var records = new List<BenchmarkHistoryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new BenchmarkHistoryRecord
            {
                RunId = reader.GetString(0),
                ExperimentId = reader.GetString(1),
                ExperimentHash = reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim(),
                ExperimentName = reader.GetString(3),
                Status = reader.GetString(4),
                StartedUtc = reader.GetDateTimeOffset(5),
                CompletedUtc = reader.GetDateTimeOffset(6),
                SqlServerVersion = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                SqlServerEdition = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                MachineName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                BaselineDurationMedianMs = ReadNullableDecimal(reader, 10),
                OptimizedDurationMedianMs = ReadNullableDecimal(reader, 11),
                BaselineLogicalReadsMedian = ReadNullableDecimal(reader, 12),
                OptimizedLogicalReadsMedian = ReadNullableDecimal(reader, 13),
                OptimizedCpuTimeMedianMs = ReadNullableDecimal(reader, 14),
                OptimizedSqlElapsedTimeMedianMs = ReadNullableDecimal(reader, 15),
                BaselinePlanHash = reader.IsDBNull(16) ? null : reader.GetString(16).Trim(),
                OptimizedPlanHash = reader.IsDBNull(17) ? null : reader.GetString(17).Trim()
            });
        }

        return records;
    }

    public async Task<BenchmarkResultDocument?> GetRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT ResultJson FROM dbo.BenchmarkRuns WHERE RunId = @RunId;", connection);
        command.Parameters.Add("@RunId", SqlDbType.VarChar, 32).Value = runId;
        var resultJson = await command.ExecuteScalarAsync(cancellationToken) as string;
        return resultJson is null ? null : JsonSerializer.Deserialize<BenchmarkResultDocument>(resultJson, SerializerOptions);
    }

    public async Task<BenchmarkResultDocument?> GetLatestSuccessfulRunAsync(string experimentId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT TOP (1) ResultJson FROM dbo.BenchmarkRuns WHERE ExperimentId = @ExperimentId AND Status = N'Success' ORDER BY StartedUtc DESC;", connection);
        command.Parameters.Add("@ExperimentId", SqlDbType.NVarChar, 64).Value = experimentId;
        var resultJson = await command.ExecuteScalarAsync(cancellationToken) as string;
        return resultJson is null ? null : JsonSerializer.Deserialize<BenchmarkResultDocument>(resultJson, SerializerOptions);
    }

    public async Task SetBaselineAsync(BenchmarkBaselineRecord baseline, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            IF EXISTS (SELECT 1 FROM dbo.BenchmarkBaselines WITH (UPDLOCK, HOLDLOCK) WHERE ExperimentId = @ExperimentId)
                UPDATE dbo.BenchmarkBaselines SET RunId = @RunId, EstablishedUtc = @EstablishedUtc, MetricsJson = @MetricsJson WHERE ExperimentId = @ExperimentId;
            ELSE
                INSERT dbo.BenchmarkBaselines (ExperimentId, RunId, EstablishedUtc, MetricsJson) VALUES (@ExperimentId, @RunId, @EstablishedUtc, @MetricsJson);
            """, connection);
        command.Parameters.Add("@ExperimentId", SqlDbType.NVarChar, 64).Value = baseline.ExperimentId;
        command.Parameters.Add("@RunId", SqlDbType.VarChar, 32).Value = baseline.RunId;
        command.Parameters.Add("@EstablishedUtc", SqlDbType.DateTimeOffset).Value = baseline.EstablishedUtc;
        command.Parameters.Add("@MetricsJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(baseline.Metrics, SerializerOptions);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BenchmarkBaselineRecord?> GetBaselineAsync(string experimentId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT ExperimentId, RunId, EstablishedUtc, MetricsJson FROM dbo.BenchmarkBaselines WHERE ExperimentId = @ExperimentId;", connection);
        command.Parameters.Add("@ExperimentId", SqlDbType.NVarChar, 64).Value = experimentId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BenchmarkBaselineRecord
        {
            ExperimentId = reader.GetString(0),
            RunId = reader.GetString(1),
            EstablishedUtc = reader.GetDateTimeOffset(2),
            Metrics = JsonSerializer.Deserialize<Dictionary<string, decimal?>>(reader.GetString(3), SerializerOptions) ?? new(StringComparer.Ordinal)
        };
    }

    public async Task<BenchmarkBaselineRecord?> GetLatestBaselineAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT TOP (1) ExperimentId, RunId, EstablishedUtc, MetricsJson FROM dbo.BenchmarkBaselines ORDER BY EstablishedUtc DESC;", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BenchmarkBaselineRecord
        {
            ExperimentId = reader.GetString(0),
            RunId = reader.GetString(1),
            EstablishedUtc = reader.GetDateTimeOffset(2),
            Metrics = JsonSerializer.Deserialize<Dictionary<string, decimal?>>(reader.GetString(3), SerializerOptions) ?? new(StringComparer.Ordinal)
        };
    }

    public async Task<Guid> StoreComparisonAsync(BenchmarkComparison comparison, CancellationToken cancellationToken = default)
    {
        var comparisonId = Guid.NewGuid();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            INSERT dbo.BenchmarkComparisons (ComparisonId, ExperimentId, RunA, RunB, ComparedUtc, RegressionDetected, Health, ComparisonJson)
            VALUES (@ComparisonId, @ExperimentId, @RunA, @RunB, @ComparedUtc, @RegressionDetected, @Health, @ComparisonJson);
            """, connection);
        command.Parameters.Add("@ComparisonId", SqlDbType.UniqueIdentifier).Value = comparisonId;
        command.Parameters.Add("@ExperimentId", SqlDbType.NVarChar, 64).Value = comparison.ExperimentId;
        command.Parameters.Add("@RunA", SqlDbType.VarChar, 32).Value = comparison.RunA;
        command.Parameters.Add("@RunB", SqlDbType.VarChar, 32).Value = comparison.RunB;
        command.Parameters.Add("@ComparedUtc", SqlDbType.DateTimeOffset).Value = comparison.ComparedUtc;
        command.Parameters.Add("@RegressionDetected", SqlDbType.Bit).Value = comparison.RegressionDetected;
        command.Parameters.Add("@Health", SqlDbType.NVarChar, 32).Value = comparison.Health;
        command.Parameters.Add("@ComparisonJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(comparison, SerializerOptions);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return comparisonId;
    }

    public async Task<BenchmarkComparison?> GetComparisonAsync(Guid comparisonId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT ComparisonJson FROM dbo.BenchmarkComparisons WHERE ComparisonId = @ComparisonId;", connection);
        command.Parameters.Add("@ComparisonId", SqlDbType.UniqueIdentifier).Value = comparisonId;
        var comparisonJson = await command.ExecuteScalarAsync(cancellationToken) as string;
        return comparisonJson is null ? null : JsonSerializer.Deserialize<BenchmarkComparison>(comparisonJson, SerializerOptions);
    }

    public async Task<ExecutionPlanArtifact?> GetExecutionPlanAsync(
        string runId,
        string variant,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(variant, "Baseline", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(variant, "Optimized", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Variant must be Baseline or Optimized.", nameof(variant));
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT PlanHash, EstimatedCost, EstimatedRows, EstimatedSubtreeCost, OperatorsJson, OperatorOccurrencesJson, PlanXml
            FROM dbo.ExecutionPlans WHERE RunId = @RunId AND Variant = @Variant;
            """, connection);
        command.Parameters.Add("@RunId", SqlDbType.VarChar, 32).Value = runId;
        command.Parameters.Add("@Variant", SqlDbType.NVarChar, 16).Value = variant;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ExecutionPlanArtifact
        {
            PlanHash = reader.GetString(0).Trim(),
            EstimatedCost = ReadNullableDecimal(reader, 1),
            EstimatedRows = ReadNullableDecimal(reader, 2),
            EstimatedSubtreeCost = ReadNullableDecimal(reader, 3),
            Operators = JsonSerializer.Deserialize<Dictionary<string, int>>(reader.GetString(4), SerializerOptions) ?? new(StringComparer.Ordinal),
            OperatorOccurrences = JsonSerializer.Deserialize<List<ExecutionPlanOperatorOccurrence>>(reader.GetString(5), SerializerOptions) ?? new(),
            PlanXml = reader.GetString(6)
        };
    }

    public async Task<IReadOnlyList<BenchmarkResourceRecord>> GetResourceTrendsAsync(
        string experimentId,
        int latest = 100,
        CancellationToken cancellationToken = default)
    {
        if (latest <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(latest));
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT TOP (@Latest) m.RunId, r.ExperimentId, m.Variant, m.SequenceNumber,
                   m.RequestedMemoryKb, m.GrantedMemoryKb, m.UsedMemoryKb,
                   m.TempDbPages, m.TempDbAllocatedPages, m.WorktableLogicalReads,
                   m.DegreeOfParallelism, m.UsedParallelPlan, m.ParallelOperators
            FROM dbo.BenchmarkMetrics AS m
            JOIN dbo.BenchmarkRuns AS r ON r.RunId = m.RunId
            WHERE r.ExperimentId = @ExperimentId
            ORDER BY r.StartedUtc DESC, m.SequenceNumber DESC;
            """, connection);
        command.Parameters.Add("@Latest", SqlDbType.Int).Value = latest;
        command.Parameters.Add("@ExperimentId", SqlDbType.NVarChar, 64).Value = experimentId;
        var records = new List<BenchmarkResourceRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new BenchmarkResourceRecord
            {
                RunId = reader.GetString(0),
                ExperimentId = reader.GetString(1),
                Variant = reader.GetString(2),
                SequenceNumber = reader.GetInt32(3),
                RequestedMemoryKb = ReadNullableInt64(reader, 4),
                GrantedMemoryKb = ReadNullableInt64(reader, 5),
                UsedMemoryKb = ReadNullableInt64(reader, 6),
                TempDbPages = ReadNullableInt64(reader, 7),
                TempDbAllocatedPages = ReadNullableInt64(reader, 8),
                WorktableLogicalReads = ReadNullableInt64(reader, 9),
                DegreeOfParallelism = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                UsedParallelPlan = reader.IsDBNull(11) ? null : reader.GetBoolean(11),
                ParallelOperators = reader.IsDBNull(12) ? null : reader.GetInt32(12)
            });
        }

        return records;
    }

    public async Task<IReadOnlyList<ExperimentVersionRecord>> GetExperimentVersionsAsync(
        string experimentId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT ExperimentId, ExperimentHash, CreatedUtc, DefinitionSnapshot
            FROM dbo.ExperimentVersions WHERE ExperimentId = @ExperimentId ORDER BY CreatedUtc DESC;
            """, connection);
        command.Parameters.Add("@ExperimentId", SqlDbType.NVarChar, 64).Value = experimentId;
        var versions = new List<ExperimentVersionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(new ExperimentVersionRecord
            {
                ExperimentId = reader.GetString(0),
                ExperimentHash = reader.GetString(1).Trim(),
                CreatedUtc = reader.GetDateTimeOffset(2),
                DefinitionSnapshot = reader.GetString(3)
            });
        }

        return versions;
    }

    private static async Task<long> InsertEnvironmentAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        EnvironmentSummary environment,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            INSERT dbo.Environments (SqlServerVersion, SqlServerEdition, DotnetVersion, RunnerVersion, MachineName)
            VALUES (@SqlServerVersion, @SqlServerEdition, @DotnetVersion, @RunnerVersion, @MachineName);
            SELECT CONVERT(bigint, SCOPE_IDENTITY());
            """, connection, transaction);
        command.Parameters.Add("@SqlServerVersion", SqlDbType.NVarChar, 512).Value = environment.SqlServerVersion;
        command.Parameters.Add("@SqlServerEdition", SqlDbType.NVarChar, 256).Value = environment.SqlServerEdition;
        command.Parameters.Add("@DotnetVersion", SqlDbType.NVarChar, 64).Value = environment.DotnetVersion;
        command.Parameters.Add("@RunnerVersion", SqlDbType.NVarChar, 64).Value = environment.RunnerVersion;
        command.Parameters.Add("@MachineName", SqlDbType.NVarChar, 256).Value = environment.MachineName;
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task InsertMetricsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string runId,
        string variant,
        IEnumerable<BenchmarkRunRecord> runs,
        CancellationToken cancellationToken)
    {
        foreach (var run in runs)
        {
            await ExecuteAsync(connection, transaction, """
                INSERT dbo.BenchmarkMetrics (
                    RunId, Variant, SequenceNumber, DurationMs, RowsReturned,
                    LogicalReads, PhysicalReads, ScanCount, CpuTimeMs, SqlElapsedTimeMs,
                    RequestedMemoryKb, GrantedMemoryKb, UsedMemoryKb, TempDbPages, TempDbAllocatedPages,
                    WorktableLogicalReads, DegreeOfParallelism, UsedParallelPlan, ParallelOperators)
                VALUES (
                    @RunId, @Variant, @SequenceNumber, @DurationMs, @RowsReturned,
                    @LogicalReads, @PhysicalReads, @ScanCount, @CpuTimeMs, @SqlElapsedTimeMs,
                    @RequestedMemoryKb, @GrantedMemoryKb, @UsedMemoryKb, @TempDbPages, @TempDbAllocatedPages,
                    @WorktableLogicalReads, @DegreeOfParallelism, @UsedParallelPlan, @ParallelOperators);
                """,
                cancellationToken,
                ("@RunId", SqlDbType.VarChar, 32, runId),
                ("@Variant", SqlDbType.NVarChar, 16, variant),
                ("@SequenceNumber", SqlDbType.Int, 0, run.SequenceNumber),
                ("@DurationMs", SqlDbType.Decimal, 0, run.DurationMs),
                ("@RowsReturned", SqlDbType.BigInt, 0, run.RowsReturned),
                ("@LogicalReads", SqlDbType.BigInt, 0, (object?)run.LogicalReads ?? DBNull.Value),
                ("@PhysicalReads", SqlDbType.BigInt, 0, (object?)run.PhysicalReads ?? DBNull.Value),
                ("@ScanCount", SqlDbType.BigInt, 0, (object?)run.ScanCount ?? DBNull.Value),
                ("@CpuTimeMs", SqlDbType.Decimal, 0, (object?)run.CpuTimeMs ?? DBNull.Value),
                ("@SqlElapsedTimeMs", SqlDbType.Decimal, 0, (object?)run.SqlElapsedTimeMs ?? DBNull.Value),
                ("@RequestedMemoryKb", SqlDbType.BigInt, 0, (object?)run.RequestedMemoryKb ?? DBNull.Value),
                ("@GrantedMemoryKb", SqlDbType.BigInt, 0, (object?)run.GrantedMemoryKb ?? DBNull.Value),
                ("@UsedMemoryKb", SqlDbType.BigInt, 0, (object?)run.UsedMemoryKb ?? DBNull.Value),
                ("@TempDbPages", SqlDbType.BigInt, 0, (object?)run.TempDbPages ?? DBNull.Value),
                ("@TempDbAllocatedPages", SqlDbType.BigInt, 0, (object?)run.TempDbAllocatedPages ?? DBNull.Value),
                ("@WorktableLogicalReads", SqlDbType.BigInt, 0, (object?)run.WorktableLogicalReads ?? DBNull.Value),
                ("@DegreeOfParallelism", SqlDbType.Int, 0, (object?)run.DegreeOfParallelism ?? DBNull.Value),
                ("@UsedParallelPlan", SqlDbType.Bit, 0, (object?)run.UsedParallelPlan ?? DBNull.Value),
                ("@ParallelOperators", SqlDbType.Int, 0, (object?)run.ParallelOperators ?? DBNull.Value));
        }
    }

    private static Task InsertPlanAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string runId,
        string variant,
        ExecutionPlanArtifact? plan,
        CancellationToken cancellationToken)
    {
        if (plan is null)
        {
            return Task.CompletedTask;
        }

        return ExecuteAsync(connection, transaction, """
            INSERT dbo.ExecutionPlans (
                RunId, Variant, PlanHash, EstimatedCost, EstimatedRows, EstimatedSubtreeCost,
                OperatorsJson, OperatorOccurrencesJson, PlanXml)
            VALUES (
                @RunId, @Variant, @PlanHash, @EstimatedCost, @EstimatedRows, @EstimatedSubtreeCost,
                @OperatorsJson, @OperatorOccurrencesJson, @PlanXml);
            """,
            cancellationToken,
            ("@RunId", SqlDbType.VarChar, 32, runId),
            ("@Variant", SqlDbType.NVarChar, 16, variant),
            ("@PlanHash", SqlDbType.Char, 64, plan.PlanHash),
            ("@EstimatedCost", SqlDbType.Decimal, 0, (object?)plan.EstimatedCost ?? DBNull.Value),
            ("@EstimatedRows", SqlDbType.Decimal, 0, (object?)plan.EstimatedRows ?? DBNull.Value),
            ("@EstimatedSubtreeCost", SqlDbType.Decimal, 0, (object?)plan.EstimatedSubtreeCost ?? DBNull.Value),
            ("@OperatorsJson", SqlDbType.NVarChar, -1, JsonSerializer.Serialize(plan.Operators, SerializerOptions)),
            ("@OperatorOccurrencesJson", SqlDbType.NVarChar, -1, JsonSerializer.Serialize(plan.OperatorOccurrences, SerializerOptions)),
            ("@PlanXml", SqlDbType.NVarChar, -1, plan.PlanXml));
    }

    private static async Task InsertAdvisorAnalysisAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string runId,
        IEnumerable<AdvisorFinding> findings,
        IEnumerable<AdvisorRecommendation> recommendations,
        CancellationToken cancellationToken)
    {
        foreach (var finding in findings)
        {
            await ExecuteAsync(connection, transaction, """
                INSERT dbo.AdvisorFindings (RunId, RuleName,  Finding, Evidence, Severity)
                VALUES (@RunId, @Rule, @Finding, @Evidence, @Severity);
                """,
                cancellationToken,
                ("@RunId", SqlDbType.VarChar, 32, runId),
                ("@Rule", SqlDbType.NVarChar, 64, finding.Rule),
                ("@Finding", SqlDbType.NVarChar, 512, finding.Finding),
                ("@Evidence", SqlDbType.NVarChar, -1, finding.Evidence),
                ("@Severity", SqlDbType.NVarChar, 32, finding.Severity));
        }

        foreach (var recommendation in recommendations)
        {
            await ExecuteAsync(connection, transaction, """
                INSERT dbo.AdvisorRecommendations (RunId, Recommendation, RuleName,  Confidence)
                VALUES (@RunId, @Recommendation, @Rule, @Confidence);
                """,
                cancellationToken,
                ("@RunId", SqlDbType.VarChar, 32, runId),
                ("@Recommendation", SqlDbType.NVarChar, 1000, recommendation.Recommendation),
                ("@Rule", SqlDbType.NVarChar, 64, recommendation.Rule),
                ("@Confidence", SqlDbType.Decimal, 0, recommendation.Confidence));
        }
    }

    private static async Task ExecuteAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, SqlDbType Type, int Size, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            var sqlParameter = command.Parameters.Add(parameter.Name, parameter.Type, parameter.Size);
            sqlParameter.Value = parameter.Value;
            if (parameter.Type == SqlDbType.Decimal)
            {
                sqlParameter.Precision = 28;
                sqlParameter.Scale = parameter.Name.Contains("Cost", StringComparison.Ordinal) ? (byte)12 : (byte)8;
            }
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static decimal? ReadNullableDecimal(SqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static long? ReadNullableInt64(SqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}