using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Performance metric types for navigation
    /// </summary>
    public enum PerformanceMetricType
    {
        LocationUpdate,
        RouteCalculation,
        NavigationUpdate,
        TTSSpeech,
        TrafficQuery,
        RouteDeviation,
        Memory,
        Battery
    }

    /// <summary>
    /// Performance metric data
    /// </summary>
    public class PerformanceMetric
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public PerformanceMetricType Type { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// Performance summary statistics
    /// </summary>
    public class PerformanceSummary
    {
        public PerformanceMetricType MetricType { get; set; }
        public int SampleCount { get; set; }
        public double AverageValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double MedianValue { get; set; }
        public double P95Value { get; set; } // 95th percentile
        public TimeSpan TimeWindow { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        public string FormattedAverage => MetricType switch
        {
            PerformanceMetricType.LocationUpdate => $"{AverageValue:F1} ms",
            PerformanceMetricType.RouteCalculation => $"{AverageValue:F1} ms",
            PerformanceMetricType.NavigationUpdate => $"{AverageValue:F1} ms",
            PerformanceMetricType.TTSSpeech => $"{AverageValue:F1} ms",
            PerformanceMetricType.TrafficQuery => $"{AverageValue:F1} ms",
            PerformanceMetricType.RouteDeviation => $"{AverageValue:F1} ms",
            PerformanceMetricType.Memory => $"{AverageValue:F1} MB",
            PerformanceMetricType.Battery => $"{AverageValue:F1}%",
            _ => $"{AverageValue:F2}"
        };
    }

    /// <summary>
    /// Interface for navigation performance monitoring
    /// </summary>
    public interface INavigationPerformanceMonitor
    {
        /// <summary>
        /// Tracks performance for a specific operation
        /// </summary>
        void TrackMetric(PerformanceMetricType type, TimeSpan duration, string? description = null);

        /// <summary>
        /// Tracks a value-based metric
        /// </summary>
        void TrackValue(PerformanceMetricType type, double value, string unit, string? description = null);

        /// <summary>
        /// Gets performance summary for a specific metric type
        /// </summary>
        PerformanceSummary GetSummary(PerformanceMetricType type, TimeSpan? timeWindow = null);

        /// <summary>
        /// Gets all performance summaries
        /// </summary>
        List<PerformanceSummary> GetAllSummaries(TimeSpan? timeWindow = null);

        /// <summary>
        /// Starts tracking a performance operation
        /// </summary>
        IDisposable StartTracking(PerformanceMetricType type, string? description = null);

        /// <summary>
        /// Clears old performance data
        /// </summary>
        void ClearOldData(TimeSpan maxAge);

        /// <summary>
        /// Event fired when performance issues are detected
        /// </summary>
        event EventHandler<PerformanceAlert> PerformanceAlertTriggered;
    }

    /// <summary>
    /// Performance alert information
    /// </summary>
    public class PerformanceAlert
    {
        public PerformanceMetricType MetricType { get; set; }
        public string AlertType { get; set; } = string.Empty; // "HIGH_LATENCY", "MEMORY_USAGE", "BATTERY_DRAIN"
        public string Message { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Threshold { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Performance tracking operation
    /// </summary>
    public class PerformanceTracker : IDisposable
    {
        private readonly INavigationPerformanceMonitor _monitor;
        private readonly PerformanceMetricType _type;
        private readonly string? _description;
        private readonly Stopwatch _stopwatch;

        public PerformanceTracker(INavigationPerformanceMonitor monitor, PerformanceMetricType type, string? description)
        {
            _monitor = monitor;
            _type = type;
            _description = description;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _monitor.TrackMetric(_type, _stopwatch.Elapsed, _description);
        }
    }

    /// <summary>
    /// Navigation performance monitor implementation
    /// </summary>
    public class NavigationPerformanceMonitor : INavigationPerformanceMonitor
    {
        private readonly List<PerformanceMetric> _metrics = new();
        private readonly object _lock = new object();

        // Performance thresholds for alerts
        private readonly Dictionary<PerformanceMetricType, double> _latencyThresholds = new()
        {
            { PerformanceMetricType.LocationUpdate, 1000 }, // 1 second
            { PerformanceMetricType.RouteCalculation, 5000 }, // 5 seconds
            { PerformanceMetricType.NavigationUpdate, 500 }, // 500ms
            { PerformanceMetricType.TTSSpeech, 2000 }, // 2 seconds
            { PerformanceMetricType.TrafficQuery, 3000 }, // 3 seconds
            { PerformanceMetricType.RouteDeviation, 1000 } // 1 second
        };

        public event EventHandler<PerformanceAlert>? PerformanceAlertTriggered;

        public void TrackMetric(PerformanceMetricType type, TimeSpan duration, string? description = null)
        {
            var metric = new PerformanceMetric
            {
                Type = type,
                Duration = duration,
                Value = duration.TotalMilliseconds,
                Unit = "ms",
                Description = description ?? string.Empty
            };

            lock (_lock)
            {
                _metrics.Add(metric);
            }

            // Check for performance alerts
            CheckForAlert(type, duration.TotalMilliseconds);

            // Log the metric
            Debug.WriteLine($"Performance: {type} took {duration.TotalMilliseconds:F1}ms - {description}");
        }

        public void TrackValue(PerformanceMetricType type, double value, string unit, string? description = null)
        {
            var metric = new PerformanceMetric
            {
                Type = type,
                Value = value,
                Unit = unit,
                Description = description ?? string.Empty
            };

            lock (_lock)
            {
                _metrics.Add(metric);
            }

            // Check for alerts based on value
            if (type == PerformanceMetricType.Memory && value > 100) // 100MB threshold
            {
                TriggerAlert(type, "HIGH_MEMORY", $"Memory usage is high: {value:F1} MB", value, 100);
            }
            else if (type == PerformanceMetricType.Battery && value < 20) // 20% battery threshold
            {
                TriggerAlert(type, "LOW_BATTERY", $"Battery level is low: {value:F1}%", value, 20);
            }

            Debug.WriteLine($"Performance: {type} = {value:F1} {unit} - {description}");
        }

        public PerformanceSummary GetSummary(PerformanceMetricType type, TimeSpan? timeWindow = null)
        {
            var window = timeWindow ?? TimeSpan.FromHours(1);
            var cutoffTime = DateTime.UtcNow - window;

            List<PerformanceMetric> relevantMetrics;
            lock (_lock)
            {
                relevantMetrics = _metrics
                    .Where(m => m.Type == type && m.Timestamp >= cutoffTime)
                    .ToList();
            }

            if (!relevantMetrics.Any())
            {
                return new PerformanceSummary
                {
                    MetricType = type,
                    TimeWindow = window
                };
            }

            var values = relevantMetrics.Select(m => m.Value).OrderBy(v => v).ToList();

            return new PerformanceSummary
            {
                MetricType = type,
                SampleCount = values.Count,
                AverageValue = values.Average(),
                MinValue = values.Min(),
                MaxValue = values.Max(),
                MedianValue = GetPercentile(values, 0.5),
                P95Value = GetPercentile(values, 0.95),
                TimeWindow = window
            };
        }

        public List<PerformanceSummary> GetAllSummaries(TimeSpan? timeWindow = null)
        {
            var metricTypes = Enum.GetValues<PerformanceMetricType>();
            return metricTypes.Select(type => GetSummary(type, timeWindow)).ToList();
        }

        public IDisposable StartTracking(PerformanceMetricType type, string? description = null)
        {
            return new PerformanceTracker(this, type, description);
        }

        public void ClearOldData(TimeSpan maxAge)
        {
            var cutoffTime = DateTime.UtcNow - maxAge;
            
            lock (_lock)
            {
                _metrics.RemoveAll(m => m.Timestamp < cutoffTime);
            }

            Debug.WriteLine($"Cleared performance data older than {maxAge.TotalHours:F1} hours");
        }

        private void CheckForAlert(PerformanceMetricType type, double latencyMs)
        {
            if (_latencyThresholds.TryGetValue(type, out var threshold) && latencyMs > threshold)
            {
                TriggerAlert(type, "HIGH_LATENCY", 
                    $"{type} operation took {latencyMs:F1}ms (threshold: {threshold}ms)", 
                    latencyMs, threshold);
            }
        }

        private void TriggerAlert(PerformanceMetricType type, string alertType, string message, double value, double threshold)
        {
            var alert = new PerformanceAlert
            {
                MetricType = type,
                AlertType = alertType,
                Message = message,
                Value = value,
                Threshold = threshold
            };

            PerformanceAlertTriggered?.Invoke(this, alert);
            Debug.WriteLine($"Performance Alert: {alert.AlertType} - {alert.Message}");
        }

        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (!sortedValues.Any()) return 0;
            
            var index = percentile * (sortedValues.Count - 1);
            var lower = (int)Math.Floor(index);
            var upper = (int)Math.Ceiling(index);
            
            if (lower == upper) return sortedValues[lower];
            
            var weight = index - lower;
            return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
        }
    }

    /// <summary>
    /// Performance monitoring extensions for easy use
    /// </summary>
    public static class PerformanceMonitoringExtensions
    {
        /// <summary>
        /// Track location update performance
        /// </summary>
        public static void TrackLocationUpdateLatency(this INavigationPerformanceMonitor monitor, TimeSpan latency)
        {
            monitor.TrackMetric(PerformanceMetricType.LocationUpdate, latency, "Location service update");
        }

        /// <summary>
        /// Track route calculation performance
        /// </summary>
        public static void TrackRouteCalculationTime(this INavigationPerformanceMonitor monitor, TimeSpan calculationTime)
        {
            monitor.TrackMetric(PerformanceMetricType.RouteCalculation, calculationTime, "Route calculation");
        }

        /// <summary>
        /// Track navigation update performance
        /// </summary>
        public static void TrackNavigationUpdateLatency(this INavigationPerformanceMonitor monitor, TimeSpan latency)
        {
            monitor.TrackMetric(PerformanceMetricType.NavigationUpdate, latency, "Navigation status update");
        }

        /// <summary>
        /// Track TTS speech performance
        /// </summary>
        public static void TrackTTSLatency(this INavigationPerformanceMonitor monitor, TimeSpan latency)
        {
            monitor.TrackMetric(PerformanceMetricType.TTSSpeech, latency, "Text-to-speech playback");
        }

        /// <summary>
        /// Track memory usage
        /// </summary>
        public static void TrackMemoryUsage(this INavigationPerformanceMonitor monitor, double memoryMB)
        {
            monitor.TrackValue(PerformanceMetricType.Memory, memoryMB, "MB", "Application memory usage");
        }

        /// <summary>
        /// Track battery level
        /// </summary>
        public static void TrackBatteryLevel(this INavigationPerformanceMonitor monitor, double batteryPercent)
        {
            monitor.TrackValue(PerformanceMetricType.Battery, batteryPercent, "%", "Device battery level");
        }
    }
}