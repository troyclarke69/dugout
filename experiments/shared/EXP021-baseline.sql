SELECT SUM(RunningTotal) AS RunningTotalSum
FROM (
    SELECT SUM(CONVERT(decimal(38,0), OrderId)) OVER (ORDER BY OrderId ROWS UNBOUNDED PRECEDING) AS RunningTotal
    FROM dbo.Orders
) AS RunningTotals;
