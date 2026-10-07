SELECT COUNT_BIG(*) * (COUNT_BIG(*) + 1) / 2 AS RankTotal
FROM dbo.Orders;
