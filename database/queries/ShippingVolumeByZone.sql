DECLARE @Date date = CONVERT(date, SYSUTCDATETIME());
DECLARE @BranchCode varchar(10) = NULL;
DECLARE @StartUtc datetime2(7) = CONVERT(datetime2(7), @Date);
DECLARE @EndUtc datetime2(7) = DATEADD(day, 1, @StartUtc);

SELECT shipment.ZoneCode AS DestinationZone,
       shipment.ServiceLevel,
       COUNT_BIG(*) AS ShipmentCount
FROM dbo.Shipments AS shipment
WHERE shipment.CreatedAtUtc >= @StartUtc
  AND shipment.CreatedAtUtc < @EndUtc
  AND (@BranchCode IS NULL OR shipment.BranchCode = @BranchCode)
GROUP BY shipment.ZoneCode, shipment.ServiceLevel
ORDER BY shipment.ZoneCode, shipment.ServiceLevel;
