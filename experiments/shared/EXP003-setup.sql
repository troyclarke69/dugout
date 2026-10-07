IF OBJECT_ID(N'dbo.P6OrdersHeap', N'U') IS NOT NULL DROP TABLE dbo.P6OrdersHeap;
SELECT OrderId, CustomerId, OrderDate, TotalAmount
INTO dbo.P6OrdersHeap
FROM dbo.Orders;
