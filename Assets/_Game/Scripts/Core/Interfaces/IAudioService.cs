using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Core.Interfaces
{
    public interface IAudioService
    {
        /// <summary>
        /// Phát nhạc nền (BGM). Hỗ trợ tự động fade-out bài cũ và fade-in bài mới.
        /// </summary>
        void PlayBGM(SoundType type, float fadeDuration = 1f);

        /// <summary>
        /// Phát hiệu ứng âm thanh (SFX). Có thể truyền vị trí (world space) để phát âm thanh 3D, hoặc null để phát 2D.
        /// </summary>
        void PlaySFX(SoundType type, Vector3? position = null);

        /// <summary>
        /// Dừng nhạc nền ngay lập tức.
        /// </summary>
        void StopBGM(float fadeOutDuration = 0f);

        /// <summary>
        /// Thay đổi âm lượng tổng. Giá trị từ 0f đến 1f.
        /// </summary>
        void SetMasterVolume(float volume);

        /// <summary>
        /// Thay đổi âm lượng BGM. Giá trị từ 0f đến 1f.
        /// </summary>
        void SetBGMVolume(float volume);

        /// <summary>
        /// Thay đổi âm lượng SFX. Giá trị từ 0f đến 1f.
        /// </summary>
        void SetSFXVolume(float volume);

        /// <summary>
        /// Âm lượng tổng hiện tại. Giá trị từ 0f đến 1f.
        /// </summary>
        float MasterVolume { get; }

        /// <summary>
        /// Âm lượng BGM hiện tại. Giá trị từ 0f đến 1f.
        /// </summary>
        float BGMVolume { get; }

        /// <summary>
        /// Âm lượng SFX hiện tại. Giá trị từ 0f đến 1f.
        /// </summary>
        float SFXVolume { get; }
    }
}
