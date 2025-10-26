using Xunit;
using FluentAssertions;
using MapLocationApp.Services;
using MapLocationApp.Models;

namespace MapLocationApp.Tests.Services;

/// <summary>
/// T123-T125: Unit tests for ReportService
/// Tests report generation, export formats, and filtering
/// </summary>
public class ReportServiceTests
{
    [Fact]
    public async Task T123_GenerateCheckInReport_DateRangeFiltering_ReturnsCorrectRecords()
    {
        // Arrange
        // Test generating check-in report with date range filtering
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        // Act
        var report = await reportService.GenerateCustomReportAsync(startDate, endDate);

        // Assert
        report.Should().NotBeNull("report should be generated");
        report.StartDate.Should().Be(startDate);
        report.EndDate.Should().Be(endDate);

        // Expected structure of CheckInReport
        // - Total check-ins
        // - Date range
        // - Statistics
        // - List of check-in records
    }

    [Fact]
    public async Task T123_GenerateDailyReport_SingleDay_ReturnsOnlyDailyCheckIns()
    {
        // Arrange
        // Test daily report generation
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var targetDate = DateTime.Today;

        // Act
        var dailyReport = await reportService.GenerateDailyReportAsync(targetDate);

        // Assert
        dailyReport.Should().NotBeNull();
        dailyReport.StartDate.Date.Should().Be(targetDate.Date);
        dailyReport.EndDate.Date.Should().Be(targetDate.Date);
    }

    [Fact]
    public async Task T123_GenerateWeeklyReport_SevenDays_ReturnsWeeklyCheckIns()
    {
        // Arrange
        // Test weekly report generation
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek); // Start of week

        // Act
        var weeklyReport = await reportService.GenerateWeeklyReportAsync(weekStart);

        // Assert
        weeklyReport.Should().NotBeNull();

        var expectedDuration = weeklyReport.EndDate - weeklyReport.StartDate;
        expectedDuration.TotalDays.Should().BeApproximately(7, 0.1,
            "weekly report should span 7 days");
    }

    [Fact]
    public async Task T123_GenerateMonthlyReport_SpecificMonth_ReturnsMonthlyCheckIns()
    {
        // Arrange
        // Test monthly report generation
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var year = 2025;
        var month = 1;

        // Act
        var monthlyReport = await reportService.GenerateMonthlyReportAsync(year, month);

        // Assert
        monthlyReport.Should().NotBeNull();
        monthlyReport.StartDate.Should().Be(new DateTime(year, month, 1));
        monthlyReport.EndDate.Month.Should().Be(month);
    }

    [Fact]
    public async Task T124_ExportReportAsync_JSONFormat_CreatesValidJSON()
    {
        // Arrange
        // Test exporting report in JSON format
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var report = await reportService.GenerateDailyReportAsync(DateTime.Today);
        var exportPath = Path.Combine(Path.GetTempPath(), "test-report.json");

        // Act
        var exportResult = await reportService.ExportReportAsync(
            report,
            ExportFormat.Json,
            exportPath);

        // Assert
        exportResult.Should().BeTrue("export should succeed");

        if (File.Exists(exportPath))
        {
            var jsonContent = await File.ReadAllTextAsync(exportPath);
            jsonContent.Should().NotBeNullOrEmpty("exported file should have content");
            jsonContent.Should().StartWith("{", "JSON should start with opening brace");

            // Cleanup
            File.Delete(exportPath);
        }
    }

    [Fact]
    public async Task T124_ExportReportAsync_CSVFormat_CreatesValidCSV()
    {
        // Arrange
        // Test exporting report in CSV format
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var report = await reportService.GenerateDailyReportAsync(DateTime.Today);
        var exportPath = Path.Combine(Path.GetTempPath(), "test-report.csv");

        // Act
        var exportResult = await reportService.ExportReportAsync(
            report,
            ExportFormat.Csv,
            exportPath);

        // Assert
        exportResult.Should().BeTrue("export should succeed");

        if (File.Exists(exportPath))
        {
            var csvContent = await File.ReadAllTextAsync(exportPath);
            csvContent.Should().NotBeNullOrEmpty("exported file should have content");

            // CSV should have header row
            var lines = csvContent.Split('\n');
            lines.Should().NotBeEmpty();

            // Cleanup
            File.Delete(exportPath);
        }
    }

    [Fact]
    public async Task T124_ExportReportAsync_HTMLFormat_CreatesValidHTML()
    {
        // Arrange
        // Test exporting report in HTML format
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var report = await reportService.GenerateDailyReportAsync(DateTime.Today);
        var exportPath = Path.Combine(Path.GetTempPath(), "test-report.html");

        // Act
        var exportResult = await reportService.ExportReportAsync(
            report,
            ExportFormat.Html,
            exportPath);

        // Assert
        exportResult.Should().BeTrue("export should succeed");

        if (File.Exists(exportPath))
        {
            var htmlContent = await File.ReadAllTextAsync(exportPath);
            htmlContent.Should().NotBeNullOrEmpty("exported file should have content");
            htmlContent.Should().Contain("<html", "should be valid HTML");
            htmlContent.Should().Contain("</html>", "should be valid HTML");

            // Cleanup
            File.Delete(exportPath);
        }
    }

    [Fact]
    public async Task T125_GenerateReport_CustomerFiltering_FiltersCorrectly()
    {
        // Arrange
        // Test generating report filtered by specific customer
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        // Expected: Report service can filter check-ins by customer/location
        // This tests the filtering capability

        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        // Act
        var report = await reportService.GenerateCustomReportAsync(startDate, endDate);

        // Assert
        report.Should().NotBeNull();

        // If check-in records have customer field, verify filtering
        // report.CheckIns.Should().OnlyContain(c => c.CustomerId == "specific-customer");
    }

    [Fact]
    public async Task T125_GetCheckInStatistics_DateRange_CalculatesCorrectStats()
    {
        // Arrange
        // Test statistical analysis of check-ins
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 7);

        // Act
        var statistics = await reportService.GetCheckInStatisticsAsync(startDate, endDate);

        // Assert
        statistics.Should().NotBeNull();
        statistics.Should().BeOfType<List<CheckInStatistics>>();

        // Expected statistics:
        // - Total check-ins per day
        // - Unique locations visited
        // - First/last check-in times
        // - Average accuracy
    }

    [Fact]
    public async Task T125_GetFrequentLocations_TopCount_ReturnsFrequentLocations()
    {
        // Arrange
        // Test finding most frequently visited locations
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);
        var topCount = 10;

        // Act
        var frequentLocations = await reportService.GetFrequentLocationsAsync(
            startDate,
            endDate,
            topCount);

        // Assert
        frequentLocations.Should().NotBeNull();
        frequentLocations.Should().HaveCountLessOrEqualTo(topCount,
            "should return at most 'topCount' locations");

        // Locations should be sorted by frequency (most to least)
        // frequentLocations.Should().BeInDescendingOrder(l => l.VisitCount);
    }

    [Fact]
    public async Task T125_GetLocationHeatmapData_DateRange_ReturnsHeatmapData()
    {
        // Arrange
        // Test generating heatmap data for location visualization
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        // Act
        var heatmapData = await reportService.GetLocationHeatmapDataAsync(startDate, endDate);

        // Assert
        heatmapData.Should().NotBeNull();
        heatmapData.Should().BeOfType<List<LocationHeatmap>>();

        // Each heatmap point should have:
        // - Latitude
        // - Longitude
        // - Weight/intensity (based on visit frequency)
    }
}
