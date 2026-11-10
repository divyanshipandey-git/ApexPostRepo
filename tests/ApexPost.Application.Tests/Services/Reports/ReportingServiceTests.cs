using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexPost.Application.Abstractions.Reports;
using ApexPost.Application.Reports;
using ApexPost.Application.Services.Reports;
using Moq;
using Xunit;

namespace ApexPost.Application.Tests.Services.Reports;

/// <summary>
/// Tests for ReportingService
/// </summary>
public class ReportingServiceTests
{
    private readonly Mock<IReportDataSource> _mockDataSource;
    private readonly ReportingService _service;

    public ReportingServiceTests()
    {
        _mockDataSource = new Mock<IReportDataSource>();
        _service = new ReportingService(_mockDataSource.Object);
    }

    [Fact]
    public async Task GetDailyBranchSalesAsync_ValidInput_ReturnsReport()
    {
        // Arrange
        var reportData = new ReportDailyBranchSales(
            "BR001",
            DateOnly.FromDateTime(DateTime.Today),
            10,
            1000m,
            500m);
        _mockDataSource.Setup(d => d.GetDailyBranchSalesAsync(DateOnly.FromDateTime(DateTime.Today), "BR001", default))
            .ReturnsAsync(new[] { reportData });

        // Act
        var result = await _service.GetDailyBranchSalesAsync("BR001", DateOnly.FromDateTime(DateTime.Today));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("BR001", result.BranchCode);
        Assert.Equal(10, result.TransactionCount);
        Assert.Equal(1000m, result.RetailSales);
        Assert.Equal(500m, result.ServiceSales);
        Assert.Equal(1500m, result.TotalSales);
    }

    [Fact]
    public async Task GetDailyBranchSalesAsync_NoData_ReturnsEmptyReport()
    {
        // Arrange
        _mockDataSource.Setup(d => d.GetDailyBranchSalesAsync(DateOnly.FromDateTime(DateTime.Today), "BR001", default))
            .ReturnsAsync(Array.Empty<ReportDailyBranchSales>());

        // Act
        var result = await _service.GetDailyBranchSalesAsync("BR001", DateOnly.FromDateTime(DateTime.Today));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("BR001", result.BranchCode);
        Assert.Equal(0, result.TransactionCount);
        Assert.Equal(0m, result.RetailSales);
        Assert.Equal(0m, result.ServiceSales);
        Assert.Equal(0m, result.TotalSales);
    }

    [Fact]
    public async Task GetDailyBranchSalesAsync_NullBranchCode_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetDailyBranchSalesAsync("", DateOnly.FromDateTime(DateTime.Today)));
    }

    [Fact]
    public async Task GetDailyBranchSalesAsync_NormalizesBranchCode()
    {
        // Arrange
        var reportData = new ReportDailyBranchSales(
            "BR001",
            DateOnly.FromDateTime(DateTime.Today),
            5,
            500m,
            250m);
        _mockDataSource.Setup(d => d.GetDailyBranchSalesAsync(DateOnly.FromDateTime(DateTime.Today), "BR001", default))
            .ReturnsAsync(new[] { reportData });

        // Act
        var result = await _service.GetDailyBranchSalesAsync("  br001  ", DateOnly.FromDateTime(DateTime.Today));

        // Assert
        Assert.Equal("BR001", result.BranchCode);
    }

    [Fact]
    public async Task GetDailyBranchSalesReportsAsync_ReturnsAllReports()
    {
        // Arrange
        var reports = new[]
        {
            new ReportDailyBranchSales("BR001", DateOnly.FromDateTime(DateTime.Today), 10, 1000m, 500m),
            new ReportDailyBranchSales("BR002", DateOnly.FromDateTime(DateTime.Today), 5, 500m, 250m)
        };
        _mockDataSource.Setup(d => d.GetDailyBranchSalesAsync(DateOnly.FromDateTime(DateTime.Today), null, default))
            .ReturnsAsync(reports);

        // Act
        var result = await _service.GetDailyBranchSalesReportsAsync(DateOnly.FromDateTime(DateTime.Today));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("BR001", result[0].BranchCode);
        Assert.Equal("BR002", result[1].BranchCode);
    }

    [Fact]
    public async Task GetDailyBranchSalesReportsAsync_WithBranchFilter_ReturnsFiltered()
    {
        // Arrange
        var reports = new[]
        {
            new ReportDailyBranchSales("BR001", DateOnly.FromDateTime(DateTime.Today), 10, 1000m, 500m),
            new ReportDailyBranchSales("BR002", DateOnly.FromDateTime(DateTime.Today), 5, 500m, 250m)
        };
        _mockDataSource.Setup(d => d.GetDailyBranchSalesAsync(DateOnly.FromDateTime(DateTime.Today), "BR001", default))
            .ReturnsAsync(new[] { reports[0] });

        // Act
        var result = await _service.GetDailyBranchSalesReportsAsync(DateOnly.FromDateTime(DateTime.Today), "BR001");

        // Assert
        Assert.Single(result);
        Assert.Equal("BR001", result[0].BranchCode);
    }

    [Fact]
    public void Constructor_NullDataSource_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ReportingService(null!));
    }
}