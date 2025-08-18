using System;
using System.Threading.Tasks;
using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    /// <summary>
    /// 導航狀態
    /// </summary>
    public class NavigationState
    {
        public Route CurrentRoute { get; set; }
        public AppLocation CurrentLocation { get; set; }
        public NavigationInstruction NextInstruction { get; set; }
        public NavigationInstruction CurrentInstruction { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public double DistanceRemaining { get; set; }
        public bool IsActive { get; set; }
        public DateTime StartTime { get; set; }
        public string EstimatedArrivalTime { get; set; }
        public bool IsOffRoute { get; set; }
        public double RouteProgress { get; set; } // 0.0 - 1.0
    }

    /// <summary>
    /// 路線偏離檢測結果
    /// </summary>
    public class RouteDeviationResult
    {
        public bool IsDeviated { get; set; }
        public double DeviationDistance { get; set; }
        public RouteAction SuggestedAction { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// 路線動作建議
    /// </summary>
    public enum RouteAction
    {
        Continue,       // 繼續當前路線
        Recalculate,    // 重新計算路線
        UTurn,          // 建議迴轉
        GetBackOnRoute  // 回到路線上
    }

    /// <summary>
    /// 進階導航服務介面
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// 目前導航狀態
        /// </summary>
        NavigationState CurrentState { get; }

        /// <summary>
        /// 是否正在導航
        /// </summary>
        bool IsNavigating { get; }

        /// <summary>
        /// 開始導航
        /// </summary>
        /// <param name="route">導航路線</param>
        /// <returns></returns>
        Task StartNavigationAsync(Route route);

        /// <summary>
        /// 停止導航
        /// </summary>
        /// <returns></returns>
        Task StopNavigationAsync();

        /// <summary>
        /// 暫停導航
        /// </summary>
        /// <returns></returns>
        Task PauseNavigationAsync();

        /// <summary>
        /// 恢復導航
        /// </summary>
        /// <returns></returns>
        Task ResumeNavigationAsync();

        /// <summary>
        /// 檢查路線偏離
        /// </summary>
        /// <param name="currentLocation">目前位置</param>
        /// <returns></returns>
        Task<RouteDeviationResult> CheckRouteDeviationAsync(AppLocation currentLocation);

        /// <summary>
        /// 重新計算路線
        /// </summary>
        /// <param name="currentLocation">目前位置</param>
        /// <returns></returns>
        Task<Route> RecalculateRouteAsync(AppLocation currentLocation);

        /// <summary>
        /// 更新導航狀態
        /// </summary>
        /// <param name="currentLocation">目前位置</param>
        /// <returns></returns>
        Task UpdateNavigationStateAsync(AppLocation currentLocation);

        /// <summary>
        /// 取得下一個導航指令
        /// </summary>
        /// <param name="currentLocation">目前位置</param>
        /// <returns></returns>
        Task<NavigationInstruction> GetNextInstructionAsync(AppLocation currentLocation);

        /// <summary>
        /// 檢查是否到達目的地
        /// </summary>
        /// <param name="currentLocation">目前位置</param>
        /// <returns></returns>
        Task<bool> CheckArrivalAsync(AppLocation currentLocation);

        // 事件
        /// <summary>
        /// 導航指令更新事件
        /// </summary>
        event EventHandler<NavigationInstruction> InstructionUpdated;

        /// <summary>
        /// 位置更新事件
        /// </summary>
        event EventHandler<AppLocation> LocationUpdated;

        /// <summary>
        /// 路線偏離事件
        /// </summary>
        event EventHandler<RouteDeviationResult> RouteDeviated;

        /// <summary>
        /// 到達目的地事件
        /// </summary>
        event EventHandler DestinationReached;

        /// <summary>
        /// 導航狀態變更事件
        /// </summary>
        event EventHandler<NavigationState> StateChanged;

        /// <summary>
        /// 錯誤事件
        /// </summary>
        event EventHandler<Exception> NavigationError;
    }
}