SELECT SUM(TotalAmount) AS TotalAmount
FROM dbo.Orders
WHERE CONVERT(varchar(12), CustomerId) = '25';
