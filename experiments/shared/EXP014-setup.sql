IF OBJECT_ID(N'dbo.P6SkewedOrders', N'U') IS NOT NULL DROP TABLE dbo.P6SkewedOrders;
SELECT TOP (100000)
    OrderId,
    CASE WHEN OrderId <= 95000 THEN 1 ELSE CustomerId END AS CustomerId,
    OrderDate,
    TotalAmount
INTO dbo.P6SkewedOrders
FROM dbo.Orders
ORDER BY OrderId;
CREATE STATISTICS ST_P6SkewedOrders_CustomerId ON dbo.P6SkewedOrders(CustomerId) WITH FULLSCAN;
