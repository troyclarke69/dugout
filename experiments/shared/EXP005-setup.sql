IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'IX_P6_Orders_2025')
    CREATE INDEX IX_P6_Orders_2025 ON dbo.Orders(OrderDate) INCLUDE (TotalAmount)
    WHERE OrderDate >= '2025-01-01' AND OrderDate < '2026-01-01';
