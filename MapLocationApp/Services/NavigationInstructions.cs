using System;
using System.Collections.Generic;

namespace MapLocationApp.Services
{
    /// <summary>
    /// 導航指令類型
    /// </summary>
    public enum NavigationType
    {
        TurnLeft,           // 左轉
        TurnRight,          // 右轉
        TurnSlightLeft,     // 輕微左轉
        TurnSlightRight,    // 輕微右轉
        Continue,           // 直行
        UTurn,              // 迴轉
        Merge,              // 匯入
        Exit,               // 出口
        Arrive,             // 到達目的地
        KeepLeft,           // 靠左行駛
        KeepRight,          // 靠右行駛
        Roundabout,         // 圓環
        Ferry,              // 輪渡
        RampLeft,           // 左側匝道
        RampRight           // 右側匝道
    }

    /// <summary>
    /// 導航指令資料模型
    /// </summary>
    public class NavigationInstruction
    {
        public string Text { get; set; }
        public string Distance { get; set; }
        public string ArrowIcon { get; set; }
        public NavigationType Type { get; set; }
        public double DistanceInMeters { get; set; }
        public string StreetName { get; set; }
        public int ExitNumber { get; set; } // 圓環出口編號
        public string Direction { get; set; } // 方向（北、東南等）
    }

    /// <summary>
    /// 導航指令模板和語音指令生成器
    /// </summary>
    public static class NavigationInstructions
    {
        // 基本導航指令模板
        private static readonly Dictionary<NavigationType, string> InstructionTemplates = new()
        {
            { NavigationType.TurnLeft, "前方 {distance} 左轉" },
            { NavigationType.TurnRight, "前方 {distance} 右轉" },
            { NavigationType.TurnSlightLeft, "前方 {distance} 輕微左轉" },
            { NavigationType.TurnSlightRight, "前方 {distance} 輕微右轉" },
            { NavigationType.Continue, "直行 {distance}" },
            { NavigationType.UTurn, "前方 {distance} 迴轉" },
            { NavigationType.Merge, "前方 {distance} 匯入車道" },
            { NavigationType.Exit, "前方 {distance} 從出口離開" },
            { NavigationType.Arrive, "您已到達目的地" },
            { NavigationType.KeepLeft, "前方 {distance} 靠左行駛" },
            { NavigationType.KeepRight, "前方 {distance} 靠右行駛" },
            { NavigationType.Roundabout, "前方 {distance} 進入圓環，第 {exit} 個出口離開" },
            { NavigationType.Ferry, "前方 {distance} 搭乘輪渡" },
            { NavigationType.RampLeft, "前方 {distance} 從左側匝道離開" },
            { NavigationType.RampRight, "前方 {distance} 從右側匝道離開" }
        };

        // 帶有街道名稱的指令模板
        private static readonly Dictionary<NavigationType, string> InstructionWithStreetTemplates = new()
        {
            { NavigationType.TurnLeft, "前方 {distance} 左轉進入 {street}" },
            { NavigationType.TurnRight, "前方 {distance} 右轉進入 {street}" },
            { NavigationType.TurnSlightLeft, "前方 {distance} 輕微左轉進入 {street}" },
            { NavigationType.TurnSlightRight, "前方 {distance} 輕微右轉進入 {street}" },
            { NavigationType.Continue, "繼續沿著 {street} 直行 {distance}" },
            { NavigationType.Merge, "前方 {distance} 匯入 {street}" },
            { NavigationType.Exit, "前方 {distance} 從出口離開進入 {street}" }
        };

        // 圖示對應
        private static readonly Dictionary<NavigationType, string> ArrowIcons = new()
        {
            { NavigationType.TurnLeft, "⬅️" },
            { NavigationType.TurnRight, "➡️" },
            { NavigationType.TurnSlightLeft, "↖️" },
            { NavigationType.TurnSlightRight, "↗️" },
            { NavigationType.Continue, "⬆️" },
            { NavigationType.UTurn, "↩️" },
            { NavigationType.Merge, "🔀" },
            { NavigationType.Exit, "🛤️" },
            { NavigationType.Arrive, "🏁" },
            { NavigationType.KeepLeft, "⬅️" },
            { NavigationType.KeepRight, "➡️" },
            { NavigationType.Roundabout, "🔄" },
            { NavigationType.Ferry, "⛴️" },
            { NavigationType.RampLeft, "↰" },
            { NavigationType.RampRight, "↱" }
        };

        /// <summary>
        /// 生成導航指令
        /// </summary>
        public static NavigationInstruction CreateInstruction(
            NavigationType type, 
            double distanceInMeters, 
            string streetName = null, 
            int exitNumber = 0)
        {
            var distanceText = FormatDistance(distanceInMeters);
            var template = GetInstructionTemplate(type, streetName);
            
            var text = template
                .Replace("{distance}", distanceText)
                .Replace("{street}", streetName ?? "")
                .Replace("{exit}", exitNumber.ToString());

            return new NavigationInstruction
            {
                Text = text,
                Distance = distanceText,
                ArrowIcon = GetArrowIcon(type),
                Type = type,
                DistanceInMeters = distanceInMeters,
                StreetName = streetName,
                ExitNumber = exitNumber
            };
        }

        /// <summary>
        /// 格式化距離顯示
        /// </summary>
        public static string FormatDistance(double distanceInMeters)
        {
            if (distanceInMeters < 1000)
            {
                return $"{Math.Round(distanceInMeters / 10) * 10:F0} 公尺";
            }
            else if (distanceInMeters < 10000)
            {
                return $"{distanceInMeters / 1000:F1} 公里";
            }
            else
            {
                return $"{Math.Round(distanceInMeters / 1000):F0} 公里";
            }
        }

        /// <summary>
        /// 取得指令模板
        /// </summary>
        private static string GetInstructionTemplate(NavigationType type, string streetName)
        {
            if (!string.IsNullOrEmpty(streetName) && InstructionWithStreetTemplates.ContainsKey(type))
            {
                return InstructionWithStreetTemplates[type];
            }
            
            return InstructionTemplates.ContainsKey(type) 
                ? InstructionTemplates[type] 
                : "繼續前進 {distance}";
        }

        /// <summary>
        /// 取得箭頭圖示
        /// </summary>
        private static string GetArrowIcon(NavigationType type)
        {
            return ArrowIcons.ContainsKey(type) ? ArrowIcons[type] : "⬆️";
        }

        /// <summary>
        /// 生成即將到達的警告指令
        /// </summary>
        public static NavigationInstruction CreateApproachingInstruction(NavigationType nextType, double distanceInMeters)
        {
            var instruction = CreateInstruction(nextType, distanceInMeters);
            
            if (distanceInMeters <= 100)
            {
                instruction.Text = "準備 " + instruction.Text.Replace("前方 ", "");
            }
            
            return instruction;
        }

        /// <summary>
        /// 特殊情況指令
        /// </summary>
        public static class SpecialInstructions
        {
            public const string GPS_LOST = "GPS 訊號丟失，請檢查位置設定";
            public const string RECALCULATING = "正在重新計算路線";
            public const string OFF_ROUTE = "您已偏離路線，正在重新規劃";
            public const string TRAFFIC_AHEAD = "前方有交通阻塞，建議改道";
            public const string SPEED_LIMIT = "注意限速 {speed} 公里";
            public const string TOLL_AHEAD = "前方有收費站";
            public const string REST_AREA = "前方有休息站";
            public const string GAS_STATION = "前方有加油站";
            public const string PARKING_NEARBY = "附近有停車場";
            public const string DESTINATION_ON_LEFT = "目的地在您的左側";
            public const string DESTINATION_ON_RIGHT = "目的地在您的右側";
        }
    }
}