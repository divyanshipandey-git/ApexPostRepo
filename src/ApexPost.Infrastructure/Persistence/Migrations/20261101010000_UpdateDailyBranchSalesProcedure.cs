using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApexPostDbContext))]
[Migration("20261101010000_UpdateDailyBranchSalesProcedure")]
public partial class UpdateDailyBranchSalesProcedure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
                @ReportDate date,
                @BranchCode varchar(10) = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                IF @ReportDate IS NULL
                    THROW 51005, 'A report date is required.', 1;
                IF @BranchCode IS NOT NULL AND NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
                    THROW 51006, 'Branch code cannot be blank.', 1;

                SELECT transactionRow.BranchCode,
                       CONVERT(date, transactionRow.OccurredAtUtc) AS SaleDate,
                       COUNT_BIG(DISTINCT transactionRow.Id) AS TransactionCount,
                       COALESCE(SUM(CASE WHEN line.ItemType = 'Retail'
                            THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END), 0) AS RetailSales,
                       COALESCE(SUM(CASE WHEN line.ItemType = 'Service'
                            THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END), 0) AS ServiceSales,
                       COALESCE(SUM(line.LineTotal + line.LineTotal * tax.Rate), 0) AS TotalSales
                FROM dbo.CounterTransactions AS transactionRow
                INNER JOIN dbo.TransactionLines AS line ON line.TransactionId = transactionRow.Id
                INNER JOIN dbo.TransactionTaxRates AS tax
                    ON tax.TransactionId = line.TransactionId AND tax.Category = line.TaxCategory
                WHERE transactionRow.Status = 'Paid'
                  AND transactionRow.OccurredAtUtc >= @ReportDate
                  AND transactionRow.OccurredAtUtc < DATEADD(day, 1, @ReportDate)
                  AND line.ItemType IN ('Retail', 'Service')
                  AND (@BranchCode IS NULL OR transactionRow.BranchCode = @BranchCode)
                GROUP BY transactionRow.BranchCode, CONVERT(date, transactionRow.OccurredAtUtc);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
