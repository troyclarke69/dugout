SELECT SUM(GroupTotal) AS TotalAmount
FROM (SELECT CustomerId, SUM(TotalAmount) AS GroupTotal FROM dbo.Orders GROUP BY CustomerId) AS CustomerTotals
OPTION (ORDER GROUP);
