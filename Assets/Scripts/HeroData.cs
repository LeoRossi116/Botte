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
}
