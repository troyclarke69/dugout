WITH LatestRun AS
(
    SELECT TOP (1)
        RunId,
        StartedUtc,
        Status,
        JSON_VALUE(ResultJson, '$.schemaVersion') AS SchemaVersion
    FROM dbo.BenchmarkRuns
    WHERE ExperimentId = N'EXP001'
    ORDER BY StartedUtc DESC
)
SELECT
    r.RunId,
    r.Status,
    r.SchemaVersion,
    r.StartedUtc,
    COUNT(m.SequenceNumber) OVER () AS MetricRowCount,
    m.Variant,
    m.SequenceNumber,
    m.RequestedMemoryKb,
    m.GrantedMemoryKb,
    m.UsedMemoryKb,
    m.TempDbPages,
    m.TempDbAllocatedPages,
    m.WorktableLogicalReads,
    m.DegreeOfParallelism,
    m.UsedParallelPlan,
    m.ParallelOperators
FROM LatestRun AS r
LEFT JOIN dbo.BenchmarkMetrics AS m ON m.RunId = r.RunId
ORDER BY m.SequenceNumber;