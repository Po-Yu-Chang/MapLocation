using System;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Text-to-Speech 服務介面，用於語音導航功能
    /// </summary>
    public interface ITTSService
    {
        /// <summary>
        /// 是否支援 TTS 功能
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// 播放語音
        /// </summary>
        /// <param name="text">要播放的文字</param>
        /// <param name="language">語言代碼 (預設: zh-TW)</param>
        /// <returns></returns>
        Task SpeakAsync(string text, string language = "zh-TW");

        /// <summary>
        /// 設定語音速度
        /// </summary>
        /// <param name="rate">語音速度 (0.1 - 2.0，預設: 1.0)</param>
        /// <returns></returns>
        Task SetSpeechRateAsync(float rate);

        /// <summary>
        /// 設定音量
        /// </summary>
        /// <param name="volume">音量 (0.0 - 1.0，預設: 1.0)</param>
        /// <returns></returns>
        Task SetVolumeAsync(float volume);

        /// <summary>
        /// 停止目前的語音播放
        /// </summary>
        /// <returns></returns>
        Task StopSpeakingAsync();

        /// <summary>
        /// 檢查是否正在播放語音
        /// </summary>
        bool IsSpeaking { get; }

        /// <summary>
        /// 語音播放完成事件
        /// </summary>
        event EventHandler SpeechFinished;
    }
}