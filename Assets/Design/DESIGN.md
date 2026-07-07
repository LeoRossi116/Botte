# Game Design — Battle Panel Redesign

**Scope:** In-game **battle panel only**. Main menu, lobby, play, and character-select screens are out of scope and untouched by this spec.

**Domains covered:** UI Design, Game Feedback. (Asset Design is not a required domain here; a short *Assets referenced* note lives under Game Feedback for animation/feedback dependencies.)

**Goal:** Make the battle read like a physical tabletop card duel — a central table surface as the stage, heroes standing on it, decks fanned in triangles, cards as tactile objects — while extending the existing dark theme. Per-client perspective: **the local player always sees their own hero and cards on the LEFT, the opponent on the RIGHT.**

**Target canvas (fixed):** uGUI, ScreenSpaceOverlay, CanvasScaler = ScaleWithScreenSize, reference resolution **1280×720**, match **0.5**. All pixel values below are at 1280×720; fractions are of the full screen (origin bottom-left, so `y=0` is screen bottom, `y=1` is top).

**Coordinate convention:** Unless noted, a point-anchored element uses `anchorMin = anchorMax = (fx, fy)`, `anchoredPosition = (0,0)`, and the listed `pivot` + `sizeDelta`. This centers the element on the fractional screen point (or aligns it to that point per the pivot). Stretch elements list explicit `anchorMin`/`anchorMax`.

**Note for planning:** the current scene has NO perspective swap (P1 is hard-left for every client) and NO table/hero-sprite/deck/scoreboard elements — all table-stage elements are net-new. Runtime `GameObject.Find` paths (`Canvas/BattleScreen/CenterPanel`, `Canvas/BattleScreen`) and hardcoded desc/equip constants in `BattleUI` will need updating when the hierarchy is restructured. These are implementation concerns for the developer; this doc defines intent and geometry only.

---

## Z-Order / Layer Structure (required)

Front-to-back the game must render: **(closest to player) player's own cards → table + everything on it → opponent's cards (backmost).** In uGUI ScreenSpaceOverlay, later siblings render in front. Structure `BattleScreen` with these child containers in this exact sibling order (index 0 = first child = backmost):

| Sibling idx | Container | Renders | Purpose |
|---|---|---|---|
| 0 | **OpponentLayer** | Opponent's card row (top-right) | Backmost per requirement |
| 1 | **TableLayer** | Table surface + heroes + stat bars + hero popups + all decks + item deck + on-table buttons (action + book) + equipment window | The stage; everything here sits on top of the table image |
| 2 | **HudLayer** | Scoreboard, Options button, Inspect box, Game log | Screen HUD, always readable (never overlaps player cards) |
| 3 | **PlayerLayer** | Player's own drawn cards (bottom-left) | **Frontmost** per requirement |
| 4 | **OverlayLayer** | Turn-change banner, winner overlay | Transient full-attention moments, above all |

Within **TableLayer**, order children so the table image is first (back), then decks, then heroes, then stat bars/popups, then on-table buttons, then equipment window (front). A hovered player card temporarily reparents/raises to the top of PlayerLayer while hovered (see Game Feedback).

---

## UI Design

### Color system

Extends the existing dark theme. Existing tokens are reused verbatim; two derived tokens are added (marked *derived*) to complete the surface ramp and the deck coding.

**Surface hierarchy (tonal steps, structure via tone not borders):**
| Token | Hex | Use |
|---|---|---|
| Base background | `#1A1A2E` | Screen backdrop behind the table |
| Table felt | `#16213E` | Table surface, panels (darkened toward `#0F1428` at edges as a soft vignette) |
| Inset / deep | `#0F1428` | Recessed content: inspect box, log, popups, equip window, input wells |
| Raised surface *(derived)* | `#1F2A4D` | Buttons, cards, deck bodies at rest (one step up from felt) |
| Hover surface *(derived)* | `#273458` | Hover/raised state of interactive surfaces |

**Text (tinted toward palette, never pure grey/white):**
| Token | Hex | Use |
|---|---|---|
| Text primary | `#EAECF5` | Names, headings, values |
| Text secondary | `#9AA3C2` | Units, labels, muted log lines |

**Accent / intent mapping:**
| Token | Hex | Reserved for |
|---|---|---|
| Accent red | `#E94560` | High-stakes only: Attack action, danger, errors, critical timer |
| Secondary blue | `#0F3460` | Primary/neutral actions (Draw, phase advance) — used as gradient bottom |
| Amber | `#F5A623` | Cost values, Equip identity, warning timer |
| Green | `#2ECC71` | HP, heal, "YOUR TURN", positive confirmation |
| HP-bar-bg grey | `#333333` | Stat-bar backing troughs |

**Class colors (hero identity — rims, name text, popup top-border):** Warrior `#E94560`, Rogue `#2980B9`, Mage `#30E3CA`, Necro `#8A39E8`.

**Deck color coding:**
| Deck | Hex | Note |
|---|---|---|
| Spell | `#8A39E8` | Existing Necro purple reused as the "spell" token |
| Equip | `#F5A623` | Existing amber ("orange") |
| Discard | `#3A3D52` *(derived)* | Tinted grey, not pure `#333` — reads neutral on felt |
| Item (shared) | `#F5C842` *(derived)* | Brighter **gold**, distinct from amber Equip |

### Typography

Three roles (assign to the project's TMP fonts; do not hard-require a specific font asset):
- **Display** (bold, structural): scoreboard turn label, hero names, winner text, damage numbers.
- **Body** (readable sans): inspect-box effect text, log lines, popup stat lines.
- **Label/Numeric** (legible small, tabular): cost line `M:/S:`, units, timer, stat values.

Size scale (extends current usage): Display L 30, Display M 22, Body 15, Label 13, Micro 11. Exaggerate numeric prominence (timer, damage numbers use Display sizes; their units use Micro).

### Layout — master coordinate table

All entries live under `BattleScreen`; the **Layer** column is the sibling container from the Z-Order section.

| Element | Layer | anchorMin | anchorMax | pivot | anchoredPos | sizeDelta (px) | Screen rect (px) | Tier | Notes |
|---|---|---|---|---|---|---|---|---|---|
| **Table surface** | Table | (0, 0.333) | (1, 0.667) | (0.5,0.5) | (0,0) | (0,0) stretch | x 0–1280, y 240–480 | core | Middle horizontal band. Felt `#16213E`, edge vignette to `#0F1428`, corner radius 0 (full-bleed), subtle top-edge inner highlight |
| **Local hero sprite** | Table | (0.22,0.34) | (0.22,0.34) | (0.5,0) | (0,0) | (150,210) | feet@(282,245), top≈455 | core | LEFT of table, off-border and off-center. UI Image driven by Animator |
| **Opponent hero sprite** | Table | (0.78,0.34) | (0.78,0.34) | (0.5,0) | (0,0) | (150,210) | feet@(998,245) | core | RIGHT of table (mirror) |
| **Local stat bars** | Table | (0.22,0.635) | (0.22,0.635) | (0.5,0) | (0,0) | (100,28) | ~x232–332, y457–485 | core | Floats just above local hero (see Stat bars) |
| **Opponent stat bars** | Table | (0.78,0.635) | (0.78,0.635) | (0.5,0) | (0,0) | (100,28) | ~x948–1048, y457–485 | core | Above opponent hero |
| **Local hero popup** | Table | (0.30,0.70) | (0.30,0.70) | (0,1) | (0,0) | (190,150) | x384–574, y354–504 | core | Hover-only; opens up-right so it never covers the sprite |
| **Opponent hero popup** | Table | (0.70,0.70) | (0.70,0.70) | (1,1) | (0,0) | (190,150) | x706–896, y354–504 | core | Hover-only; opens up-left |
| **Local decks — back (Spell)** | Table | (0.115,0.60) | (0.115,0.60) | (0.5,0.5) | (0,0) | (58,80) | center (147,432) | core | rot **−6°**. Top-left of table, behind hero |
| **Local decks — front-L (Equip)** | Table | (0.085,0.55) | (0.085,0.55) | (0.5,0.5) | (0,0) | (58,80) | center (109,396) | core | rot **−12°** |
| **Local decks — front-R (Discard)** | Table | (0.145,0.55) | (0.145,0.55) | (0.5,0.5) | (0,0) | (58,80) | center (186,396) | core | rot **0°**. Triangle = 1 back + 2 front |
| **Opp decks — back (Spell)** | Table | (0.885,0.40) | (0.885,0.40) | (0.5,0.5) | (0,0) | (58,80) | center (1133,288) | core | rot **+6°**. Bottom-right of table |
| **Opp decks — front-R (Equip)** | Table | (0.915,0.45) | (0.915,0.45) | (0.5,0.5) | (0,0) | (58,80) | center (1171,324) | core | rot **+12°** |
| **Opp decks — front-L (Discard)** | Table | (0.855,0.45) | (0.855,0.45) | (0.5,0.5) | (0,0) | (58,80) | center (1094,324) | core | rot **0°** (mirrored triangle) |
| **Item deck (shared)** | Table | (0.5,0.60) | (0.5,0.60) | (0.5,0.5) | (0,0) | (66,88) | center (640,432) | core | Top-CENTER of table, **gold `#F5C842`**, upright (rot 0). Same spot on BOTH screens — not mirrored |
| **Action button bar** | Table | (0.5,0.333) | (0.5,0.333) | (0.5,0) | (0,10) | (520,52) | x380–900, y250–302 | core | Bottom-center of table. Hosts Draw / End Turn / phase buttons (see Components) |
| **Local book-change buttons** | Table | (0,0.333) | (0,0.333) | (0,0) | (16,8) | (168,40) | x16–184, y248–288 | core | Bottom-LEFT of table, just above player cards. 3 buttons: Spell / Equip / Item. Replaces the removed separate "equipment" button |
| **Opponent book-change buttons** | Table | (1,0.667) | (1,0.667) | (1,1) | (-16,-8) | (168,40) | x1096–1264, y432–472 | core | Top-RIGHT of table, mirrored. Switches which opponent deck is shown |
| **Equipment window** | Table | (0.5,0.5) | (0.5,0.5) | (0.5,0.5) | (0,0) | (380,200) | x450–830, y260–460 | core | Centered on table. Opens on hero click; auto-closes at end of turn or when opening the other hero's equipment |
| **Player cards (hand)** | Player | (0,0) | (0,0) | (0,0) | (16,12) | height 156, width dynamic | starts x16, y12–168 | core | Bottom-LEFT. Overlapping hand (see Cards). Frontmost layer |
| **Opponent cards (hand)** | Opponent | (1,1) | (1,1) | (1,1) | (-16,-64) | (430,132) | x834–1264, y524–656 | core | Top-RIGHT of screen. Backmost layer. Card size ~84×120, overlapping |
| **Scoreboard** | Hud | (0.5,1) | (0.5,1) | (0.5,1) | (0,-8) | (380,60) | x450–830, y652–712 | core | Top-CENTER. 3 info items (see Scoreboard) |
| **Options button** | Hud | (0,1) | (0,1) | (0,1) | (12,-12) | (112,40) | x12–124, y668–708 | core | Top-LEFT. Unchanged from current |
| **Inspect / description box** | Hud | (0.5,0) | (0.5,0) | (0.5,0) | (0,12) | (344,184) | x468–812, y12–196 | core | Bottom-CENTER. Card name, cost line `M:/S:`, effect/requirements text |
| **Game log** | Hud | (1,0) | (1,0) | (1,0) | (-16,12) | (300,184) | x964–1264, y12–196 | core | Bottom-RIGHT. Scrollable |
| **Turn-change banner** | Overlay | (0.5,0.62) | (0.5,0.62) | (0.5,0.5) | (0,0) | (520,72) | center (640,446) | core | Transient (see Feedback) |
| **Winner overlay** | Overlay | (0,0) | (1,1) | (0.5,0.5) | (0,0) | stretch | full screen | core | Semi-transparent so table stays visible behind |

**No-overlap check (bottom band):** player hand ends by ~x450 at 8 cards, inspect box starts x468, log starts x964 — all clear. Action bar (y250–302) and book buttons (y248–288) sit in the table band, above the bottom-band HUD. Equipment window (table-center) sits below the scoreboard and above heroes.

### Components

**Buttons** (raised, tactile):
- Rounding 8px. Fill = vertical gradient, lighter top → darker bottom (convex feel). Neutral/primary button: `#273458` → `#1F2A4D`. Draw/phase (primary intent): blue `#1B4A87` → `#0F3460`. **Attack** (high-stakes): red `#F25873` → `#E94560`. End Turn: neutral raised.
- Layered depth: soft ambient drop shadow (`#0A0D1A` ~45% alpha, 6px blur, +3y) for presence **plus** a 3px darker bottom-edge lip for thickness.
- States: hover → surface lifts to hover tone + shadow grows slightly; press → whole button drops 2–3px and bottom lip collapses (the "thud"); disabled → desaturate + 40% opacity.
- Book-change buttons: 52×40 each, icon + short label, selected state = filled in that book's color (Spell purple / Equip amber / Item gold) with an inset pressed look; unselected = neutral raised.

**Cards (player hand):** 112×156, rounding 10px, `#1F2A4D` body, **class/type-color rim** (spell amber, item blue, equip purple, active/used green — matching current type coding), no internal dividers (separate art / name / cost by spacing and a subtle background shift). Overlap so only ~46px of each card shows (≈66px overlap), flat row (no fan), left-aligned, grows rightward. Soft ambient shadow beneath the whole hand for a "stack on table" read. Opponent cards use the same body but shown as backs/compact (84×120).

**Inspect box & Game log:** inset appearance (`#0F1428`, lighter-inset feel vs the felt), rounding 12px, no borders. Inspect: name (Display M), cost line `M:/S:` in amber (Label), effect text (Body) with generous line spacing. Log: scrollable, muted secondary text, newest at bottom, subtle alpha fade at the top edge.

**Scoreboard (380×60, `#0F1428` inset, rounding 12px, no dividers):** three items separated by spacing —
- Left (~120px): `TURN {n}` — Label secondary + Display value.
- Center (~150px): whose-turn — Display M, color-coded: `YOUR TURN` green `#2ECC71`, `OPPONENT'S TURN` secondary `#9AA3C2`.
- Right (~90px): timer `M:SS` — Display, tabular numerals, with a small clock glyph. Color shifts with time (see Feedback).

**Stat bars (per hero, 100×28 block, floats above the sprite):** three stacked slim bars, each 100×7, 3px vertical gap. Trough `#333333`, rounding 3px. Fills — **HP green `#2ECC71`, Mana blue `#2980B9`, Stamina amber `#F5A623`** (fill width driven by value %). Deliberately small; no numeric text (exact values live in the hover popup).

**Hero hover popup (190×150, `#0F1428`, rounding 10px):** soft shadow, top border strip in the hero's **class color**. Lines (Body/Label): `HP x / y`, `Mana x / y`, `Stamina x / y`, then 1–2 key attributes. Opens offset away from screen edge/center so it never occludes the sprite (up-right for local, up-left for opponent).

**Equipment window (380×200, `#0F1428`, rounding 12px):** 6 equip slots in a 2-col × 3-row grid (~56px slots) on one side, compact stats readout on the other; overlays the table semi-transparently so the felt stays visible behind. No borders — slots are inset wells.

**Deck placeholders:** rounded-rect (8px) card-stack bodies in their deck color at ~35% toward the felt tone (so empty/placeholder reads as "a deck slot"), thin bottom-edge lip for stack thickness, slight incline per the rotation values above. Item deck upright and gold.

**Overlays:** winner overlay = `#0A0D1A` at ~85% alpha (table faintly visible), winner text Display L, Restart (green) / Exit (red) buttons. Turn banner = pill, colored by side.

---

## Game Feedback

**Genre profile:** **Tactical / Strategy** (turn-based card duel) as the base — clarity, crisp confirmation, eased transitions — with **light High-Energy accents** reserved for the few combat/critical moments (hero attack, death, victory). Tone is a tabletop duel: tactile and satisfying, **readable and not noisy**. Restraint is the default; the full channel stack appears only on death/victory.

### Interaction map

| Interaction | Tier | Importance | Camera | Time | Transform | Visual | Audio | Input | Rationale |
|---|---|---|---|---|---|---|---|---|---|
| Card hover | core | Light | — | — | Lift **+28px**, scale **1.08**, raise to front; 0.12s ease-out with 10–15% overshoot then settle | Rim brightens to type color; soft shadow grows | Paper-slide, pitch ±12% | — | Signals which card is focused; makes the hand feel physical |
| Card exit hover | core | Minor | — | — | Return to rest, 0.10s ease-in | Rim restores | — | — | Clean release |
| Play / use card | core | Medium | — | — | Card detaches, moves to table/target 0.25s ease-in-out, scales to table size | 1–2 frame white→class-color flash on land; small felt ripple at landing point | "thwip" launch + "tap" land, pitch ±10% | — | Confirms the action registered and where it resolved |
| Discard (right-click) | optional | Light | — | — | Card slides to the discard deck 0.2s ease-in, fade | Faint puff at deck | Soft "flick" | — | Confirms removal; ties card to its deck |
| Hero attack | core | Heavy | Directional nudge **3px**, exp. decay (**heavy hits only**) | Hitstop **0.03s** at contact | Attacker plays *attacking* anim; target squash **12%** + recoil **8px** away then settle | White hit-flash on target (2 frames); small impact spark | Impact thud, pitch ±10% | — | Sells the strike; the one moment that earns the full-ish stack |
| Damage taken (bar) | core | Medium | — | — | Bar bg quick shake ~2px | HP fill drains 0.3s ease-out; bg flash **red**; `-N` number pops | Damage tick | — | Makes stat loss legible and weighty |
| Heal / gain | core | Light | — | — | — | Fill grows 0.3s ease-out; **green** flash; `+N` number pops | Soft chime | — | Positive confirmation, distinct from damage |
| Mana / stamina spend | core | Minor | — | — | — | Fill drop + brief dim pulse on that bar | Soft click | — | Shows resource cost of an action |
| Turn change | core | Medium | — | — | Banner slides in from side, holds 0.8s, slides out | Scoreboard whose-turn label crossfades color; active hero gains soft class-color rim glow, inactive hero dims 15% | Distinct chime per side | — | Unambiguous "it's my/their turn" |
| Timer running low | core | Light→Medium | — | — | ≤5s: timer pulses scale 1.0↔1.12 each second | Timer color → amber ≤10s, red `#E94560` ≤5s | Soft tick last 5s (optional) | — | Escalating urgency without a loud alarm |
| Deck draw | core | Light | — | — | Deck press scale 0.96 (0.06s); top card slides out to hand 0.3s ease-out; hand re-flows | Brief highlight border in deck color | Card-draw slide, pitch ±12% | — | Ties the drawn card to its source deck |
| Deck hover | optional | Minor | — | — | Lift +4px | Highlight border in deck color | — | — | Affordance that a deck is interactive |
| Equipment open/close | core | Light | — | — | Scale 0.9→1.0 (open, 0.15s ease-out, slight overshoot) / reverse (close, 0.12s) | Table dims faintly behind | Soft "open"/"close" | — | Confirms the modal-on-table without hiding the game |
| Hero hover popup | core | Minor | — | — | Fade + rise 6px, 0.12s | — | — | — | Non-intrusive detail on demand |
| Hero death | core | Critical | Nudge 4px (single) | Brief 0.04s hitstop | Slump via *death* anim | Desaturate to grey over 0.4s; stat bars fade out | Low audio sting | — | The consequence must land hard but brief |
| Victory | core | Critical | — | — | Winner plays *victory* anim | Winner overlay fades in 0.4s (table stays faintly visible) | Celebratory sting | — | Payoff moment |

### Animation-state triggers (hero sprites)

- **Idle:** default resting state; the *active* hero's idle carries a subtle class-color rim glow, the inactive hero's idle is dimmed 15%.
- **Attacking:** fired at the moment an attack action resolves against a target (the contact frame drives the hit-flash/hitstop/recoil above); returns to Idle on completion.
- **Death:** fired when a hero's HP reaches 0; holds on the final slumped pose (do not loop back to Idle).
- **Victory:** fired on the winning hero the moment the match is decided, in sync with the winner overlay; the losing hero holds Death (or Idle if still alive by timeout).

### Sequences

- **Play card:** hover-lift (0ms) → detach + move to table, ease-in-out (0→250ms) → land: white→class-color flash + felt ripple + "tap" (250ms) → inspect box + log update (300ms).
- **Hero attack + damage:** attacking anim start (0ms) → contact: hitstop 0.03s + target white-flash + 8px recoil + impact thud (≈180ms) → HP bar drains ease-out + bg red flash + `-N` number pop (rise 20px, scale 0→1.2→1 over 0.25s, fade by 600ms) (220ms) → attacker returns to Idle (400ms).
- **Turn change:** banner slides in (0ms) → scoreboard label crossfade + active-hero rim glow up / inactive dim (0–150ms) → banner holds (150–950ms) → banner slides out (950–1150ms).

### Feedback design rules applied

- Shake decays exponentially, never linearly; combat camera nudge is tiny (3–4px) and heavy-hits-only — routine turns get **no** camera.
- Freeze frames stay 0.02–0.05s.
- Squash/stretch conserves volume; overshoot peaks ~30% into the move then settles (hover 10–15%).
- Pitch randomize ±10–15% on any sound that can repeat within a second (card slides, ticks, hits).
- Every effect earns its place: if removing it loses information (which card is focused, that HP changed, whose turn it is), it stays; otherwise it's cut or made optional.

### Assets referenced (dependencies for the feedback above)

Per-hero sprite states — **Idle, Attacking, Death (core)**; **Victory (optional — may reuse a celebratory Idle if a unique clip isn't available)**. Supporting placeholders (core): table surface, deck-stack + card-back graphics, stat-bar fill/trough, button 9-slice, damage/heal number style. Optional polish: hit-flash/spark and felt-ripple effects, rim-glow for the active hero. Match all new art to the existing dark palette and class colors defined above; keep silhouettes clear at the ~150px on-table hero scale.
