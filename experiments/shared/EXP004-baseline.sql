SELECT SUM(TotalAmount) AS TotalAmount
FROM dbo.Orders WITH (INDEX(IX_P6_Customer_Narrow))
WHERE CustomerId BETWEEN 100 AND 900;
