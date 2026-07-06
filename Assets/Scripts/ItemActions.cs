using UnityEngine;

public static class ItemActions
{
    // Uses an item card. Items cost no mana/stamina. Validates LoseMana effects
    // (e.g. travaso) so they only run when the user has enough mana.
    public static CastResult TryUseItem(HeroState user, HeroState opponent, ItemData item)
    {
        CastResult result = new CastResult();
        if (item == null) return result;

        int manaCost = 0;
        int staminaCost = 0;
        foreach (SpellEffect e in item.effects)
        {
            if (e.type == SpellEffectType.LoseMana) manaCost += e.value;
            if (e.type == SpellEffectType.LoseStamina) staminaCost += e.value;
        }
        if (manaCost > 0 && user.currentMana < manaCost)
        {
            Debug.Log($"[Combat] {user.data.heroName} non ha abbastanza Mana per usare {item.cardName} (richiesti: {manaCost}, disponibili: {user.currentMana}).");
            return result;
        }
        if (staminaCost > 0 && user.currentStamina < staminaCost)
        {
            Debug.Log($"[Combat] {user.data.heroName} non ha abbastanza Stamina per usare {item.cardName} (richiesti: {staminaCost}, disponibili: {user.currentStamina}).");
            return result;
        }

        Debug.Log($"[Combat] {user.data.heroName} usa l'oggetto {item.cardName} ({item.category}, bersaglio: {item.target}).");

        foreach (SpellEffect e in item.effects)
        {
            SpellActions.ApplyEffect(user, opponent, item.cardName, e, result);
        }

        result.success = true;
        return result;
    }
}
