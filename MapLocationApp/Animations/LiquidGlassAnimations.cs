using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace MapLocationApp.Animations
{
    public static class LiquidGlassAnimations
    {
        /// <summary>
        /// 液態玻璃進入動畫 - 卡片從底部滑入並帶有縮放效果
        /// </summary>
        public static async Task CardSlideInAnimation(View element, uint duration = 600, uint delay = 0)
        {
            if (element == null) return;

            // 初始狀態
            element.TranslationY = 50;
            element.Scale = 0.95;
            element.Opacity = 0;

            // 延遲開始
            if (delay > 0)
                await Task.Delay((int)delay);

            // 同時執行多個動畫
            var translateTask = element.TranslateTo(0, 0, duration, Easing.CubicOut);
            var scaleTask = element.ScaleTo(1.0, duration, Easing.CubicOut);
            var fadeTask = element.FadeTo(1.0, duration, Easing.CubicOut);

            await Task.WhenAll(translateTask, scaleTask, fadeTask);
        }

        /// <summary>
        /// 按鈕按下動畫 - 模擬液態玻璃的回彈效果
        /// </summary>
        public static async Task ButtonPressAnimation(View element)
        {
            if (element == null) return;

            // 按下效果 - 快速縮小
            await element.ScaleTo(0.95, 80, Easing.CubicOut);

            // 鬆開效果 - 回彈並略微放大
            await element.ScaleTo(1.02, 120, Easing.BounceOut);

            // 回到正常大小
            await element.ScaleTo(1.0, 100, Easing.CubicOut);
        }

        /// <summary>
        /// 玻璃卡片懸停動畫 - 輕微上浮效果
        /// </summary>
        public static async Task CardHoverAnimation(View element, bool isHovered = true)
        {
            if (element == null) return;

            if (isHovered)
            {
                // 懸停效果 - 輕微上升和陰影增強
                var translateTask = element.TranslateTo(0, -3, 200, Easing.CubicOut);
                var scaleTask = element.ScaleTo(1.02, 200, Easing.CubicOut);

                await Task.WhenAll(translateTask, scaleTask);
            }
            else
            {
                // 離開效果 - 回到原位
                var translateTask = element.TranslateTo(0, 0, 200, Easing.CubicOut);
                var scaleTask = element.ScaleTo(1.0, 200, Easing.CubicOut);

                await Task.WhenAll(translateTask, scaleTask);
            }
        }

        /// <summary>
        /// 狀態指示器脈衝動畫
        /// </summary>
        public static async Task StatusIndicatorPulse(View element, bool isRunning = true)
        {
            if (element == null) return;

            while (isRunning)
            {
                // 放大
                await element.ScaleTo(1.1, 800, Easing.SinInOut);
                // 縮小
                await element.ScaleTo(1.0, 800, Easing.SinInOut);

                // 檢查是否需要繼續動畫
                if (!isRunning) break;
            }
        }

        /// <summary>
        /// 漸變載入動畫 - 適用於整個頁面
        /// </summary>
        public static async Task PageLoadAnimation(params View[] elements)
        {
            if (elements == null || elements.Length == 0) return;

            // 初始化所有元素
            foreach (var element in elements)
            {
                element.TranslationY = 30;
                element.Opacity = 0;
            }

            // 錯開動畫每個元素
            var tasks = new List<Task>();
            for (int i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                var delay = i * 100; // 每個元素延遲100毫秒

                tasks.Add(CardSlideInAnimation(element, 500, (uint)delay));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 成功反饋動畫 - 綠色脈衝效果
        /// </summary>
        public static async Task SuccessFeedbackAnimation(View element)
        {
            if (element == null) return;

            // 快速放大並變綠
            await element.ScaleTo(1.1, 150, Easing.CubicOut);

            // 保持狀態短暫時間
            await Task.Delay(200);

            // 回到正常狀態
            await element.ScaleTo(1.0, 200, Easing.BounceOut);
        }

        /// <summary>
        /// 錯誤反饋動畫 - 震動效果
        /// </summary>
        public static async Task ErrorFeedbackAnimation(View element)
        {
            if (element == null) return;

            // 震動動畫
            for (int i = 0; i < 3; i++)
            {
                await element.TranslateTo(-5, 0, 50, Easing.Linear);
                await element.TranslateTo(5, 0, 50, Easing.Linear);
            }

            // 回到中心位置
            await element.TranslateTo(0, 0, 50, Easing.Linear);
        }

        /// <summary>
        /// 液態玻璃漸變動畫 - 模擬液體流動效果
        /// </summary>
        public static async Task LiquidFlowAnimation(View element, uint duration = 2000)
        {
            if (element == null) return;

            // 創建連續的波浪式動畫
            var animation = new Animation();

            // X軸輕微移動
            animation.Add(0, 1, new Animation(v => element.TranslationX = Math.Sin(v * Math.PI * 2) * 2));

            // 透明度輕微變化
            animation.Add(0, 1, new Animation(v => element.Opacity = 0.9 + Math.Sin(v * Math.PI * 4) * 0.1));

            animation.Commit(element, "LiquidFlow", 16, duration, Easing.SinInOut, (v, c) => {
                element.TranslationX = 0;
                element.Opacity = 1.0;
            }, () => true);

            await Task.Delay((int)duration);
        }

        /// <summary>
        /// 觸控回饋動畫 - 波紋效果模擬
        /// </summary>
        public static async Task RippleEffect(View element, double fromScale = 0.8, double toScale = 1.2)
        {
            if (element == null) return;

            // 創建遮罩效果
            element.Scale = fromScale;
            element.Opacity = 0.3;

            var scaleTask = element.ScaleTo(toScale, 600, Easing.CubicOut);
            var fadeTask = element.FadeTo(0, 600, Easing.CubicOut);

            await Task.WhenAll(scaleTask, fadeTask);

            // 重置狀態
            element.Scale = 1.0;
            element.Opacity = 1.0;
        }

        /// <summary>
        /// 彈性載入動畫 - 適用於內容載入時
        /// </summary>
        public static async Task ElasticLoadAnimation(View element)
        {
            if (element == null) return;

            // 初始狀態
            element.Scale = 0.3;
            element.Opacity = 0;

            // 彈性放大動畫
            var scaleTask = element.ScaleTo(1.0, 800, Easing.SpringOut);
            var fadeTask = element.FadeTo(1.0, 600, Easing.CubicOut);

            await Task.WhenAll(scaleTask, fadeTask);
        }

        /// <summary>
        /// 呼吸動畫 - 適用於狀態指示或聚焦效果
        /// </summary>
        public static void StartBreathingAnimation(View element, uint duration = 2000)
        {
            if (element == null) return;

            var animation = new Animation(v => element.Scale = 1.0 + (v * 0.05), 0, 1);
            animation.Commit(element, "Breathing", 16, duration, Easing.SinInOut, (v, c) => { }, () => true);
        }

        /// <summary>
        /// 停止呼吸動畫
        /// </summary>
        public static void StopBreathingAnimation(View element)
        {
            if (element == null) return;

            element.AbortAnimation("Breathing");
            element.Scale = 1.0;
        }
    }

    /// <summary>
    /// 自定義緩動函數 - 液態玻璃效果
    /// </summary>
    public static class LiquidGlassEasing
    {
        /// <summary>
        /// 液態回彈效果 - 模擬液體的表面張力
        /// </summary>
        public static readonly Easing LiquidBounce = Easing.CubicOut;

        /// <summary>
        /// 玻璃滑動效果 - 平滑漸入漸出
        /// </summary>
        public static readonly Easing GlassSlide = new Easing(t => {
            if (t < 0.5)
                return 2 * t * t;
            return -1 + (4 - 2 * t) * t;
        });

        /// <summary>
        /// 液態波動效果 - 正弦波動
        /// </summary>
        public static readonly Easing LiquidWave = new Easing(t =>
            (Math.Sin(t * Math.PI - Math.PI / 2) + 1) / 2);
    }

    /// <summary>
    /// 動畫隊列管理器 - 用於管理複雜的動畫序列
    /// </summary>
    public class AnimationQueue
    {
        private readonly List<Func<Task>> _animations = new();
        private bool _isRunning = false;

        public AnimationQueue Add(Func<Task> animation)
        {
            _animations.Add(animation);
            return this;
        }

        public AnimationQueue Add(View element, Func<View, Task> animation)
        {
            _animations.Add(() => animation(element));
            return this;
        }

        public async Task PlayAsync()
        {
            if (_isRunning) return;

            _isRunning = true;

            foreach (var animation in _animations)
            {
                await animation();
            }

            _isRunning = false;
        }

        public async Task PlayParallelAsync()
        {
            if (_isRunning) return;

            _isRunning = true;

            var tasks = _animations.Select(animation => animation()).ToArray();
            await Task.WhenAll(tasks);

            _isRunning = false;
        }

        public void Clear()
        {
            _animations.Clear();
        }
    }
}