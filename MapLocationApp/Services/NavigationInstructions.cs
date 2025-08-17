using System;
using System.Collections.Generic;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Navigation instruction templates for voice guidance
    /// Supports multiple languages and instruction types
    /// </summary>
    public static class NavigationInstructions
    {
        // Distance thresholds for different instruction types
        public const double IMMEDIATE_TURN_DISTANCE = 50; // 50 meters
        public const double PREPARE_TURN_DISTANCE = 200; // 200 meters
        public const double LONG_DISTANCE_THRESHOLD = 1000; // 1 kilometer

        // Basic navigation instructions in Traditional Chinese
        public static class ZhTW
        {
            public const string TURN_RIGHT = "前方 {distance} 右轉";
            public const string TURN_LEFT = "前方 {distance} 左轉";
            public const string TURN_SLIGHT_RIGHT = "前方 {distance} 靠右行駛";
            public const string TURN_SLIGHT_LEFT = "前方 {distance} 靠左行駛";
            public const string CONTINUE_STRAIGHT = "直行 {distance}";
            public const string UTURN = "前方 {distance} 迴轉";
            public const string MERGE = "前方 {distance} 匯入車道";
            public const string EXIT = "前方 {distance} 駛離";
            public const string ARRIVE_DESTINATION = "您已到達目的地";
            public const string RECALCULATING = "正在重新計算路線";
            public const string GPS_LOST = "GPS 訊號丟失，請檢查位置設定";
            public const string ROUTE_DEVIATION = "您已偏離路線，正在重新規劃";
            public const string SPEED_LIMIT = "注意前方限速 {speed} 公里";
            public const string TRAFFIC_AHEAD = "前方道路擁塞";
            public const string PREPARE_TURN = "準備在 {distance} 處{direction}";
        }

        // English instructions
        public static class EnUS
        {
            public const string TURN_RIGHT = "In {distance}, turn right";
            public const string TURN_LEFT = "In {distance}, turn left";
            public const string TURN_SLIGHT_RIGHT = "In {distance}, keep right";
            public const string TURN_SLIGHT_LEFT = "In {distance}, keep left";
            public const string CONTINUE_STRAIGHT = "Continue straight for {distance}";
            public const string UTURN = "In {distance}, make a U-turn";
            public const string MERGE = "In {distance}, merge";
            public const string EXIT = "In {distance}, take the exit";
            public const string ARRIVE_DESTINATION = "You have arrived at your destination";
            public const string RECALCULATING = "Recalculating route";
            public const string GPS_LOST = "GPS signal lost, please check location settings";
            public const string ROUTE_DEVIATION = "You have deviated from the route, recalculating";
            public const string SPEED_LIMIT = "Speed limit ahead: {speed} km/h";
            public const string TRAFFIC_AHEAD = "Traffic ahead";
            public const string PREPARE_TURN = "Prepare to {direction} in {distance}";
        }

        /// <summary>
        /// Gets the instruction template for the specified language and instruction type
        /// </summary>
        public static string GetInstruction(NavigationType type, string language = "zh-TW")
        {
            return language.ToLower() switch
            {
                "zh-tw" or "zh" => GetZhTWInstruction(type),
                "en-us" or "en" => GetEnUSInstruction(type),
                _ => GetZhTWInstruction(type) // Default to Traditional Chinese
            };
        }

        private static string GetZhTWInstruction(NavigationType type)
        {
            return type switch
            {
                NavigationType.TurnLeft => ZhTW.TURN_LEFT,
                NavigationType.TurnRight => ZhTW.TURN_RIGHT,
                NavigationType.TurnSlightLeft => ZhTW.TURN_SLIGHT_LEFT,
                NavigationType.TurnSlightRight => ZhTW.TURN_SLIGHT_RIGHT,
                NavigationType.Continue => ZhTW.CONTINUE_STRAIGHT,
                NavigationType.UTurn => ZhTW.UTURN,
                NavigationType.Merge => ZhTW.MERGE,
                NavigationType.Exit => ZhTW.EXIT,
                NavigationType.Arrive => ZhTW.ARRIVE_DESTINATION,
                _ => ZhTW.CONTINUE_STRAIGHT
            };
        }

        private static string GetEnUSInstruction(NavigationType type)
        {
            return type switch
            {
                NavigationType.TurnLeft => EnUS.TURN_LEFT,
                NavigationType.TurnRight => EnUS.TURN_RIGHT,
                NavigationType.TurnSlightLeft => EnUS.TURN_SLIGHT_LEFT,
                NavigationType.TurnSlightRight => EnUS.TURN_SLIGHT_RIGHT,
                NavigationType.Continue => EnUS.CONTINUE_STRAIGHT,
                NavigationType.UTurn => EnUS.UTURN,
                NavigationType.Merge => EnUS.MERGE,
                NavigationType.Exit => EnUS.EXIT,
                NavigationType.Arrive => EnUS.ARRIVE_DESTINATION,
                _ => EnUS.CONTINUE_STRAIGHT
            };
        }

        /// <summary>
        /// Formats distance for display in navigation instructions
        /// </summary>
        public static string FormatDistance(double distanceMeters, string language = "zh-TW")
        {
            if (distanceMeters < 100)
            {
                return language.ToLower() switch
                {
                    "zh-tw" or "zh" => $"{(int)distanceMeters} 公尺",
                    "en-us" or "en" => $"{(int)distanceMeters} meters",
                    _ => $"{(int)distanceMeters} 公尺"
                };
            }
            else if (distanceMeters < 1000)
            {
                var rounded = Math.Round(distanceMeters / 50) * 50; // Round to nearest 50m
                return language.ToLower() switch
                {
                    "zh-tw" or "zh" => $"{(int)rounded} 公尺",
                    "en-us" or "en" => $"{(int)rounded} meters",
                    _ => $"{(int)rounded} 公尺"
                };
            }
            else
            {
                var km = distanceMeters / 1000.0;
                return language.ToLower() switch
                {
                    "zh-tw" or "zh" => $"{km:F1} 公里",
                    "en-us" or "en" => $"{km:F1} kilometers",
                    _ => $"{km:F1} 公里"
                };
            }
        }

        /// <summary>
        /// Creates a complete navigation instruction with formatted distance
        /// </summary>
        public static string CreateInstruction(NavigationType type, double distanceMeters, string language = "zh-TW")
        {
            var template = GetInstruction(type, language);
            var formattedDistance = FormatDistance(distanceMeters, language);
            
            return template.Replace("{distance}", formattedDistance);
        }

        /// <summary>
        /// Gets the appropriate instruction timing based on distance
        /// </summary>
        public static InstructionTiming GetInstructionTiming(double distanceMeters)
        {
            return distanceMeters switch
            {
                <= IMMEDIATE_TURN_DISTANCE => InstructionTiming.Immediate,
                <= PREPARE_TURN_DISTANCE => InstructionTiming.Prepare,
                <= LONG_DISTANCE_THRESHOLD => InstructionTiming.Normal,
                _ => InstructionTiming.LongDistance
            };
        }
    }

    /// <summary>
    /// Navigation instruction types for turn-by-turn guidance
    /// </summary>
    public enum NavigationType
    {
        TurnLeft,
        TurnRight,
        TurnSlightLeft,
        TurnSlightRight,
        Continue,
        UTurn,
        Merge,
        Exit,
        Arrive,
        RoundaboutEnter,
        RoundaboutExit
    }

    /// <summary>
    /// Timing for when to deliver navigation instructions
    /// </summary>
    public enum InstructionTiming
    {
        Immediate,      // 0-50m
        Prepare,        // 50-200m
        Normal,         // 200m-1km
        LongDistance    // >1km
    }
}