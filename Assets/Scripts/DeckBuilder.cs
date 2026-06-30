using UnityEngine;
using System.Collections.Generic;

public static class DeckBuilder
{
    public static List<CardData> BuildSpellDeck(HeroClass heroClass)
    {
        List<CardData> deck = new List<CardData>();
        string classFolderName = heroClass.ToString();
        string[] spellAssetNames = GetSpellAssetNames(heroClass);

        foreach (string spellName in spellAssetNames)
        {
            MagicData card = Resources.Load<MagicData>($"Spells/{classFolderName}/{spellName}");
            if (card != null)
            {
                deck.Add(card);
            }
            else
            {
                Debug.LogWarning($"Could not load spell asset: Spells/{classFolderName}/{spellName}");
            }
        }
        return deck;
    }

    public static bool CanDrawFromDeck(HeroState hero, MagicData card)
    {
        if (card == null) return false;
        return card.cardClass == CardClass.Shared || card.cardClass.ToString() == hero.data.heroClass.ToString();
    }

    private static string[] GetSpellAssetNames(HeroClass heroClass)
    {
        switch (heroClass)
        {
            case HeroClass.Warrior:
                return new string[] { "UrloDelBarbaro", "PugnoDelPaladino", "DifesaImperturbabile", "Ristoro", "GridoDiGuerra", "Terrore", "AffondoPoderoso", "ColpoMirato" };
            case HeroClass.Mage:
                return new string[] { "DardoDiFuoco", "PallaDiFuoco", "DrenaggioVitale", "ScudoArcano", "Lentezza", "Debolezza", "Concentrazione", "Teletrasporto" };
            case HeroClass.Rogue:
                return new string[] { "VelenoRapido", "Elusione", "PassoDOmbra", "PuntoDebole", "AffondoPerforante", "RistoroVeloce", "Gambetto", "Prontezza" };
            case HeroClass.Necro:
                return new string[] { "ToccoPutrido", "DrenaggioDiVita", "BanchettoDiAnime", "PattoDiSangue", "ComunioneOscura", "Maledizione", "TerroreMortale", "VeloDellaMorte" };
            default:
                return new string[0];
        }
    }
}
