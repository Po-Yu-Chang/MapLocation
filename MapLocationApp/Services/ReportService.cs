using System.Text.Json;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    public interface IReportService
    {
        Task<CheckInReport> GenerateDailyReportAsync(DateTime date);
        Task<CheckInReport> GenerateWeeklyReportAsync(DateTime weekStart);
        Task<CheckInReport> GenerateMonthlyReportAsync(int year, int month);
        Task<CheckInReport> GenerateCustomReportAsync(DateTime startDate, DateTime endDate);
        Task<List<CheckInStatistics>> GetCheckInStatisticsAsync(DateTime startDate, DateTime endDate);
        Task<List<LocationHeatmap>> GetLocationHeatmapDataAsync(DateTime startDate, DateTime endDate);
        Task<bool> ExportReportAsync(CheckInReport report, ExportFormat format, string filePath);
        Task<CheckInTrend> GetCheckInTrendAsync(DateTime startDate, DateTime endDate);
        Task<List<FrequentLocation>> GetFrequentLocationsAsync(DateTime startDate, DateTime endDate, int topCount = 10);
        Task<WorkTimeAnalysis> GetWorkTimeAnalysisAsync(DateTime startDate, DateTime endDate);
    }

    public class ReportService : IReportService
    {
        private readonly CheckInStorageService _checkInStorage;

        public ReportService(CheckInStorageService checkInStorage)
        {
            _checkInStorage = checkInStorage;
        }

        public async Task<CheckInReport> GenerateDailyReportAsync(DateTime date)
        {
            var startDate = date.Date;
            var endDate = startDate.AddDays(1).AddTicks(-1);
            
            var checkIns = await _checkInStorage.GetAllCheckInRecordsAsync();
            var dailyCheckIns = checkIns.Where(c => c.CheckInTime >= startDate && c.CheckInTime <= endDate).ToList();

            return CreateReportAsync("每日報表", startDate, endDate, dailyCheckIns);
        }

        public async Task<CheckInReport> GenerateWeeklyReportAsync(DateTime weekStart)
        {
            var startDate = weekStart.Date;
            var endDate = startDate.AddDays(7).AddTicks(-1);
            
            var checkIns = await _checkInStorage.GetAllCheckInRecordsAsync();
            var weeklyCheckIns = checkIns.Where(c => c.CheckInTime >= startDate && c.CheckInTime <= endDate).ToList();

            return CreateReportAsync("每週報表", startDate, endDate, weeklyCheckIns);
        }

        public async Task<CheckInReport> GenerateMonthlyReportAsync(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddTicks(-1);
            
            var checkIns = await _checkInStorage.GetAllCheckInRecordsAsync();
            var monthlyCheckIns = checkIns.Where(c => c.CheckInTime >= startDate && c.CheckInTime <= endDate).ToList();

            return CreateReportAsync("每月報表", startDate, endDate, monthlyCheckIns);
        }

        public async Task<CheckInReport> GenerateCustomReportAsync(DateTime startDate, DateTime endDate)
        {
            var checkIns = await _checkInStorage.GetAllCheckInRecordsAsync();
            var customCheckIns = checkIns.Where(c => c.CheckInTime >= startDate && c.CheckInTime <= endDate).ToList();

            return CreateReportAsync("自訂報表", startDate, endDate, customCheckIns);
        }

        public async Task<List<CheckInStatistics>> GetCheckInStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            var checkIns = await _checkInStorage.GetAllCheckInRecordsAsync();
            var filteredCheckIns = checkIns.Where(c => c.CheckInTime >= startDate && c.CheckInTime <= endDate).ToList();

            var statistics = new List<CheckInStatistics>();

            // 按日期分組統計
            var dailyGroups = filteredCheckIns.GroupBy(c => c.CheckInTime.Date);
            
            foreach (var group in dailyGroups)
            {
                var dailyCheckIns = group.ToList();
                var stat = new CheckInStatistics
                {
                    Date = group.Key,
                    TotalCheckIns = dailyCheckIns.Count,
                    UniqueLocations = dailyCheckIns.Select(c => $"{c.Latitude:F6},{c.Longitude:F6}").Distinct().Count(),
                    FirstCheckIn = dailyCheckIns.Min(c => c.CheckInTime),
                    LastCheckIn = dailyCheckIns.Max(c => c.CheckInTime),
                    AverageAccuracy = 10.0, // 預設精確度
                    CheckInsWithNotes = dailyCheckIns.Count(c => !string.IsNullOrEmpty(c.Notes)),
                    CheckInsWithPhotos = 0 // CheckInRecord 沒有 PhotoPath
                };

                // 計算活動時間
                if (dailyCheckIns.Count > 1)
                {
                    var timeSpan = stat.LastCheckIn - stat.FirstCheckIn;
                    stat.ActiveTimeHours = timeSpan.TotalHours;
                }

                statistics.Add(stat);
            }

            return statistics.OrderBy(s => s.Date).ToList();
        }

        public async Task<List<LocationHeatmap>> GetLocationHeatmapDataAsync(DateTime startDate, DateTime endDate)
        {
            var checkIns = await _checkInStorage.GetAllCheckInRecordsAsync();
            var filteredCheckIns = checkIns.Where(c => c.CheckInTime >= startDate && c.CheckInTime <= endDate).ToList();

            // 將位置按精度分組（約 100 公尺精度）
            var precision = 0.001; // 約 100 公尺
            var locationGroups = filteredCheckIns.GroupBy(c => new
            {
                Lat = Math.Round(c.Latitude / precision) * precision,
                Lng = Math.Round(c.Longitude / precision) * precision
            });

            var heatmapData = new List<LocationHeatmap>();

            foreach (var group in locationGroups)
            {
                var checkInList = group.ToList();
                var heatmapPoint = new LocationHeatmap
                {
                    Latitude = group.Key.Lat,
                    Longitude = group.Key.Lng,
                    CheckInCount = checkInList.Count,
                    Intensity = CalculateIntensity(checkInList.Count, filteredCheckIns.Count),
                    AverageTime = checkInList.Average(c => c.CheckInTime.TimeOfDay.TotalHours),
                    LocationName = checkInList.FirstOrDefault()?.GeofenceName ?? "未知位置"
                };

                heatmapData.Add(heatmapPoint);
            }

            return heatmapData.OrderByDescending(h => h.CheckInCount).ToList();
        }

        public async Task<bool> ExportReportAsync(CheckInReport report, ExportFormat format, string filePath)
        {
            try
            {
                switch (format)
                {
                    case ExportFormat.Json:
                        return await ExportToJsonAsync(report, filePath);
                    case ExportFormat.Csv:
                        return await ExportToCsvAsync(report, filePath);
                    case ExportFormat.Html:
                        return await ExportToHtmlAsync(report, filePath);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Export error: {ex.Message}");
                return false;
            }
        }

        public async Task<CheckInTrend> GetCheckInTrendAsync(DateTime startDate, DateTime endDate)
        {
            var checkIns = await _checkInStorage.GetAllCheckInRecordsAsync();
            var filteredCheckIns = checkIns.Where(c => c.CheckInTime >= startDate && c.CheckInTime <= endDate).ToList();

            var trend = new CheckInTrend
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalDays = (endDate - startDate).Days + 1
            };

            // 計算每日打卡數量
            var dailyCheckIns = filteredCheckIns.GroupBy(c => c.CheckInTime.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            trend.DailyCheckInCounts = new List<DailyCheckInCount>();
            
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var count = dailyCheckIns.TryGetValue(date, out var dailyCount) ? dailyCount : 0;
                trend.DailyCheckInCounts.Add(new DailyCheckInCount
                {
                    Date = date,
                    Count = count
                });
            }

            // 計算趨勢統計
            var counts = trend.DailyCheckInCounts.Select(d => d.Count).ToList();
            trend.AverageCheckInsPerDay = counts.Average();
            trend.MaxCheckInsPerDay = counts.Max();
            trend.MinCheckInsPerDay = counts.Min();
            trend.TotalCheckIns = counts.Sum();

            // 計算活躍天數（有打卡的天數）
            trend.ActiveDays = counts.Count(c => c > 0);
            trend.ActivityRate = (double)trend.ActiveDays / trend.TotalDays * 100;

            return trend;
        }

        public async Task<List<FrequentLocation>> GetFrequentLocationsAsync(DateTime startDate, DateTime endDate, int topCount = 10)
        {
            var checkIns = await _checkInStorage.GetAllCheckInRecordsAsync();
            var filteredCheckIns = checkIns.Where(c => c.CheckInTime >= startDate && c.CheckInTime <= endDate).ToList();

            // 按位置名稱分組（如果沒有名稱則按座標）
            var locationGroups = filteredCheckIns.GroupBy(c => 
                !string.IsNullOrEmpty(c.GeofenceName) ? c.GeofenceName : $"{c.Latitude:F4},{c.Longitude:F4}");

            var frequentLocations = new List<FrequentLocation>();

            foreach (var group in locationGroups)
            {
                var locationCheckIns = group.ToList();
                var frequentLocation = new FrequentLocation
                {
                    LocationName = group.Key,
                    CheckInCount = locationCheckIns.Count,
                    Latitude = locationCheckIns.Average(c => c.Latitude),
                    Longitude = locationCheckIns.Average(c => c.Longitude),
                    FirstVisit = locationCheckIns.Min(c => c.CheckInTime),
                    LastVisit = locationCheckIns.Max(c => c.CheckInTime),
                    AverageTimeSpent = CalculateAverageTimeSpent(locationCheckIns),
                    TotalTimeSpent = CalculateTotalTimeSpent(locationCheckIns)
                };

                frequentLocations.Add(frequentLocation);
            }

            return frequentLocations.OrderByDescending(l => l.CheckInCount)
                                  .Take(topCount)
                                  .ToList();
        }

        public async Task<WorkTimeAnalysis> GetWorkTimeAnalysisAsync(DateTime startDate, DateTime endDate)
        {
            var checkIns = await _checkInStorage.GetAllCheckInRecordsAsync();
            var filteredCheckIns = checkIns.Where(c => c.CheckInTime >= startDate && c.CheckInTime <= endDate).ToList();

            var analysis = new WorkTimeAnalysis
            {
                StartDate = startDate,
                EndDate = endDate
            };

            // 按小時分組分析
            var hourlyGroups = filteredCheckIns.GroupBy(c => c.CheckInTime.Hour);
            analysis.HourlyDistribution = new Dictionary<int, int>();

            for (int hour = 0; hour < 24; hour++)
            {
                var count = hourlyGroups.FirstOrDefault(g => g.Key == hour)?.Count() ?? 0;
                analysis.HourlyDistribution[hour] = count;
            }

            // 找出最活躍的時段
            analysis.PeakHour = analysis.HourlyDistribution.OrderByDescending(kvp => kvp.Value).First().Key;
            analysis.PeakHourCheckIns = analysis.HourlyDistribution[analysis.PeakHour];

            // 計算工作日 vs 週末
            var weekdayCheckIns = filteredCheckIns.Where(c => c.CheckInTime.DayOfWeek >= DayOfWeek.Monday && 
                                                             c.CheckInTime.DayOfWeek <= DayOfWeek.Friday).ToList();
            var weekendCheckIns = filteredCheckIns.Where(c => c.CheckInTime.DayOfWeek == DayOfWeek.Saturday || 
                                                             c.CheckInTime.DayOfWeek == DayOfWeek.Sunday).ToList();

            analysis.WeekdayCheckIns = weekdayCheckIns.Count;
            analysis.WeekendCheckIns = weekendCheckIns.Count;
            analysis.WeekdayToWeekendRatio = weekendCheckIns.Count > 0 ? 
                (double)weekdayCheckIns.Count / weekendCheckIns.Count : double.PositiveInfinity;

            return analysis;
        }

        // 私有輔助方法
        private CheckInReport CreateReportAsync(string title, DateTime startDate, DateTime endDate, List<CheckInRecord> checkIns)
        {
            var report = new CheckInReport
            {
                Id = Guid.NewGuid().ToString(),
                Title = title,
                StartDate = startDate,
                EndDate = endDate,
                GeneratedDate = DateTime.Now,
                TotalCheckIns = checkIns.Count,
                CheckIns = checkIns.OrderBy(c => c.CheckInTime).ToList()
            };

            if (checkIns.Any())
            {
                report.UniqueLocations = checkIns.Select(c => $"{c.Latitude:F6},{c.Longitude:F6}").Distinct().Count();
                report.AverageAccuracy = 10.0; // 預設精確度
                report.CheckInsWithNotes = checkIns.Count(c => !string.IsNullOrEmpty(c.Notes));
                report.CheckInsWithPhotos = 0; // CheckInRecord 沒有 PhotoPath
                
                // 計算最遠距離
                report.MaxDistanceFromFirst = CalculateMaxDistance(checkIns);
            }

            return report;
        }

        private double CalculateIntensity(int checkInCount, int totalCheckIns)
        {
            if (totalCheckIns == 0) return 0;
            return (double)checkInCount / totalCheckIns * 100;
        }

        private TimeSpan CalculateAverageTimeSpent(List<CheckInRecord> locationCheckIns)
        {
            // 簡化計算：假設連續打卡之間的時間就是停留時間
            if (locationCheckIns.Count < 2) return TimeSpan.Zero;

            var orderedCheckIns = locationCheckIns.OrderBy(c => c.CheckInTime).ToList();
            var totalTime = TimeSpan.Zero;
            var sessions = 0;

            for (int i = 1; i < orderedCheckIns.Count; i++)
            {
                var timeDiff = orderedCheckIns[i].CheckInTime - orderedCheckIns[i - 1].CheckInTime;
                if (timeDiff.TotalHours <= 4) // 假設超過 4 小時就是不同的工作階段
                {
                    totalTime = totalTime.Add(timeDiff);
                    sessions++;
                }
            }

            return sessions > 0 ? TimeSpan.FromTicks(totalTime.Ticks / sessions) : TimeSpan.Zero;
        }

        private TimeSpan CalculateTotalTimeSpent(List<CheckInRecord> locationCheckIns)
        {
            if (locationCheckIns.Count < 2) return TimeSpan.Zero;

            var orderedCheckIns = locationCheckIns.OrderBy(c => c.CheckInTime).ToList();
            return orderedCheckIns.Last().CheckInTime - orderedCheckIns.First().CheckInTime;
        }

        private double CalculateMaxDistance(List<CheckInRecord> checkIns)
        {
            if (checkIns.Count < 2) return 0;

            var firstCheckIn = checkIns.First();
            var maxDistance = 0.0;

            foreach (var checkIn in checkIns.Skip(1))
            {
                var distance = CalculateDistance(firstCheckIn.Latitude, firstCheckIn.Longitude,
                                               checkIn.Latitude, checkIn.Longitude);
                if (distance > maxDistance)
                    maxDistance = distance;
            }

            return maxDistance;
        }

        private double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            const double earthRadius = 6371000; // 地球半徑（公尺）
            
            var lat1Rad = lat1 * Math.PI / 180;
            var lat2Rad = lat2 * Math.PI / 180;
            var deltaLatRad = (lat2 - lat1) * Math.PI / 180;
            var deltaLngRad = (lng2 - lng1) * Math.PI / 180;

            var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return earthRadius * c;
        }

        // 匯出方法
        private async Task<bool> ExportToJsonAsync(CheckInReport report, string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(report, options);
                await File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ExportToCsvAsync(CheckInReport report, string filePath)
        {
            try
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("CheckInTime,Latitude,Longitude,GeofenceName,Notes");

                foreach (var checkIn in report.CheckIns)
                {
                    csv.AppendLine($"{checkIn.CheckInTime:yyyy-MM-dd HH:mm:ss},{checkIn.Latitude},{checkIn.Longitude}," +
                                 $"\"{checkIn.GeofenceName ?? ""}\",\"{checkIn.Notes ?? ""}\"");
                }

                await File.WriteAllTextAsync(filePath, csv.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ExportToHtmlAsync(CheckInReport report, string filePath)
        {
            try
            {
                var html = GenerateHtmlReport(report);
                await File.WriteAllTextAsync(filePath, html);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateHtmlReport(CheckInReport report)
        {
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>{report.Title}</title>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ background-color: #f5f5f5; padding: 20px; margin-bottom: 20px; }}
        .summary {{ margin-bottom: 30px; }}
        .summary-item {{ display: inline-block; margin-right: 30px; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>{report.Title}</h1>
        <p>報表期間: {report.StartDate:yyyy-MM-dd} 至 {report.EndDate:yyyy-MM-dd}</p>
        <p>產生時間: {report.GeneratedDate:yyyy-MM-dd HH:mm:ss}</p>
    </div>
    
    <div class='summary'>
        <h2>摘要統計</h2>
        <div class='summary-item'><strong>總打卡次數:</strong> {report.TotalCheckIns}</div>
        <div class='summary-item'><strong>不重複位置:</strong> {report.UniqueLocations}</div>
        <div class='summary-item'><strong>平均精確度:</strong> {report.AverageAccuracy:F1}m</div>
        <div class='summary-item'><strong>包含備註:</strong> {report.CheckInsWithNotes}</div>
        <div class='summary-item'><strong>包含照片:</strong> {report.CheckInsWithPhotos}</div>
    </div>
    
    <h2>打卡記錄</h2>
    <table>
        <tr>
            <th>時間</th>
            <th>位置</th>
            <th>座標</th>
            <th>備註</th>
            <th>精確度</th>
        </tr>";

            foreach (var checkIn in report.CheckIns)
            {
                html += $@"
        <tr>
            <td>{checkIn.CheckInTime:yyyy-MM-dd HH:mm:ss}</td>
            <td>{checkIn.GeofenceName ?? "未知位置"}</td>
            <td>{checkIn.Latitude:F6}, {checkIn.Longitude:F6}</td>
            <td>{checkIn.Notes ?? ""}</td>
            <td>10.0m</td>
        </tr>";
            }

            html += @"
    </table>
</body>
</html>";

            return html;
        }
    }

    // 資料模型
    public class CheckInReport
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalCheckIns { get; set; }
        public int UniqueLocations { get; set; }
        public double AverageAccuracy { get; set; }
        public int CheckInsWithNotes { get; set; }
        public int CheckInsWithPhotos { get; set; }
        public double MaxDistanceFromFirst { get; set; }
        public List<CheckInRecord> CheckIns { get; set; } = new();
    }

    public class CheckInStatistics
    {
        public DateTime Date { get; set; }
        public int TotalCheckIns { get; set; }
        public int UniqueLocations { get; set; }
        public DateTime FirstCheckIn { get; set; }
        public DateTime LastCheckIn { get; set; }
        public double ActiveTimeHours { get; set; }
        public double AverageAccuracy { get; set; }
        public int CheckInsWithNotes { get; set; }
        public int CheckInsWithPhotos { get; set; }
    }

    public class LocationHeatmap
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int CheckInCount { get; set; }
        public double Intensity { get; set; }
        public double AverageTime { get; set; }
        public string LocationName { get; set; }
    }

    public class CheckInTrend
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }
        public int ActiveDays { get; set; }
        public double ActivityRate { get; set; }
        public double AverageCheckInsPerDay { get; set; }
        public int MaxCheckInsPerDay { get; set; }
        public int MinCheckInsPerDay { get; set; }
        public int TotalCheckIns { get; set; }
        public List<DailyCheckInCount> DailyCheckInCounts { get; set; } = new();
    }

    public class DailyCheckInCount
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class FrequentLocation
    {
        public string LocationName { get; set; }
        public int CheckInCount { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime FirstVisit { get; set; }
        public DateTime LastVisit { get; set; }
        public TimeSpan AverageTimeSpent { get; set; }
        public TimeSpan TotalTimeSpent { get; set; }
    }

    public class WorkTimeAnalysis
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Dictionary<int, int> HourlyDistribution { get; set; } = new();
        public int PeakHour { get; set; }
        public int PeakHourCheckIns { get; set; }
        public int WeekdayCheckIns { get; set; }
        public int WeekendCheckIns { get; set; }
        public double WeekdayToWeekendRatio { get; set; }
    }

    public enum ExportFormat
    {
        Json,
        Csv,
        Html
    }
}