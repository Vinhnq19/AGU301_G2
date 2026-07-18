using DungeonBuilder.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.UI
{
    public class SettingsPanelController : MonoBehaviour
    {
        [Header("Volume Sliders")]
        [Tooltip("Kéo Slider điều khiển âm lượng tổng vào đây")]
        [SerializeField] private Slider masterVolumeSlider;

        [Tooltip("Kéo Slider điều khiển âm lượng nhạc nền vào đây")]
        [SerializeField] private Slider bgmVolumeSlider;

        [Tooltip("Kéo Slider điều khiển âm lượng hiệu ứng vào đây")]
        [SerializeField] private Slider sfxVolumeSlider;

        private void OnEnable()
        {
            Time.timeScale = 0f;

            if (AudioManager.Instance == null)
                return;

            SetupSlider(masterVolumeSlider, AudioManager.Instance.MasterVolume, OnMasterVolumeChanged);
            SetupSlider(bgmVolumeSlider, AudioManager.Instance.BGMVolume, OnBGMVolumeChanged);
            SetupSlider(sfxVolumeSlider, AudioManager.Instance.SFXVolume, OnSFXVolumeChanged);
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;

            RemoveListener(masterVolumeSlider, OnMasterVolumeChanged);
            RemoveListener(bgmVolumeSlider, OnBGMVolumeChanged);
            RemoveListener(sfxVolumeSlider, OnSFXVolumeChanged);
        }

        private static void SetupSlider(Slider slider, float currentValue, UnityEngine.Events.UnityAction<float> handler)
        {
            if (slider == null)
                return;

            slider.SetValueWithoutNotify(currentValue);
            slider.onValueChanged.RemoveListener(handler);
            slider.onValueChanged.AddListener(handler);
        }

        private static void RemoveListener(Slider slider, UnityEngine.Events.UnityAction<float> handler)
        {
            if (slider != null)
                slider.onValueChanged.RemoveListener(handler);
        }

        private void OnMasterVolumeChanged(float value) => AudioManager.Instance?.SetMasterVolume(value);

        private void OnBGMVolumeChanged(float value) => AudioManager.Instance?.SetBGMVolume(value);

        private void OnSFXVolumeChanged(float value) => AudioManager.Instance?.SetSFXVolume(value);
    }
}
