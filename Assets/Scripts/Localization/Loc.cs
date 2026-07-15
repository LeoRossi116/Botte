using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight localization facade. Italian is the authoring language (all source strings
/// in code and in ScriptableObjects are Italian); English is provided via lookup tables.
///
/// Card names and descriptions come from Resources/Localization/cards_en.json.
/// User-facing UI/code strings come from the embedded <see cref="UiEn"/> table.
///
/// Usage:
///   Loc.T("Non è il tuo turno!")            -> UI string, translated when language == English
///   Loc.CardName(card.cardName)             -> localized card name
///   Loc.CardDesc(card.effectDescription)    -> localized card description
///   string.Format(Loc.T("{0} tiene {1}."), name, Loc.CardName(cardName))
///
/// When a translation is missing, the original Italian text is returned (safe fallback).
/// </summary>
public static class Loc
{
    [Serializable] private class Pair { public string k; public string v; }
    [Serializable] private class Table { public Pair[] names; public Pair[] descs; }

    private static bool _cardsLoaded;
    private static readonly Dictionary<string, string> _names = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> _descs = new Dictionary<string, string>();

    /// <summary>Raised whenever the active language changes (mirror of GameSettings.LanguageChanged).</summary>
    public static event Action LanguageChanged;

    static Loc()
    {
        GameSettings.LanguageChanged += _ => LanguageChanged?.Invoke();
    }

    public static Language Current => GameSettings.CurrentLanguage;

    public static void SetLanguage(Language lang) => GameSettings.CurrentLanguage = lang;

    // ---------- Card names / descriptions (data-driven) ----------
    private static void EnsureCardsLoaded()
    {
        if (_cardsLoaded) return;
        _cardsLoaded = true;
        TextAsset ta = Resources.Load<TextAsset>("Localization/cards_en");
        if (ta == null) { Debug.LogWarning("[Loc] Missing Resources/Localization/cards_en.json"); return; }
        Table table = JsonUtility.FromJson<Table>(ta.text);
        if (table?.names != null)
            foreach (var p in table.names)
                if (!string.IsNullOrEmpty(p.k) && !_names.ContainsKey(p.k)) _names[p.k] = p.v;
        if (table?.descs != null)
            foreach (var p in table.descs)
                if (!string.IsNullOrEmpty(p.k) && !_descs.ContainsKey(p.k)) _descs[p.k] = p.v;
    }

    public static string CardName(string italian)
    {
        if (string.IsNullOrEmpty(italian) || Current == Language.Italian) return italian;
        EnsureCardsLoaded();
        return _names.TryGetValue(italian, out var v) && !string.IsNullOrEmpty(v) ? v : italian;
    }

    public static string CardDesc(string italian)
    {
        if (string.IsNullOrEmpty(italian) || Current == Language.Italian) return italian;
        EnsureCardsLoaded();
        return _descs.TryGetValue(italian, out var v) && !string.IsNullOrEmpty(v) ? v : italian;
    }

    // ---------- UI / code strings (embedded table) ----------
    /// <summary>Translate a UI/code string. Pass the Italian source text as the key.</summary>
    public static string T(string italian)
    {
        if (string.IsNullOrEmpty(italian) || Current == Language.Italian) return italian;
        return UiEn.TryGetValue(italian, out var v) && !string.IsNullOrEmpty(v) ? v : italian;
    }

    // Italian -> English for all user-facing UI/code strings. Keys are the exact Italian source
    // strings (including any {0}/{1} placeholders used with string.Format).
    private static readonly Dictionary<string, string> UiEn = new Dictionary<string, string>
    {
        // Books
        { "Libro Incantesimi", "Spell Book" },
        { "Libro Equipaggiamento", "Equipment Book" },
        { "Libro Oggetti", "Item Book" },

        // Status effects
        { "Veleno", "Poison" },
        { "Scudo", "Shield" },
        { "Blocco", "Block" },
        { "Perforante", "Piercing" },
        { "Riflessi", "Reflexes" },
        { "Indebol", "Weaken" },

        // Stats / labels
        { "Statistiche", "Stats" },
        { "Forza", "Strength" },
        { "Int", "Int" },
        { "Vel", "Spd" },
        { "Intelligenza", "Intelligence" },
        { "Velocità", "Speed" },
        { "HP max", "Max HP" },
        { "Danno", "Damage" },
        { "Difesa", "Defense" },
        { "Durabilità", "Durability" },
        { "Requisiti", "Requirements" },
        { "Oggetto", "Item" },
        { "ATTIVA", "ACTIVE" },
        { "USATA", "USED" },

        // Player fallback labels (NOT real usernames)
        { "Giocatore 1", "Player 1" },
        { "Avversario", "Opponent" },

        // Equipment slot placeholders (empty squares)
        { "Testa", "Head" },
        { "Torso", "Torso" },
        { "Mani", "Hands" },
        { "Piedi", "Feet" },
        { "Arma principale", "Main weapon" },
        { "Arma secondaria", "Off-hand" },

        // Equipment window / options
        { "Equipaggiamento di {0}", "{0}'s equipment" },
        { "Opzioni", "Options" },
        { "Lingua", "Language" },
        { "Equipaggia", "Equip" },

        // Peek / draw
        { "Cima del mazzo:", "Top of the deck:" },
        { "Tenere o scartare?", "Keep or discard?" },

        // Draw-choice panel title (mandatory prep draws)
        { "Prima Pescata", "First Draw" },
        { "Seconda Pescata", "Second Draw" },

        // Round & phase window (top panel)
        { "Turno {0} — tocca a {1}", "Turn {0} — {1}'s turn" },
        { "Fase: {0}", "Phase: {0}" },
        { "Recupero Risorse", "Resource Recovery" },
        { "Preparazione", "Preparation" },
        { "Combattimento", "Combat" },
        { "Fase Finale", "End Phase" },

        // Character select
        { "Scegli il Tuo Eroe", "Choose Your Hero" },

        // Settings window
        { "impostazioni", "settings" },

        // Tutorial button hints
        { "Utilizza carta equipaggiamento, fino a 2 volte per turno",
          "Use an equipment card, up to 2 times per turn" },
        { "Infliggi danno all'Avversario", "Deal damage to the Opponent" },
        { "Pesca una carta da un mazzo a scelta", "Draw a card from a deck of your choice" },
        { "Passa alla fase successiva", "Advance to the next phase" },
        { "Passa alla Fase di Combattimento", "Advance to the Combat phase" },
        { "Passa alla Fase Finale", "Advance to the End phase" },
        { "Inizia il turno dell'Avversario", "Start the Opponent's turn" },
        { "Mostra il Libro Incantesimi (magie)", "Show the Spell Book (magic)" },
        { "Mostra il Libro Equipaggiamento", "Show the Equipment Book" },
        { "Mostra il Libro Oggetti", "Show the Item Book" },

        // Battle log messages
        { "Non è il tuo turno!", "It's not your turn!" },
        { "Puoi equipaggiare solo in Preparazione (in combattimento l'equipaggiamento si può solo ispezionare).",
          "You can only equip during Preparation (in combat you can only inspect equipment)." },
        { "{0} ha già equipaggiato 2 pezzi questo turno.", "{0} has already equipped 2 pieces this turn." },
        { "Premi il pulsante Equipaggia per attivare la modalità (serve il Libro Equipaggiamento e gli slot mostrati).",
          "Press the Equip button to activate the mode (requires the Equipment Book and the shown slots)." },
        { "Puoi usare le carte solo durante la fase di Combattimento!", "You can only use cards during the Combat phase!" },
        { "{0} è un'aura già attiva.", "{0} is an aura that is already active." },
        { "{0} è già stata usata questo round.", "{0} has already been used this round." },
        { "Puoi usare gli oggetti solo durante la fase di Combattimento!", "You can only use items during the Combat phase!" },
        { "Puoi scartare le carte solo durante il tuo turno!", "You can only discard cards during your turn!" },
        { "Puoi disequipaggiare/scartare equipaggiamento solo durante il tuo turno!",
          "You can only unequip/discard equipment during your turn!" },
        { "{0} non ha carte negli scarti.", "{0} has no cards in the discard pile." },
        { "{0} non ha carte negli scarti da rubare.", "{0} has no cards in the discard pile to steal." },
        { "Il mazzo oggetti è vuoto.", "The item deck is empty." },
        { "Il mazzo equipaggiamento è vuoto.", "The equipment deck is empty." },
        { "Il mazzo incantesimi di {0} è vuoto.", "{0}'s spell deck is empty." },
        { "{0} tiene l'oggetto {1}.", "{0} keeps the item {1}." },
        { "Libro pieno: {0} viene scartata invece di essere tenuta.", "Book full: {0} is discarded instead of being kept." },
        { "{0} tiene {1}.", "{0} keeps {1}." },
        { "{0} scarta {1} dalla cima del mazzo.", "{0} discards {1} from the top of the deck." },
        { "Non puoi scegliere lo stesso eroe dell'altro giocatore ({0})!",
          "You can't pick the same hero as the other player ({0})!" },
        { "{0} non può equipaggiare {1}: richiede {2} {3} (attuale: {4}).",
          "{0} cannot equip {1}: requires {2} {3} (current: {4})." },

        // Menu / connect screen (SceneUIManager)
        { "Inserisci il tuo nome prima di unirti o ospitare una partita.",
          "insert your name before joining or hosting a game" },
        { "Non sei connesso ai servizi online.", "Not signed in to online services." },
        { "Impossibile avviare l'host.", "Failed to start host." },
        { "Impossibile creare una stanza.", "Failed to create a room." },
        { "Inserisci prima un codice stanza.", "Enter a room code first." },
        { "Impossibile avviare il client.", "Failed to start client." },
        { "Stanza non trovata! Controlla il codice.", "Lobby not found! Check your code." },
        { "Connessione scaduta.", "Connection timed out." },
        { "Impossibile sincronizzarsi con l'host.", "Could not sync with host." },
        { "Servizi online non disponibili: {0}", "Online services failed: {0}" },
        { "Errore", "Error" },

        // Buttons (exact-case keys: the dictionary lookup is case-sensitive, so each button's
        // authored Italian text maps to an English value that mirrors its capitalization style).
        // Main menu / play / lobby screens
        { "gioca", "play" },
        { "opzioni", "options" },
        { "esci", "quit" },
        { "Esci", "Quit" },
        { "unisciti", "join" },
        { "crea lobby\n", "create lobby\n" },
        { "cancella", "clear" },
        { "Inserisci Nome", "Enter Name" },
        { "Avvia", "Start" },
        // Character select
        { "INIZIA", "START" },
        // Draw-choice panel
        { "Magie", "Spells" },
        { "EQUIPAGGIAMENTO", "EQUIPMENT" },
        { "OGGETTI", "ITEMS" },
        // Peek panel
        { "TIENI", "KEEP" },
        { "SCARTA", "DISCARD" },
        // In-battle action buttons
        { "Attacca", "Attack" },
        { "Prossima Fase", "Next Phase" },
        { "Dormi", "Sleep" },
        { "Pescata Extra", "Extra Draw" },
        // In-battle options window
        { "OPZIONI", "OPTIONS" },
        { "IMPOSTAZIONI", "SETTINGS" },
        { "ESCI", "QUIT" },
        // End-game (winner) overlay
        { "RICOMINCIA", "RESTART" },
        { "RIGIOCA", "REPLAY" },
        { "LOBBY", "LOBBY" },
    };
}
