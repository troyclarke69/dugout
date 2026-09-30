DROP INDEX IF EXISTS IX_Orders_OrderDate
ON dbo.Orders;

CREATE INDEX IX_Orders_OrderDate
ON dbo.Orders(OrderDate)
INCLUDE (TotalAmount);
