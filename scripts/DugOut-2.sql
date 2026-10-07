select * from dbo.BenchmarkRuns 
where JSON_VALUE(ResultJson, '$.schemaVersion') = '4'
order by CompletedUtc DESC

select * from dbo.BenchmarkMetrics bm
	inner join dbo.BenchmarkRuns br
		on bm.RunId = br.RunId
where JSON_VALUE(ResultJson, '$.schemaVersion') = '4'
order by br.CompletedUtc, br.RunId, bm.SequenceNumber DESC

select top 100 * from dbo.Environments
select top 100 * from dbo.ExecutionPlans
select top 100 * from dbo.Experiments
select top 100 * from dbo.ExperimentVersions