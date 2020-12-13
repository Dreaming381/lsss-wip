using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lsss
{
    [AddComponentMenu("LSSS/UI/HUD")]
    public class Hud : MonoBehaviour
    {
        public RectTransform boostBar;
        public TMP_Text      health;
        public TMP_Text      bulletCount;
        public TMP_Text      factions;
        public Image         blackFade;
        public float         blackFadeControl = 1f;
        public float         blackFadeOutTime = 0.5f;
        public float         blackFadeInTime  = 0.5f;

        private void Awake()
        {
            blackFade.color = new Color(0f, 0f, 0f, 1f);
        }
    }
}

