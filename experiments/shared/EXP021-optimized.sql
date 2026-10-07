SELECT SUM(CONVERT(decimal(38,0), OrderId) * CONVERT(decimal(38,0), 1000000 - OrderId + 1)) AS RunningTotalSum
FROM dbo.Orders;
