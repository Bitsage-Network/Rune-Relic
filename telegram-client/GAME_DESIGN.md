# Rune Relic: The 21 Runes

## Core Concept
You are a **Rune Seeker** - a wizard who discovers, collects, and masters ancient runes. Each rune contains a bound elemental spirit with unique power.

**Goal:** Complete your RuneDex (21 runes), craft powerful variants, battle other Seekers, trade rare finds.

---

## The 21 Base Runes

### 🔥 FIRE (Aggression)
| # | Rune | Trait | Signature Move |
|---|------|-------|----------------|
| 1 | **Ember** | Quick Strike | First hit deals +20% |
| 2 | **Blaze** | Burning | Damage over 2 rounds |
| 3 | **Inferno** | Rage | Power increases when losing |

### 💧 WATER (Control)
| # | Rune | Trait | Signature Move |
|---|------|-------|----------------|
| 4 | **Droplet** | Adaptive | Copies enemy element |
| 5 | **Tide** | Flow | Swaps position with ally |
| 6 | **Tsunami** | Overwhelming | Ignores 50% defense |

### 🌍 EARTH (Defense)
| # | Rune | Trait | Signature Move |
|---|------|-------|----------------|
| 7 | **Pebble** | Sturdy | Survives one KO hit |
| 8 | **Boulder** | Heavy | Can't be swapped out |
| 9 | **Mountain** | Fortress | +50% defense, -20% speed |

### 💨 AIR (Speed)
| # | Rune | Trait | Signature Move |
|---|------|-------|----------------|
| 10 | **Breeze** | Evasive | 20% dodge chance |
| 11 | **Gust** | Swift | Always attacks first |
| 12 | **Tempest** | Chaos | Randomizes enemy order |

### ✨ LIGHT (Support)
| # | Rune | Trait | Signature Move |
|---|------|-------|----------------|
| 13 | **Spark** | Illuminate | Reveals enemy picks |
| 14 | **Radiant** | Blessed | Heals 10% after each round |
| 15 | **Solar** | Judgment | Crits vs dark-aligned |

### 🌑 VOID (Disruption)
| # | Rune | Trait | Signature Move |
|---|------|-------|----------------|
| 16 | **Shadow** | Stealth | Hidden until attack |
| 17 | **Null** | Silence | Disables enemy trait |
| 18 | **Abyss** | Drain | Steals 15% of damage dealt |

### ⚡ ARCANE (Wild Card)
| # | Rune | Trait | Signature Move |
|---|------|-------|----------------|
| 19 | **Glyph** | Inscribed | Trait changes each battle |
| 20 | **Sigil** | Sealed | Unlocks after 10 wins |
| 21 | **Relic** | Ancient | Combines two elements |

---

## Element Wheel (Rock-Paper-Scissors+)

```
       FIRE
      /    \
   AIR      EARTH
     \      /
      WATER
        |
   LIGHT ←→ VOID
        |
      ARCANE (neutral to all)
```

- Fire beats Air, Air beats Earth, Earth beats Water, Water beats Fire
- Light and Void counter each other
- Arcane is neutral (no weakness, no advantage)

**Advantage = +25% damage**

---

## Rune Stats

Each rune has 3 stats (1-100 scale):
- **Power** - Base damage
- **Guard** - Damage reduction
- **Speed** - Turn priority (ties = random)

Stats are **randomized on catch** within a range per species.
- Common: 30-50 range
- Rare: 45-65 range
- Epic: 55-80 range
- Legendary: 70-95 range

---

## Rarity System

| Rarity | Catch Rate | Stat Range | Visual |
|--------|------------|------------|--------|
| Common | 60% | 30-50 | Gray border |
| Rare | 25% | 45-65 | Blue glow |
| Epic | 12% | 55-80 | Purple glow |
| Legendary | 3% | 70-95 | Gold + particles |

**Variants** (crafted, not caught):
- Shiny: Alternate colors, same stats
- Corrupted: Void-touched, +Power -Guard
- Purified: Light-touched, +Guard -Power
- Prismatic: All elements, ultra rare

---

## Core Gameplay Loops

### 1. ENCOUNTERS (Catch Loop)
- Energy regenerates: 1 per 10 minutes, max 5
- Spend 1 energy → See 3 random rune encounters
- Pick one to attempt catch
- **Catch mini-game:** Tap when circle aligns (skill-based)
- Better timing = higher catch rate

### 2. CRAFTING LAB
**Fusion:** Combine 2 runes → new rune
- Same element = evolved form (Ember + Ember = Blaze)
- Different elements = hybrid (Fire + Water = Steam Rune?)

**Upgrade:** Use materials to:
- Reroll one stat
- Unlock second trait slot
- Change variant type

**Materials:**
- Dust (common, from any source)
- Shards (element-specific)
- Essence (from releasing runes)
- Catalyst (rare, from bosses/events)

### 3. BATTLE ARENA

**Quick Duel (30 seconds):**
1. Both players pick 3 runes (hidden)
2. Reveal round 1 picks simultaneously
3. Resolve: Element advantage → Stat comparison → Random tiebreak
4. Winner of round gets 1 point + charges Relic meter
5. Best of 3 rounds wins
6. If Relic meter full, can use ultimate ability

**Battle Rewards:**
- Winner: 50 Dust + 10 SAGE + Trophy points
- Loser: 20 Dust + 3 SAGE (participation)

**Leagues:**
- Bronze → Silver → Gold → Platinum → Diamond → Master
- Weekly reset, rewards based on peak rank

### 4. TRADING BAZAAR

**Direct Trade:**
- Send trade request to friend
- Both select runes to offer
- Confirm → swap

**Marketplace:**
- List rune with SAGE price
- 5% fee on sale
- Can search by element, rarity, stats

### 5. RUNEDEX

**Completion Rewards:**
| Milestone | Reward |
|-----------|--------|
| 7 runes | +1 Energy slot |
| 14 runes | Rare Catalyst |
| 21 runes | Legendary Relic Frame + Title |

**Per-Rune Mastery:**
- Catch 5 of same species → Mastery Level 1 (stat preview on encounters)
- Win 20 battles with it → Mastery Level 2 (catch rate bonus)
- Fuse to variant → Mastery Level 3 (exclusive frame)

---

## SAGE Economy

### Earning SAGE (capped, not infinite)
| Source | Amount | Limit |
|--------|--------|-------|
| Daily login | 10 | 1/day |
| Win battle | 10 | 20/day |
| Complete quest | 25-100 | 3/day |
| Weekly league reward | 100-500 | 1/week |
| Achievement | One-time | - |
| Sell rune | Variable | - |

### Spending SAGE (meaningful sinks)
| Use | Cost |
|-----|------|
| Stat reroll | 25 |
| Trait unlock | 50 |
| Fusion catalyst | 100 |
| Marketplace listing | 10 |
| Arena ticket (ranked) | 5 |
| Cosmetic frame | 200-500 |
| Energy refill (1) | 20 |

---

## MVP Features (Week 1)

### Must Have:
1. **RuneDex screen** - 21 slots, silhouettes for uncaught
2. **Encounter system** - 3 cards, pick one, tap-to-catch
3. **Rune collection** - View owned runes with stats
4. **Basic battle** - Pick 3, auto-resolve, see result
5. **SAGE balance** - Earn from battles/daily

### Week 2:
6. Crafting Lab (fusion only)
7. Trading (direct only)
8. League system

### Week 3:
9. Full marketplace
10. Boss events
11. Achievements

---

## UI Screens

```
[Home]
  ├── RuneDex (collection + progress)
  ├── Encounters (spend energy, catch)
  ├── Battle (PvP queue)
  ├── Lab (craft/fuse)
  └── Bazaar (trade)

[Bottom Nav]: Home | Dex | Battle | Lab | Trade
```

---

## Visual Style

- Dark mystical theme (deep purple/blue bg)
- Runes are geometric symbols with glowing edges
- Each element has distinct color palette
- Particles and glow for rarity
- Clean, card-based UI

---

## What Makes This Fun

1. **Collection drive** - "Gotta catch 'em all" for 21 runes
2. **Skill expression** - Catch timing, team building, predictions
3. **Discovery** - What fusions create what?
4. **Social** - Trading, battling friends, leagues
5. **Progression** - Stats, mastery, variants
6. **Economy** - But NOT as the core loop

SAGE is gasoline, not the destination.
