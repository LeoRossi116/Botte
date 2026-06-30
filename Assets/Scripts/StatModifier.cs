using UnityEngine;

[System.Serializable]
public class StatModifier
{
    public string sourceName;       // name of the card/effect that applied it, for logging
    public ModifierStat stat;       // which stat is affected
    public int amount;              // can be negative for debuffs
    public ModifierDuration duration;

    public StatModifier(string sourceName, ModifierStat stat, int amount, ModifierDuration duration)
    {
        this.sourceName = sourceName;
        this.stat = stat;
        this.amount = amount;
        this.duration = duration;
    }
}

public enum ModifierStat { Strength, Intelligence, Agility, Defense, DamageBonus }
public enum ModifierDuration { UntilNextOpponentTurn, EndOfThisTurn, UntilNextAttack }
