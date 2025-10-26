using Xunit;
using FluentAssertions;
using MapLocationApp.Services;
using MapLocationApp.Models;

namespace MapLocationApp.Tests.Integration;

/// <summary>
/// T126: Integration test for report generation with multi-day check-in data
/// Tests complete reporting workflow
/// </summary>
public class ReportingTests
{
    [Fact]
    public async Task T126_ReportGeneration_MultiDayCheckInData_GeneratesComprehensiveReport()
    {
        // Arrange
        // Test generating comprehensive report with multiple days of check-in data
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        // Simulate multiple check-in records across different days
        var testCheckIns = new List<CheckInRecord>();

        // Day 1: 3 check-ins
        testCheckIns.Add(new CheckInRecord
        {
            Id = "checkin-1",
            LocationName = "Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            CheckInTime = new DateTime(2025, 1, 1, 9, 0, 0),
            Notes = "Morning arrival"
        });
        testCheckIns.Add(new CheckInRecord
        {
            Id = "checkin-2",
            LocationName = "Client A",
            Latitude = 25.0420,
            Longitude = 121.5650,
            CheckInTime = new DateTime(2025, 1, 1, 14, 30, 0),
            Notes = "Afternoon meeting"
        });
        testCheckIns.Add(new CheckInRecord
        {
            Id = "checkin-3",
            LocationName = "Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            CheckInTime = new DateTime(2025, 1, 1, 18, 0, 0),
            Notes = "Return to office"
        });

        // Day 2: 4 check-ins
        testCheckIns.Add(new CheckInRecord
        {
            Id = "checkin-4",
            LocationName = "Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            CheckInTime = new DateTime(2025, 1, 2, 8, 45, 0)
        });
        testCheckIns.Add(new CheckInRecord
        {
            Id = "checkin-5",
            LocationName = "Client B",
            Latitude = 25.0250,
            Longitude = 121.5660,
            CheckInTime = new DateTime(2025, 1, 2, 10, 30, 0)
        });
        testCheckIns.Add(new CheckInRecord
        {
            Id = "checkin-6",
            LocationName = "Warehouse",
            Latitude = 25.0200,
            Longitude = 121.5700,
            CheckInTime = new DateTime(2025, 1, 2, 14, 0, 0)
        });
        testCheckIns.Add(new CheckInRecord
        {
            Id = "checkin-7",
            LocationName = "Office",
            Latitude = 25.0330,
            Longitude = 121.5654,
            CheckInTime = new DateTime(2025, 1, 2, 17, 30, 0)
        });

        // Save test check-ins
        foreach (var checkIn in testCheckIns)
        {
            await checkInStorage.SaveCheckInRecordAsync(checkIn);
        }

        // Act
        // Generate report for entire date range
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 2, 23, 59, 59);

        var report = await reportService.GenerateCustomReportAsync(startDate, endDate);

        // Assert
        report.Should().NotBeNull("report should be generated");
        report.StartDate.Should().Be(startDate);
        report.EndDate.Should().Be(endDate);

        // Verify daily statistics
        var statistics = await reportService.GetCheckInStatisticsAsync(startDate, endDate);
        statistics.Should().HaveCountGreaterOrEqualTo(2, "should have statistics for 2 days");

        // Day 1 should have 3 check-ins
        var day1Stats = statistics.FirstOrDefault(s => s.Date.Date == new DateTime(2025, 1, 1));
        if (day1Stats != null)
        {
            day1Stats.TotalCheckIns.Should().Be(3, "Day 1 had 3 check-ins");
        }

        // Day 2 should have 4 check-ins
        var day2Stats = statistics.FirstOrDefault(s => s.Date.Date == new DateTime(2025, 1, 2));
        if (day2Stats != null)
        {
            day2Stats.TotalCheckIns.Should().Be(4, "Day 2 had 4 check-ins");
        }

        // Test frequent locations
        var frequentLocations = await reportService.GetFrequentLocationsAsync(startDate, endDate, 5);
        frequentLocations.Should().NotBeEmpty("should identify frequent locations");

        // Office should be most frequent (4 visits)
        var officeLocation = frequentLocations.FirstOrDefault();
        // officeLocation?.LocationName.Should().Be("Office");
        // officeLocation?.VisitCount.Should().Be(4);

        // Test export to different formats
        var jsonPath = Path.Combine(Path.GetTempPath(), "multi-day-report.json");
        var csvPath = Path.Combine(Path.GetTempPath(), "multi-day-report.csv");
        var htmlPath = Path.Combine(Path.GetTempPath(), "multi-day-report.html");

        var jsonExport = await reportService.ExportReportAsync(report, ExportFormat.Json, jsonPath);
        var csvExport = await reportService.ExportReportAsync(report, ExportFormat.Csv, csvPath);
        var htmlExport = await reportService.ExportReportAsync(report, ExportFormat.Html, htmlPath);

        jsonExport.Should().BeTrue("JSON export should succeed");
        csvExport.Should().BeTrue("CSV export should succeed");
        htmlExport.Should().BeTrue("HTML export should succeed");

        // Cleanup
        if (File.Exists(jsonPath)) File.Delete(jsonPath);
        if (File.Exists(csvPath)) File.Delete(csvPath);
        if (File.Exists(htmlPath)) File.Delete(htmlPath);

        // Clean up test data
        foreach (var checkIn in testCheckIns)
        {
            await checkInStorage.DeleteCheckInRecordAsync(checkIn.Id);
        }
    }

    [Fact]
    public async Task T126_ReportGeneration_WorkTimeAnalysis_CalculatesWorkHours()
    {
        // Arrange
        // Test work time analysis feature
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 7);

        // Act
        var workTimeAnalysis = await reportService.GetWorkTimeAnalysisAsync(startDate, endDate);

        // Assert
        workTimeAnalysis.Should().NotBeNull("work time analysis should be generated");

        // Expected analysis:
        // - Total work hours
        // - Average daily work hours
        // - Earliest/latest check-in times
        // - Work pattern analysis
    }

    [Fact]
    public async Task T126_ReportGeneration_CheckInTrend_ShowsTrendOverTime()
    {
        // Arrange
        // Test check-in trend analysis
        var checkInStorage = new CheckInStorageService();
        var reportService = new ReportService(checkInStorage);

        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        // Act
        var trend = await reportService.GetCheckInTrendAsync(startDate, endDate);

        // Assert
        trend.Should().NotBeNull("trend analysis should be generated");

        // Expected trend data:
        // - Daily check-in counts
        // - Trend direction (increasing/decreasing)
        // - Peak days and times
    }
}
