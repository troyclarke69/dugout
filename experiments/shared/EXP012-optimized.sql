SELECT SUM(DailyTotal) AS TotalAmount
FROM (SELECT OrderDate, SUM(TotalAmount) AS DailyTotal FROM dbo.Orders GROUP BY OrderDate) AS DailyGroups
OPTION (ORDER GROUP);
