IF DB_ID('SalesLab') IS NULL
BEGIN
    CREATE DATABASE SalesLab;
END;
GO

USE master;
GO
ALTER DATABASE SalesLab SET RECOVERY SIMPLE;
GO

USE SalesLab;
GO

IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Orders;
END;
GO

CREATE TABLE dbo.Orders
(
    OrderId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CustomerId INT NOT NULL,
    OrderDate DATETIME2(3) NOT NULL,
    TotalAmount DECIMAL(19,4) NOT NULL
);
GO

WITH NumberSequence AS
(
    SELECT TOP (1000000)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
INSERT INTO dbo.Orders (CustomerId, OrderDate, TotalAmount)
SELECT
    ((n - 1) % 50000) + 1 AS CustomerId,
    DATEADD(day, ((n - 1) % 1826), CAST('2022-01-01' AS datetime2(3))) AS OrderDate,
    CAST((((n * 17) % 1000000) + 1) / 100.0 AS decimal(19,4)) AS TotalAmount
FROM NumberSequence;
GO

UPDATE STATISTICS dbo.Orders WITH FULLSCAN;
GO
