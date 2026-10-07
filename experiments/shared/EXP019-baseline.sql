SELECT SUM(TotalAmount) AS TotalAmount
FROM dbo.Orders
OPTION (MAXDOP 1);
