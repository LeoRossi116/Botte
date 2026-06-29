using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Botte.UI
{
    public class BattleUI : MonoBehaviour
    {
        [Header("Player 1 UI")]
        public TMP_Text p1HeroNameText;
        public RectTransform p1HPBarFill;
        public TMP_Text p1HPText;
        public TMP_Text p1ManaText;
        public TMP_Text p1StaminaText;
        public TMP_Text p1StatusText;

        [Header("Player 2 UI")]
        public TMP_Text p2HeroNameText;
        public RectTransform p2HPBarFill;
        public TMP_Text p2HPText;
        public TMP_Text p2ManaText;
        public TMP_Text p2StaminaText;
        public TMP_Text p2StatusText;

        [Header("Center Panel")]
        public TMP_Text turnText;
        public TMP_Text phaseText;
        public RectTransform logContent;
        public ScrollRect logScrollRect;
        public GameObject winnerOverlay;
        public TMP_Text winnerText;

        public void RefreshHero(HeroState hero, bool isPlayer1)
        {
            TMP_Text nameText = isPlayer1 ? p1HeroNameText : p2HeroNameText;
            RectTransform hpFill = isPlayer1 ? p1HPBarFill : p2HPBarFill;
            TMP_Text hpText = isPlayer1 ? p1HPText : p2HPText;
            TMP_Text manaText = isPlayer1 ? p1ManaText : p2ManaText;
            TMP_Text staminaText = isPlayer1 ? p1StaminaText : p2StaminaText;
            TMP_Text statusText = isPlayer1 ? p1StatusText : p2StatusText;

            nameText.text = hero.data.heroName;
            hpText.text = $"HP: {hero.currentHP} / {hero.data.maxHP}";
            manaText.text = $"Mana: {hero.currentMana} / {hero.data.intelligence}";
            staminaText.text = $"Stamina: {hero.currentStamina} / {hero.data.agility}";
            
            float hpPct = hero.data.maxHP > 0 ? (float)hero.currentHP / hero.data.maxHP : 0f;
            hpFill.anchorMax = new Vector2(hpPct, 1f);
            
            string statusStr = "";
            if (hero.poisonStacks > 0) statusStr += $"Poison ({hero.poisonStacks}) ";
            if (hero.isStunned) statusStr += "Stunned ";
            if (hero.isSilenced) statusStr += "Silenced";
            statusText.text = statusStr.Trim();
        }

        public void AddLog(string message)
        {
            Debug.Log(message);
            GameObject newTextGO = new GameObject("LogText");
            newTextGO.transform.SetParent(logContent, false);
            TextMeshProUGUI txt = newTextGO.AddComponent<TextMeshProUGUI>();
            txt.text = message;
            txt.fontSize = 16f;
            txt.color = Color.white;
            
            Canvas.ForceUpdateCanvases();
            logScrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
