SELECT SUM(DateRange.TotalAmount) AS TotalAmount
FROM (
    SELECT TotalAmount
    FROM dbo.Orders
    WHERE OrderDate >= '2025-01-01' AND OrderDate < '2026-01-01'
) AS DateRange;
