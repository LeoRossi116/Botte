using UnityEngine;

[CreateAssetMenu(fileName = "NewHeroData", menuName = "Botte/HeroData")]
public class HeroData : ScriptableObject
{
    public string heroName;
    public HeroClass heroClass;
    public int maxHP;
    public int strength;
    public int intelligence;
    public int agility;

    [Header("Visual")]
    [Tooltip("Optional hero portrait. When set, the battle hero placeholder uses this sprite. " +
             "If left empty, BattleUI falls back to the per-class portrait mapping.")]
    public Sprite heroTexture;
}
