using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes; // 提供 Ellipse, RoundRectangle
using System.ComponentModel;

namespace MapLocationApp.Controls
{
    /// <summary>
    /// 液態玻璃卡片控件 - 提供高級毛玻璃效果和互動動畫
    /// </summary>
    public class LiquidGlassCard : ContentView
    {
        public static readonly BindableProperty CornerRadiusProperty =
            BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(LiquidGlassCard),
                new CornerRadius(20), propertyChanged: OnStylePropertyChanged);

        public static readonly BindableProperty GlassOpacityProperty =
            BindableProperty.Create(nameof(GlassOpacity), typeof(double), typeof(LiquidGlassCard),
                0.8, propertyChanged: OnStylePropertyChanged);

        public static readonly BindableProperty BlurRadiusProperty =
            BindableProperty.Create(nameof(BlurRadius), typeof(double), typeof(LiquidGlassCard),
                20.0, propertyChanged: OnStylePropertyChanged);

        public static readonly BindableProperty BorderWidthProperty =
            BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(LiquidGlassCard),
                1.0, propertyChanged: OnStylePropertyChanged);

        public static readonly BindableProperty BorderColorProperty =
            BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(LiquidGlassCard),
                Colors.White, propertyChanged: OnStylePropertyChanged);

        public static readonly BindableProperty ShadowRadiusProperty =
            BindableProperty.Create(nameof(ShadowRadius), typeof(double), typeof(LiquidGlassCard),
                24.0, propertyChanged: OnStylePropertyChanged);

        public static readonly BindableProperty ShadowOffsetProperty =
            BindableProperty.Create(nameof(ShadowOffset), typeof(Point), typeof(LiquidGlassCard),
                new Point(0, 8), propertyChanged: OnStylePropertyChanged);

        public static readonly BindableProperty IsInteractiveProperty =
            BindableProperty.Create(nameof(IsInteractive), typeof(bool), typeof(LiquidGlassCard),
                true, propertyChanged: OnInteractiveChanged);

        public static readonly BindableProperty EnableRippleEffectProperty =
            BindableProperty.Create(nameof(EnableRippleEffect), typeof(bool), typeof(LiquidGlassCard),
                true);

        private Border _borderContainer;
        private bool _isPressed = false;
        private bool _isHovered = false;

        public LiquidGlassCard()
        {
            InitializeComponent();
            SetupGestureRecognizers();
        }

        #region Bindable Properties

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public double GlassOpacity
        {
            get => (double)GetValue(GlassOpacityProperty);
            set => SetValue(GlassOpacityProperty, value);
        }

        public double BlurRadius
        {
            get => (double)GetValue(BlurRadiusProperty);
            set => SetValue(BlurRadiusProperty, value);
        }

        public double BorderWidth
        {
            get => (double)GetValue(BorderWidthProperty);
            set => SetValue(BorderWidthProperty, value);
        }

        public Color BorderColor
        {
            get => (Color)GetValue(BorderColorProperty);
            set => SetValue(BorderColorProperty, value);
        }

        public double ShadowRadius
        {
            get => (double)GetValue(ShadowRadiusProperty);
            set => SetValue(ShadowRadiusProperty, value);
        }

        public Point ShadowOffset
        {
            get => (Point)GetValue(ShadowOffsetProperty);
            set => SetValue(ShadowOffsetProperty, value);
        }

        public bool IsInteractive
        {
            get => (bool)GetValue(IsInteractiveProperty);
            set => SetValue(IsInteractiveProperty, value);
        }

        public bool EnableRippleEffect
        {
            get => (bool)GetValue(EnableRippleEffectProperty);
            set => SetValue(EnableRippleEffectProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<TappedEventArgs> Tapped;
        public event EventHandler<EventArgs> Pressed;
        public event EventHandler<EventArgs> Released;
        public event EventHandler<EventArgs> Hovered;
        public event EventHandler<EventArgs> Unhovered;

        #endregion

        private void InitializeComponent()
        {
            _borderContainer = new Border();
            try
            {
                _borderContainer.StrokeShape = new RoundRectangle { CornerRadius = CornerRadius };
            }
            catch
            {
                // 若在特定平台 RoundRectangle 不可用，忽略形狀設定
            }
            _borderContainer.StrokeThickness = BorderWidth;
            _borderContainer.Stroke = BorderColor;
            _borderContainer.Shadow = new Shadow
            {
                Radius = (float)ShadowRadius,
                Offset = new Point(ShadowOffset.X, ShadowOffset.Y),
                Opacity = 0.3f,
                Brush = Brush.Black
            };

            // 創建毛玻璃背景
            var glassBackground = CreateGlassBackground();
            _borderContainer.Background = glassBackground;

            Content = _borderContainer;
            UpdateVisualState();
        }

        private Brush CreateGlassBackground()
        {
            // 創建毛玻璃漸變效果
            var glassBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };

            if (Application.Current?.RequestedTheme == AppTheme.Dark)
            {
                glassBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb("#FFFFFF20"), Offset = 0.0f });
                glassBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb("#1C1C1ECC"), Offset = 1.0f });
            }
            else
            {
                glassBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb("#FFFFFF80"), Offset = 0.0f });
                glassBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb("#F8F9FACC"), Offset = 1.0f });
            }

            return glassBrush;
        }

        private void SetupGestureRecognizers()
        {
            if (!IsInteractive) return;

            // 點擊手勢
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnTapped;
            GestureRecognizers.Add(tapGesture);

            // 指針手勢 (用於桌面懸停效果)
            var pointerGesture = new PointerGestureRecognizer();
            pointerGesture.PointerPressed += OnPointerPressed;
            pointerGesture.PointerReleased += OnPointerReleased;
            pointerGesture.PointerEntered += OnPointerEntered;
            pointerGesture.PointerExited += OnPointerExited;
            GestureRecognizers.Add(pointerGesture);
        }

        #region Event Handlers

        private async void OnTapped(object sender, TappedEventArgs e)
        {
            if (EnableRippleEffect)
            {
                await PlayRippleEffect();
            }

            Tapped?.Invoke(this, e);
        }

        private async void OnPointerPressed(object sender, PointerEventArgs e)
        {
            _isPressed = true;
            await PlayPressAnimation();
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        private async void OnPointerReleased(object sender, PointerEventArgs e)
        {
            _isPressed = false;
            await PlayReleaseAnimation();
            Released?.Invoke(this, EventArgs.Empty);
        }

        private async void OnPointerEntered(object sender, PointerEventArgs e)
        {
            _isHovered = true;
            await PlayHoverAnimation();
            Hovered?.Invoke(this, EventArgs.Empty);
        }

        private async void OnPointerExited(object sender, PointerEventArgs e)
        {
            _isHovered = false;
            await PlayUnhoverAnimation();
            Unhovered?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Animations

        private async Task PlayPressAnimation()
        {
            await this.ScaleTo(0.96, 150, Easing.CubicOut);
        }

        private async Task PlayReleaseAnimation()
        {
            if (_isHovered)
            {
                await this.ScaleTo(1.02, 200, Easing.BounceOut);
            }
            else
            {
                await this.ScaleTo(1.0, 200, Easing.BounceOut);
            }
        }

        private async Task PlayHoverAnimation()
        {
            var tasks = new[]
            {
                this.TranslateTo(0, -2, 200, Easing.CubicOut),
                this.ScaleTo(1.02, 200, Easing.CubicOut)
            };

            await Task.WhenAll(tasks);
        }

        private async Task PlayUnhoverAnimation()
        {
            if (_isPressed) return;

            var tasks = new[]
            {
                this.TranslateTo(0, 0, 200, Easing.CubicOut),
                this.ScaleTo(1.0, 200, Easing.CubicOut)
            };

            await Task.WhenAll(tasks);
        }

        private async Task PlayRippleEffect()
        {
            // 創建波紋效果
            View ripple;
            try
            {
                ripple = new Ellipse
                {
                    Fill = Brush.White,
                    Opacity = 0.3,
                    WidthRequest = 0,
                    HeightRequest = 0,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
            }
            catch
            {
                // 若 Ellipse 不可用，使用 BoxView 模擬
                ripple = new BoxView
                {
                    Color = Colors.White,
                    Opacity = 0.3,
                    WidthRequest = 10,
                    HeightRequest = 10,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    CornerRadius = 100
                };
            }

            // 暫時添加到容器中
            if (_borderContainer.Content is Layout layout)
            {
                layout.Add(ripple);
            }

            // 執行波紋動畫
            var maxSize = Math.Max(Width, Height) * 1.5;
            var scaleTask = ripple.ScaleTo(maxSize, 600, Easing.CubicOut);
            var fadeTask = ripple.FadeTo(0, 600, Easing.CubicOut);

            await Task.WhenAll(scaleTask, fadeTask);

            // 移除波紋元素
            if (_borderContainer.Content is Layout parentLayout)
            {
                parentLayout.Remove(ripple);
            }
        }

        #endregion

        #region Property Changed Handlers

        private static void OnStylePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is LiquidGlassCard card)
            {
                card.UpdateVisualState();
            }
        }

        private static void OnInteractiveChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is LiquidGlassCard card)
            {
                card.GestureRecognizers.Clear();
                if (card.IsInteractive)
                {
                    card.SetupGestureRecognizers();
                }
            }
        }

        #endregion

        private void UpdateVisualState()
        {
            if (_borderContainer == null) return;

            try
            {
                _borderContainer.StrokeShape = new RoundRectangle { CornerRadius = CornerRadius };
            }
            catch { /* 忽略平台不支援 */ }
            _borderContainer.StrokeThickness = BorderWidth;
            _borderContainer.Stroke = BorderColor;
            _borderContainer.Background = CreateGlassBackground();

            if (_borderContainer.Shadow != null)
            {
                _borderContainer.Shadow.Radius = (float)ShadowRadius;
                _borderContainer.Shadow.Offset = ShadowOffset;
            }
        }

        /// <summary>
        /// 設置內容到玻璃卡片中
        /// </summary>
        public void SetCardContent(View content)
        {
            if (content == null) return;

            _borderContainer.Content = content;
        }

        /// <summary>
        /// 播放載入動畫
        /// </summary>
        public async Task PlayLoadAnimation(uint delay = 0)
        {
            if (delay > 0)
                await Task.Delay((int)delay);

            // 初始狀態
            TranslationY = 30;
            Scale = 0.9;
            Opacity = 0;

            // 載入動畫
            var translateTask = this.TranslateTo(0, 0, 600, Easing.CubicOut);
            var scaleTask = this.ScaleTo(1.0, 600, Easing.CubicOut);
            var fadeTask = this.FadeTo(1.0, 600, Easing.CubicOut);

            await Task.WhenAll(translateTask, scaleTask, fadeTask);
        }

        /// <summary>
        /// 播放脈衝動畫
        /// </summary>
        public void StartPulseAnimation(uint duration = 2000)
        {
            var animation = new Animation(v => Scale = 1.0 + (v * 0.03), 0, 1);
            animation.Commit(this, "PulseAnimation", 16, duration, Easing.SinInOut, null, () => true);
        }

        /// <summary>
        /// 停止脈衝動畫
        /// </summary>
        public void StopPulseAnimation()
        {
            this.AbortAnimation("PulseAnimation");
            Scale = 1.0;
        }
    }
}