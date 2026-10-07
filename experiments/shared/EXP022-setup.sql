IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'IX_P6_CustomerId')
    CREATE INDEX IX_P6_CustomerId ON dbo.Orders(CustomerId) INCLUDE (TotalAmount);
