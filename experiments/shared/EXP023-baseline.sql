SELECT SUM(RankNumber) AS RankTotal
FROM (
    SELECT ROW_NUMBER() OVER (ORDER BY OrderDate, OrderId) AS RankNumber
    FROM dbo.Orders
) AS RankedOrders;
