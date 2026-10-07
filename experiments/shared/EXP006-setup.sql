IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'IX_P6_Customer_OrderDate')
    CREATE INDEX IX_P6_Customer_OrderDate ON dbo.Orders(CustomerId, OrderDate) INCLUDE (TotalAmount);
