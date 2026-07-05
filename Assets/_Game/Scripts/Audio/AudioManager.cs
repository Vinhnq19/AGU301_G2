using DG.Tweening;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Data;
using UnityEngine;

namespace DungeonBuilder.Audio
{
    public sealed class AudioManager : MonoBehaviour, IAudioService
    {
        public static AudioManager Instance { get; private set; }

        [Header("Data")]
        [SerializeField] private AudioCatalogSO _catalog;

        [Header("Settings")]
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _bgmVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;


        [Header("Pooling")]
        [SerializeField, Min(1)] private int _sfxPoolSize = 15;

        private const string PrefMasterVolume = "Audio_MasterVolume";
        private const string PrefBGMVolume = "Audio_BGMVolume";
        private const string PrefSFXVolume = "Audio_SFXVolume";

        private AudioSource _bgmSource1;
        private AudioSource _bgmSource2;
        private bool _isUsingSource1 = true;
        private Tween _bgmFadeTween1;
        private Tween _bgmFadeTween2;

        private AudioSource[] _sfxSources;
        private int _currentSfxIndex = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null); // Must be at root for DontDestroyOnLoad
            DontDestroyOnLoad(gameObject);

            // Không ép âm lượng nữa để Inspector và AudioCatalog có tác dụng
            LoadVolumeSettings();

            InitializeBGMSources();
            InitializeSFXSources();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                _bgmFadeTween1?.Kill();
                _bgmFadeTween2?.Kill();
                PlayerPrefs.Save();
                Instance = null;
            }
        }

        private void LoadVolumeSettings()
        {
            _masterVolume = PlayerPrefs.GetFloat(PrefMasterVolume, _masterVolume);
            _bgmVolume = PlayerPrefs.GetFloat(PrefBGMVolume, _bgmVolume);
            _sfxVolume = PlayerPrefs.GetFloat(PrefSFXVolume, _sfxVolume);
        }

        private void InitializeBGMSources()
        {
            GameObject bgmObj = new GameObject("BGM_Sources");
            bgmObj.transform.SetParent(transform);

            _bgmSource1 = bgmObj.AddComponent<AudioSource>();
            _bgmSource1.loop = true;
            _bgmSource1.playOnAwake = false;

            _bgmSource2 = bgmObj.AddComponent<AudioSource>();
            _bgmSource2.loop = true;
            _bgmSource2.playOnAwake = false;
        }

        private void InitializeSFXSources()
        {
            GameObject sfxObj = new GameObject("SFX_Sources");
            sfxObj.transform.SetParent(transform);

            _sfxSources = new AudioSource[_sfxPoolSize];
            for (int i = 0; i < _sfxPoolSize; i++)
            {
                _sfxSources[i] = sfxObj.AddComponent<AudioSource>();
                _sfxSources[i].playOnAwake = false;
                _sfxSources[i].spatialBlend = 0f;
                _sfxSources[i].rolloffMode = AudioRolloffMode.Linear;
                _sfxSources[i].minDistance = 5f;
                _sfxSources[i].maxDistance = 15f;
            }
        }

        public void PlayBGM(SoundType type, float fadeDuration = 1f)
        {
            Debug.Log($"[AudioManager] PlayBGM requested: {type}");
            if (_catalog == null || !_catalog.TryGetEntry(type, out var entry))
            {
                Debug.LogWarning($"[AudioManager] Cannot play BGM: {type}. Entry not found or Clip is null.");
                return;
            }

            AudioSource activeSource = _isUsingSource1 ? _bgmSource1 : _bgmSource2;

            // Khong phat lai neu cung bai

            if (activeSource.isPlaying && activeSource.clip == entry.Clip)
            {
                Debug.Log($"[AudioManager] BGM {type} is already playing.");
                return;
            }

            Debug.Log($"[AudioManager] Crossfading to BGM {type}");
            AudioSource nextSource = _isUsingSource1 ? _bgmSource2 : _bgmSource1;


            nextSource.clip = entry.Clip;
            nextSource.volume = 0f;
            nextSource.Play();

            float targetVolume = _masterVolume * _bgmVolume * entry.VolumeScale;

            // Fade out
            if (activeSource.isPlaying)
            {
                Tween fadeOutTween = _isUsingSource1 ? _bgmFadeTween1 : _bgmFadeTween2;
                fadeOutTween?.Kill();


                var sourceToFadeOut = activeSource;
                var tween = sourceToFadeOut.DOFade(0f, fadeDuration).OnComplete(() => sourceToFadeOut.Stop());


                if (_isUsingSource1) _bgmFadeTween1 = tween;
                else _bgmFadeTween2 = tween;
            }

            // Fade in
            Tween fadeInTween = _isUsingSource1 ? _bgmFadeTween2 : _bgmFadeTween1;
            fadeInTween?.Kill();


            var sourceToFadeIn = nextSource;
            var tweenIn = sourceToFadeIn.DOFade(targetVolume, fadeDuration);


            if (_isUsingSource1) _bgmFadeTween2 = tweenIn;
            else _bgmFadeTween1 = tweenIn;

            _isUsingSource1 = !_isUsingSource1;
        }

        public void PlaySFX(SoundType type, Vector3? position = null)
        {
            if (type == SoundType.None || _catalog == null || !_catalog.TryGetEntry(type, out var entry))
            {
                return;
            }

            // Round-robin pooling
            AudioSource source = _sfxSources[_currentSfxIndex];
            _currentSfxIndex = (_currentSfxIndex + 1) % _sfxPoolSize;

            if (position.HasValue)
            {
                source.transform.position = position.Value;

                // Giảm khoảng cách nghe được xuống 1/3 đối với âm thanh của tháp
                bool isTowerSound = type == SoundType.SFX_Arrow_Tower ||

                                    type == SoundType.SFX_Canon_Tower ||

                                    type == SoundType.SFX_Frost_Tower ||

                                    type == SoundType.SFX_Laser_Tower ||
                                    type == SoundType.SFX_Build_Place;

                if (isTowerSound)
                {
                    source.spatialBlend = 1f; // 3D
                    source.minDistance = 10f;
                    source.maxDistance = 30f;
                }
                else
                {
                    source.spatialBlend = 0f; // 2D
                }
            }
            else
            {
                source.transform.localPosition = Vector3.zero;
                source.spatialBlend = 0f; // 2D
            }

            source.volume = _masterVolume * _sfxVolume * entry.VolumeScale;
            source.pitch = Random.Range(0.95f, 1.05f); // Tao hieu ung am thanh da dang hon
            source.clip = entry.Clip;
            source.Play();
        }

        public void StopBGM(float fadeOutDuration = 0f)
        {
            if (fadeOutDuration <= 0f)
            {
                _bgmSource1.Stop();
                _bgmSource2.Stop();
                return;
            }

            if (_bgmSource1.isPlaying)
            {
                _bgmFadeTween1?.Kill();
                _bgmFadeTween1 = _bgmSource1.DOFade(0f, fadeOutDuration).OnComplete(() => _bgmSource1.Stop());
            }

            if (_bgmSource2.isPlaying)
            {
                _bgmFadeTween2?.Kill();
                _bgmFadeTween2 = _bgmSource2.DOFade(0f, fadeOutDuration).OnComplete(() => _bgmSource2.Stop());
            }
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefMasterVolume, _masterVolume);
            UpdateBGMVolume();
        }

        public void SetBGMVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefBGMVolume, _bgmVolume);
            UpdateBGMVolume();
        }

        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefSFXVolume, _sfxVolume);
        }

        private void UpdateBGMVolume()
        {
            AudioSource activeSource = _isUsingSource1 ? _bgmSource1 : _bgmSource2;
            if (activeSource.isPlaying)
            {
                activeSource.volume = _masterVolume * _bgmVolume;
            }
        }
    }
}
