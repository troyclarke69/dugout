DECLARE @HotCustomerId int = 1;
DECLARE @ColdCustomerId int = 50000;
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.P6SkewedOrders WHERE CustomerId = @HotCustomerId) AS HotRows,
    (SELECT COUNT_BIG(*) FROM dbo.P6SkewedOrders WHERE CustomerId = @ColdCustomerId) AS ColdRows
OPTION (RECOMPILE);
