SELECT SUM(RankNumber) AS RankTotal
FROM (
    SELECT ROW_NUMBER() OVER (ORDER BY TotalAmount, OrderId) AS RankNumber
    FROM dbo.Orders
) AS RankedOrders;
