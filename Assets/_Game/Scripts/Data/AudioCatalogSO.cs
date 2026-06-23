using System;
using System.Collections.Generic;
using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Data
{
    [Serializable]
    public class AudioEntry
    {
        public SoundType Type;
        public AudioClip Clip;
        [Range(0f, 1f)] public float VolumeScale = 1f;
    }

    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "Dungeon Builder/Data/Audio Catalog")]
    public sealed class AudioCatalogSO : ScriptableObject
    {
        [Header("BGM")]
        public AudioEntry[] BgmEntries;

        [Header("SFX")]
        public AudioEntry[] SfxEntries;

        private Dictionary<SoundType, AudioEntry> _lookupCache;

        /// <summary>
        /// Lấy thông tin âm thanh dựa vào SoundType.
        /// </summary>
        public bool TryGetEntry(SoundType type, out AudioEntry entry)
        {
            if (_lookupCache == null)
            {
                InitializeCache();
            }

            return _lookupCache.TryGetValue(type, out entry);
        }

        private void InitializeCache()
        {
            _lookupCache = new Dictionary<SoundType, AudioEntry>();

            if (BgmEntries != null)
            {
                foreach (var entry in BgmEntries)
                {
                    if (entry != null && entry.Clip != null && !_lookupCache.ContainsKey(entry.Type))
                    {
                        _lookupCache.Add(entry.Type, entry);
                    }
                }
            }

            if (SfxEntries != null)
            {
                foreach (var entry in SfxEntries)
                {
                    if (entry != null && entry.Clip != null && !_lookupCache.ContainsKey(entry.Type))
                    {
                        _lookupCache.Add(entry.Type, entry);
                    }
                }
            }
        }
    }
}
