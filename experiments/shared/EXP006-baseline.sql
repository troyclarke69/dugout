SELECT SUM(TotalAmount) AS TotalAmount
FROM dbo.Orders
WHERE CONVERT(bigint, CustomerId) BETWEEN 100 AND 500
  AND YEAR(OrderDate) = 2025;
