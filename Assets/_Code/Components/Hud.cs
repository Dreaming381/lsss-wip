using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lsss
{
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
    }
}

