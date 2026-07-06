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
                for (int i = 0; i < card.DeckCount; i++) deck.Add(card);
            }
            else
            {
                Debug.LogWarning($"Could not load spell asset: Spells/{classFolderName}/{spellName}");
            }
        }
        return deck;
    }

    // Builds the shared item deck by loading every ItemData asset under Resources/Items.
    public static List<CardData> BuildItemDeck()
    {
        List<CardData> deck = new List<CardData>();
        ItemData[] items = Resources.LoadAll<ItemData>("Items");
        foreach (ItemData item in items)
        {
            if (item != null)
                for (int i = 0; i < item.DeckCount; i++) deck.Add(item);
        }
        if (items.Length == 0) Debug.LogWarning("No ItemData assets found under Resources/Items.");
        return deck;
    }

    // Builds a hero's class-based equipment deck from Resources/Equipment/[Class].
    public static List<CardData> BuildEquipmentDeck(HeroClass heroClass)
    {
        List<CardData> deck = new List<CardData>();
        EquipmentData[] pieces = Resources.LoadAll<EquipmentData>($"Equipment/{heroClass}");
        foreach (EquipmentData eq in pieces)
        {
            if (eq != null)
                for (int i = 0; i < eq.DeckCount; i++) deck.Add(eq);
        }
        if (pieces.Length == 0) Debug.LogWarning($"No EquipmentData assets found under Resources/Equipment/{heroClass}.");
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
                return new string[] { "AffondoPoderoso", "Armageddon", "ColpoMirato", "DifesaImperturbabile", "Distruzione", "Fortezza", "Frenesia", "Furia", "GridoDiGuerra", "Guarigione", "GuarigioneDivina", "Impatto", "OndaDUrto", "PartoDellaSofferenza", "ProiezioneDelMartelloDistruttore", "PugnoDelPaladino", "RiflessiDiBattaglia", "Rigenerazione", "Ristoro", "Sacrificio", "SecondaPelle", "SeteDiBattaglia", "Slancio", "Spallata", "SpezzareLIncantesimo", "Tempra", "Terremoto", "Terrore", "UltimaDifesa", "UrloDelBarbaro" };
            case HeroClass.Mage:
                return new string[] { "BaglioreAccecante", "BarrieraDiMana", "Cataclisma", "CateneArcane", "Concentrazione", "DardoDiFuoco", "Debolezza", "Disincanto", "DrenaggioVitale", "EcoArcana", "FulmineIncatenato", "Incenerimento", "Intuizione", "Lentezza", "Meditazione", "OndaGelida", "PallaDiFuoco", "PotenziamentoArcano", "RisucchioDiMana", "Ruggine", "SapienzaArcana", "ScaricaElettrica", "ScheggiaDiGhiaccio", "ScudoArcano", "SigilloDiSilenzio", "Surriscaldamento", "Teletrasporto", "TempestaDiMeteore", "VampirismoArcano", "Vortice" };
            case HeroClass.Rogue:
                return new string[] { "AffondoPerforante", "Agguato", "Bavaglio", "Borseggio", "Cancrena", "ColpoBasso", "ColpoGemello", "DoppioAffondo", "Elusione", "Fumogeno", "Gambetto", "Indebolimento", "IntingereLeLame", "LamaAvvelenata", "LameSottili", "MaestriaDelVeleno", "ManoLesta", "NubeTossica", "PassoDOmbra", "Prontezza", "PugnalataAlleSpalle", "PuntoDebole", "RiflessiFelini", "Sabotaggio", "SchivataAcrobatica", "TempestaDiLame", "TendereLAgguato", "TossinaParalizzante", "VelenoFulminante", "VelenoRapido" };
            case HeroClass.Necro:
                return new string[] { "AnimaInCambio", "AuraDiPutrefazione", "BanchettoDiAnime", "ComunioneOscura", "Contagio", "Debilitazione", "DrenaggioDiVita", "EvocaScheletro", "EvocaSpettro", "EvocaZombie", "FurtoDAnima", "GolemDOssa", "LegioneSpettrale", "Maledizione", "MaledizionePersistente", "OrdaDiNonMorti", "PattoDiSangue", "Peste", "Premonizione", "RaccoltaDOssa", "RecuperoMacabro", "RianimaIlCaduto", "RichiamoDalCimitero", "RitoProibito", "SeteDiMorte", "SignoreDeiNonMorti", "Tenebre", "TerroreMortale", "ToccoPutrido", "VeloDellaMorte" };
            default:
                return new string[0];
        }
    }
}
