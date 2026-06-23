using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Audio
{
    public sealed class MenuAudioController : MonoBehaviour
    {
        private void Start()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(SoundType.BGM_Main_Menu, 2f);
            }
        }
    }
}
