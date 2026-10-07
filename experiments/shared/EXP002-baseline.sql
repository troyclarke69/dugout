SELECT SUM(TotalAmount) AS TotalAmount
FROM dbo.Orders WITH (INDEX(IX_P6_Customer_Narrow))
WHERE CustomerId BETWEEN 1 AND 500;
