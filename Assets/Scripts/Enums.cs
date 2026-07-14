public enum HeroClass { Warrior, Mage, Rogue, Necro }

// Supported UI/content languages. Italian is the authoring/default language.
public enum Language { Italian, English }

// Card rarity governs how many copies of a card go into a deck.
// NOTE: values are serialized in .asset files, so only append new entries at the end.
public enum Rarity { Common, Uncommon, Rare, Legendary }
public enum CardType { Equipment, Magic, Consumable }
public enum CardClass { Shared, Warrior, Mage, Rogue, Necro }
public enum EquipmentSlot { WeaponMain, WeaponOff, Head, Torso, Hands, Feet }
public enum MagicType { Repeatable, Aura, Exhaustion, Instant }
public enum GamePhase { ResourceRecovery, Preparation, Combat, EndPhase }
public enum ItemCategory { Damage, Heal, Resource, Combat, Equipment, Attribute, Deck, Graveyard, Reactive, Extra }
public enum CardTarget { Self, Opponent, PlayerChoice }
public enum BookType { Spell, Equipment, Item }
public enum DeckChoice { Spell, Equipment, Item }

// Equipment classification.
public enum EquipmentType { Armor, Utility, Weapon }

// The slot an equipment card declares it goes into.
public enum EquipSlotType { Torso, Helmet, Boots, Gloves, OneHandWeapon, TwoHandWeapon }

// Which max-stat an equipment attribute modifies.
// NOTE: values are serialized in .asset files, so only append new entries at the end.
public enum EquipAttribute { MaxHP, MaxMana, MaxStamina, Damage, Strength }

// The hero stat an equipment card requires the wearer to have (shown in hero select):
// Strength, Intelligence (= max mana), Speed (= max stamina / agility).
public enum RequirementStat { Strength, Intelligence, Speed }

// Hardcoded special effects an equipment piece can grant.
public enum EquipEffect
{
    None,
    SacrificeBlockThenBreak,        // block one full hit, then the piece breaks
    ReduceStaminaCost,              // -1 stamina cost on stamina actions (min 1)
    WieldTwoHandInOneHand,          // a 2H weapon only occupies the main hand
    PoisonOnHit,                    // apply value poison to defender on weapon hit
    SilenceOnHit,                   // defender cannot cast spells next turn
    StunOnHit,                      // defender loses value stamina next turn
    BonusDamageIfOpponentLowHP,     // +value weapon damage if opponent < 50% HP
    SpellDamageBonus,               // +value damage to the caster's direct-damage spells
    WeaponBypassDefense,            // this weapon's damage ignores defense/block
    OnDiscardGainMana,             // +value mana when this card is discarded
    OnDiscardDamage,               // deal value damage to opponent when discarded
    ShieldEachTurn,                 // +value shield at the start of each of the owner's turns
    ChanceShieldEachTurn,           // effectValue2 % chance to gain value shield each turn
    ShieldPerManaUsed,              // +2 shield for every 2 mana spent
    ExtraManaEachTurn,              // +value mana each turn (after recovery)
    ManaIfNoAttack,                 // +value mana next turn if no attack was made this turn
    ShieldPerPhysicalHitTaken,      // +value shield each time a physical hit is taken
    AttackTwice,                    // physical attacks strike twice
    PoisonOnHitTaken,               // apply value poison to attacker when taking a physical hit
    BonusDamageIfOpponentPoisoned,  // +value weapon damage if opponent is poisoned
    ExtraPoisonStack,               // whenever the owner applies poison, apply value more
    BlockEachRound,                 // gain a full block at the start of each turn
    SynergyDamage,                  // +value weapon damage this turn if >=2 card types used
    LifeOnAttack,                   // +value HP for each attack performed
    DamageAtHPCost,                 // +value weapon damage, but lose effectValue2 HP per attack
    ReaperVsPoisoned,               // +value damage and +effectValue2 HP if opponent poisoned
    PoisonOnSpell,                  // every spell cast applies value poison to opponent
    DamagePerPoisonStack,           // +value weapon damage per poison stack on opponent
    PoisonImmune,                   // owner cannot be poisoned
    ManaIfLowerHP                   // +value mana at round start if owner HP < opponent HP
}
