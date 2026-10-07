SELECT SUM(CONVERT(decimal(38,4), a.TotalAmount) + CONVERT(decimal(38,4), b.TotalAmount) + CONVERT(decimal(38,4), c.TotalAmount)) AS JoinedTotal
FROM dbo.Orders AS a
JOIN dbo.Orders AS b ON b.CustomerId = a.CustomerId
JOIN dbo.Orders AS c ON c.CustomerId = b.CustomerId
WHERE a.CustomerId BETWEEN 1 AND 3
  AND b.CustomerId BETWEEN 1 AND 3
  AND c.CustomerId BETWEEN 1 AND 3;
