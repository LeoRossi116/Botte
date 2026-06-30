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

    [Header("Defensive state")]
    public bool hasShield;              // PreventNextDamage: fully blocks the next single attack
    public int shieldAmount;           // Shield points absorbed until the hero's next turn
    public bool nextAttackUnblockable; // caster's next attack ignores defense & block
    public bool auraBlockFirstAttack;  // aura: block the first attack received each turn
    public bool blockedFirstAttackThisTurn; // runtime: reset each turn
    public int auraWeakenOpponent;     // aura: opponent deals this much less damage per attack

    public List<StatModifier> activeModifiers = new List<StatModifier>();

    // Runtime card bookkeeping (cards are shared ScriptableObjects, so state lives here)
    public List<MagicData> activeAuras = new List<MagicData>();        // aura cards currently active
    public List<MagicData> exhaustedThisRound = new List<MagicData>(); // exhaust cards used this round

    public HeroState(HeroData data)
    {
        this.data = data;
        if (data != null)
        {
            currentHP = data.maxHP;
            currentMana = data.intelligence;
            currentStamina = data.agility;
        }
        activeModifiers = new List<StatModifier>();
        activeAuras = new List<MagicData>();
        exhaustedThisRound = new List<MagicData>();
        hasShield = false;
        shieldAmount = 0;
    }

    public void AddModifier(StatModifier mod)
    {
        activeModifiers.Add(mod);
        Debug.Log($"{data.heroName} riceve {(mod.amount >= 0 ? "+" : "")}{mod.amount} {mod.stat} da {mod.sourceName} (durata: {mod.duration}).");
    }

    public int GetModifiedStrength()
    {
        int total = data.strength;
        foreach (var mod in activeModifiers)
        {
            if (mod.stat == ModifierStat.Strength)
            {
                total += mod.amount;
            }
        }
        return Mathf.Max(0, total);
    }

    public int GetModifiedIntelligence()
    {
        int total = data.intelligence;
        foreach (var mod in activeModifiers)
        {
            if (mod.stat == ModifierStat.Intelligence)
            {
                total += mod.amount;
            }
        }
        return Mathf.Max(0, total);
    }

    public int GetModifiedAgility()
    {
        int total = data.agility;
        foreach (var mod in activeModifiers)
        {
            if (mod.stat == ModifierStat.Agility)
            {
                total += mod.amount;
            }
        }
        return Mathf.Max(0, total);
    }

    public int GetModifiedDefenseBonus()
    {
        int total = 0;
        foreach (var mod in activeModifiers)
        {
            if (mod.stat == ModifierStat.Defense)
            {
                total += mod.amount;
            }
        }
        return total;
    }

    public int GetDamageBonusThisTurn()
    {
        int total = 0;
        foreach (var mod in activeModifiers)
        {
            if (mod.stat == ModifierStat.DamageBonus)
            {
                total += mod.amount;
            }
        }
        return total;
    }

    public void ExpireModifiers(ModifierDuration durationToExpire)
    {
        for (int i = activeModifiers.Count - 1; i >= 0; i--)
        {
            if (activeModifiers[i].duration == durationToExpire)
            {
                string sourceName = activeModifiers[i].sourceName;
                activeModifiers.RemoveAt(i);
                Debug.Log($"{sourceName} su {data.heroName} è scaduto.");
            }
        }
    }
}
