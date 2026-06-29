using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HeroState
{
    public HeroData data;
    public int currentHP;
    public int currentMana;
    public int currentStamina;

    public List<CardData> hand = new List<CardData>();
    public List<CardData> equipmentDeck = new List<CardData>();
    public List<CardData> magicDeck = new List<CardData>();
    public List<CardData> discardPile = new List<CardData>();
    public EquipmentData[] equippedItems = new EquipmentData[6];
    public List<MagicData> spellbook = new List<MagicData>(3);

    public int poisonStacks;
    public bool isStunned;
    public bool isSilenced;

    public HeroState(HeroData data)
    {
        this.data = data;
        if (data != null)
        {
            currentHP = data.maxHP;
            currentMana = data.intelligence;
            currentStamina = data.agility;
        }
    }
}
