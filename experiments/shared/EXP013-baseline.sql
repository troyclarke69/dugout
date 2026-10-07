SELECT SUM(GroupTotal) AS TotalAmount
FROM (SELECT CustomerId % 100 AS CustomerBucket, SUM(TotalAmount) AS GroupTotal FROM dbo.Orders GROUP BY CustomerId % 100) AS Buckets
OPTION (HASH GROUP);
