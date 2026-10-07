SELECT SUM(TotalAmount) AS TotalAmount
FROM dbo.Orders
WHERE CustomerId BETWEEN 100 AND 500
  AND OrderDate >= '2025-01-01'
  AND OrderDate < '2026-01-01';
