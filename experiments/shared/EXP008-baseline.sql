SELECT SUM(CONVERT(decimal(38,4), l.TotalAmount) + CONVERT(decimal(38,4), r.TotalAmount)) AS JoinedTotal
FROM dbo.Orders AS l
JOIN dbo.Orders AS r ON r.CustomerId = l.CustomerId AND r.OrderId > l.OrderId
WHERE l.CustomerId BETWEEN 1 AND 10
  AND r.CustomerId BETWEEN 1 AND 10
OPTION (MERGE JOIN);
