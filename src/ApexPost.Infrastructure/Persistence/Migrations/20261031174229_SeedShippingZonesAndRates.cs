using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedShippingZonesAndRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Zones TABLE (Code varchar(32) NOT NULL PRIMARY KEY, Name nvarchar(100) NOT NULL);
                INSERT @Zones (Code, Name) VALUES
                    ('ZONE1', N'Zone 1'), ('ZONE2', N'Zone 2'), ('ZONE3', N'Zone 3'),
                    ('ZONE4', N'Zone 4'), ('ZONE5', N'Zone 5'), ('ZONE6', N'Zone 6'),
                    ('ZONE7', N'Zone 7'), ('ZONE8', N'Zone 8'), ('ZONE9', N'Zone 9'),
                    ('ZONE10', N'Zone 10');
                INSERT dbo.ShippingZones (Code, Name)
                SELECT source.Code, source.Name FROM @Zones AS source
                WHERE NOT EXISTS (SELECT 1 FROM dbo.ShippingZones AS target WHERE target.Code = source.Code);

                DECLARE @Rates TABLE
                (
                    ZoneCode varchar(32) NOT NULL,
                    ServiceLevel varchar(32) NOT NULL,
                    BasePrice decimal(18,2) NOT NULL,
                    PricePerKilogram decimal(18,2) NOT NULL,
                    PRIMARY KEY (ZoneCode, ServiceLevel)
                );
                INSERT @Rates (ZoneCode, ServiceLevel, BasePrice, PricePerKilogram) VALUES
                    ('ZONE1','Standard',40,20), ('ZONE1','Express',80,30), ('ZONE1','Registered',65,25),
                    ('ZONE2','Standard',42,22), ('ZONE3','Standard',44,24),
                    ('ZONE4','Standard',46,26), ('ZONE5','Standard',48,28),
                    ('ZONE6','Standard',50,30), ('ZONE7','Standard',52,32),
                    ('ZONE8','Standard',54,34), ('ZONE9','Standard',56,36),
                    ('ZONE10','Standard',58,38);
                INSERT dbo.ShippingRates (ZoneCode, ServiceLevel, BasePrice, PricePerKilogram)
                SELECT source.ZoneCode, source.ServiceLevel, source.BasePrice, source.PricePerKilogram
                FROM @Rates AS source
                WHERE NOT EXISTS
                (
                    SELECT 1 FROM dbo.ShippingRates AS target
                    WHERE target.ZoneCode = source.ZoneCode AND target.ServiceLevel = source.ServiceLevel
                );
                """);
        }

        /// <inheritdoc />
        // Reference rows are retained during rollback to avoid deleting operator edits.
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
