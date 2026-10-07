IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'IX_P6_Customer_Narrow')
    CREATE INDEX IX_P6_Customer_Narrow ON dbo.Orders(CustomerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'IX_P6_Customer_Covering')
    CREATE INDEX IX_P6_Customer_Covering ON dbo.Orders(CustomerId) INCLUDE (TotalAmount);
