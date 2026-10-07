DECLARE @CustomerId int = 42;
SELECT SUM(TotalAmount) AS TotalAmount
FROM dbo.Orders
WHERE CustomerId = @CustomerId
OPTION (RECOMPILE);
