SELECT SUM(TotalAmount) AS TotalAmount
FROM dbo.Orders
WHERE YEAR(OrderDate) = 2025;
