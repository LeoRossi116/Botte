using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HeroState
{
    public HeroData data;
    public int currentHP;
    public int currentMana;
    public int currentStamina;

    /// <summary>1 = player1, 2 = player2. Set by GameState constructor.</summary>
    public int playerIndex;

    /// <summary>Single unified draw pile (spells + equipment + items merged). No size limit.</summary>
    public List<CardData> deck = new List<CardData>();

    /// <summary>Held cards (all types: spells, items, equipment). Preserved in draw order. No size limit.</summary>
    public List<CardData> hand = new List<CardData>();

    /// <summary>Reference to the ONE shared discard pile in GameState. Assigned by GameState constructor.</summary>
    public List<DiscardEntry> sharedDiscardPile;

    // Indexed by EquipmentSlot: WeaponMain, WeaponOff, Head, Torso, Hands, Feet.
    public EquipmentData[] equippedItems = new EquipmentData[6];
    public bool weaponTwoHandedEquipped; // main weapon occupies both weapon slots
    public Dictionary<EquipmentSlot, int> durability = new Dictionary<EquipmentSlot, int>();

    public int poisonStacks;
    public bool isStunned;
    public bool isSilenced;
    public int fatigueCount; // tracks how many times they tried to draw from an empty deck

    [Header("Per-turn / equipment tracking")]
    public bool attackedThisTurn;
    public bool attackedLastTurn;
    public int pendingStaminaPenalty;                 // stun etc. applied at next turn start
    public HashSet<string> cardTypesUsedThisTurn = new HashSet<string>();
    public int manaUsedThisTurn;

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

    // ---------- Shared discard pile helpers ----------

    /// <summary>Adds the card to the shared discard pile tagged with this player's index.</summary>
    public void Discard(CardData card)
    {
        if (sharedDiscardPile != null)
            sharedDiscardPile.Add(new DiscardEntry(card, playerIndex));
    }

    /// <summary>Iterates only the entries in the shared pile that belong to this player.</summary>
    public System.Collections.Generic.IEnumerable<DiscardEntry> MyDiscards()
    {
        if (sharedDiscardPile == null) yield break;
        foreach (var e in sharedDiscardPile)
            if (e.playerIndex == playerIndex) yield return e;
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

    public int GetModifiedMaxHP()
    {
        int total = data.maxHP;
        foreach (var mod in activeModifiers)
            if (mod.stat == ModifierStat.MaxHP) total += mod.amount;
        return Mathf.Max(1, total);
    }

    // Remove all permanent modifiers that came from a given equipment source.
    public void RemovePermanentFromSource(string sourceName)
    {
        for (int i = activeModifiers.Count - 1; i >= 0; i--)
            if (activeModifiers[i].duration == ModifierDuration.Permanent && activeModifiers[i].sourceName == sourceName)
                activeModifiers.RemoveAt(i);
    }

    // ---------- Equipment helpers ----------
    public EquipmentData GetEquipped(EquipmentSlot slot) => equippedItems[(int)slot];

    public EquipmentData MainWeapon => equippedItems[(int)EquipmentSlot.WeaponMain];
    public EquipmentData OffWeapon => equippedItems[(int)EquipmentSlot.WeaponOff];
    public EquipmentData Helmet => equippedItems[(int)EquipmentSlot.Head];
    public EquipmentData Torso => equippedItems[(int)EquipmentSlot.Torso];
    public EquipmentData Gloves => equippedItems[(int)EquipmentSlot.Hands];
    public EquipmentData Boots => equippedItems[(int)EquipmentSlot.Feet];

    public IEnumerable<EquipmentData> AllEquipped()
    {
        for (int i = 0; i < equippedItems.Length; i++)
            if (equippedItems[i] != null) yield return equippedItems[i];
    }

    // Does ANY equipped piece grant this effect?
    public bool HasEquipEffect(EquipEffect fx)
    {
        foreach (var e in AllEquipped()) if (e.specialEffect == fx) return true;
        return false;
    }

    // First equipped piece with this effect (or null).
    public EquipmentData FindEquip(EquipEffect fx)
    {
        foreach (var e in AllEquipped()) if (e.specialEffect == fx) return e;
        return null;
    }

    // Sum of effectValue across equipped pieces with this effect.
    public int SumEquipEffect(EquipEffect fx)
    {
        int total = 0;
        foreach (var e in AllEquipped()) if (e.specialEffect == fx) total += e.effectValue;
        return total;
    }
}
