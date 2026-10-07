SELECT
    (SELECT COUNT_BIG(*) FROM dbo.P6SkewedOrders WHERE CustomerId = 1) AS HotRows,
    (SELECT COUNT_BIG(*) FROM dbo.P6SkewedOrders WHERE CustomerId = 50000) AS ColdRows;
