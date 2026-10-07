WITH DateRange AS (
    SELECT TotalAmount
    FROM dbo.Orders
    WHERE OrderDate >= '2025-01-01' AND OrderDate < '2026-01-01'
)
SELECT SUM(TotalAmount) AS TotalAmount FROM DateRange;
