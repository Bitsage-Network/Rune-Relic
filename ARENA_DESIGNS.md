# Rune Relic: Arena Designs

**Version:** 1.0
**Created:** December 2024
**Based on:** Game Design Document v1.0

---

## Arena Design Philosophy

### Core Principles
1. **Clarity** - Players always know where they are and where threats come from
2. **Flow** - Natural movement paths encourage engagement, not camping
3. **Risk/Reward** - Valuable runes placed in dangerous locations
4. **Fairness** - Symmetrical spawn positions, equal access to shrines
5. **Excitement** - Shrinking boundary creates forced encounters

### Universal Constants
- **Grid Unit**: 1 unit = 1 meter (player Spark form = 0.5m radius)
- **Platform Heights**: Ground (0m), Low (3m), Medium (6m), High (10m), Tower (14m)
- **Shrine Range**: 3m capture radius
- **Rune Respawn**: 15 seconds after collection

---

## ARENA 1: Ritual Grounds (Ritual Rush - Main Arena)

**Mode:** 12-Player Survival (90 seconds)
**Size:** 100 x 100 units (shrinks to 30 x 30)
**Theme:** Ancient stone ritual circle with floating platforms

### Layout Overview
```
                         N
              ╔═══════════════════════╗
              ║    [Void Edge]        ║
              ║  ┌─────────────────┐  ║
              ║  │ P    [S1]    P  │  ║
              ║  │  ╭───╮   ╭───╮  │  ║
              ║  │  │ R │   │ R │  │  ║
         W    ║  │P ╰───╯   ╰───╯ P│  ║    E
              ║  │    ╭─────╮      │  ║
              ║[S4]   │TOWER│   [S2]║
              ║  │    │ [C] │      │  ║
              ║  │    ╰─────╯      │  ║
              ║  │P ╭───╮   ╭───╮ P│  ║
              ║  │  │ R │   │ R │  │  ║
              ║  │  ╰───╯   ╰───╯  │  ║
              ║  │ P    [S3]    P  │  ║
              ║  └─────────────────┘  ║
              ║    [Void Edge]        ║
              ╚═══════════════════════╝
                         S

Legend:
P = Player Spawn (12 total, evenly distributed)
S1-S4 = Shrines (Wisdom-N, Power-E, Speed-S, Shield-W)
R = Raised Platform with runes
C = Central Tower (Chaos rune at top)
[Void Edge] = Arena boundary (shrinks inward)
```

### Detailed Layout

#### Ground Level (Y = 0)
```
100 ┌────────────────────────────────────────────────────────────────────────────────────────────────────┐
    │ ○    ○    ○                                                                          ○    ○    ○ │
    │      SPAWN RING (12 positions at radius 45)                                                      │
 90 │                                                                                                  │
    │         ┌──────────────────────────────────────────────────────────────────────┐                │
    │         │                                                                        │                │
    │         │    ●W      ●W                              ●W      ●W                 │                │
 80 │         │                         ╔══════╗                                      │                │
    │         │                         ║SHRINE║ WISDOM                               │                │
    │         │                         ║ (N)  ║                                      │                │
    │         │                         ╚══════╝                                      │                │
 70 │         │                                                                        │                │
    │         │    ┌─────────┐                          ┌─────────┐                   │                │
    │         │    │PLATFORM │ ●S ●P                    │PLATFORM │ ●A ●S             │                │
    │         │    │  (NW)   │                          │  (NE)   │                   │                │
 60 │         │    └─────────┘                          └─────────┘                   │                │
    │         │                                                                        │                │
    │ ╔══════╗│                     ┌─────────────────────┐                           │╔══════╗        │
    │ ║SHRINE║│                     │                     │                           │║SHRINE║        │
 50 │ ║SHIELD║│                     │   CENTRAL TOWER     │                           │║POWER ║        │
    │ ║ (W)  ║│                     │      ★ CHAOS        │                           │║ (E)  ║        │
    │ ╚══════╝│                     │                     │                           │╚══════╝        │
    │         │                     └─────────────────────┘                           │                │
 40 │         │                                                                        │                │
    │         │    ┌─────────┐                          ┌─────────┐                   │                │
    │         │    │PLATFORM │ ●P ●W                    │PLATFORM │ ●W ●A             │                │
    │         │    │  (SW)   │                          │  (SE)   │                   │                │
 30 │         │    └─────────┘                          └─────────┘                   │                │
    │         │                                                                        │                │
    │         │                         ╔══════╗                                      │                │
    │         │                         ║SHRINE║ SPEED                                │                │
 20 │         │                         ║ (S)  ║                                      │                │
    │         │                         ╚══════╝                                      │                │
    │         │    ●S      ●P                              ●S      ●P                 │                │
    │         │                                                                        │                │
 10 │         └──────────────────────────────────────────────────────────────────────┘                │
    │                                                                                                  │
    │ ○    ○    ○                                                                          ○    ○    ○ │
  0 └────────────────────────────────────────────────────────────────────────────────────────────────────┘
    0        10        20        30        40        50        60        70        80        90       100

● = Rune Spawn Points (W=Wisdom, P=Power, S=Speed, H=Shield, A=Arcane, ★=Chaos)
○ = Player Spawn Points (12 total)
```

### Vertical Layout (Side View)
```
Height
  14m │                    ╔═══╗
      │                    ║ ★ ║ ← Chaos Rune (Tower Top)
  12m │                ┌───╨───╨───┐
      │                │   TOWER   │
  10m │                │   TOP     │ ← Arcane Runes
      │            ┌───┤           ├───┐
   8m │            │   │           │   │
      │        ┌───┤   │           │   ├───┐
   6m │    ┌───┤   │   │           │   │   ├───┐ ← Medium Platforms
      │    │   │   │   │           │   │   │   │
   4m │    │   │   │   │           │   │   │   │
      │┌───┤   │   │   │           │   │   │   ├───┐ ← Low Platforms
   3m ││   │   │   │   │           │   │   │   │   │
      ││   │   │   │   │           │   │   │   │   │
   0m │╧═══╧═══╧═══╧═══╧═══════════╧═══╧═══╧═══╧═══╧ ← Ground
      └─────────────────────────────────────────────────
             NW    SW    TOWER     SE    NE
```

### Spawn Points (12 Players)
```
Position | Coordinates (X, Z) | Facing
---------|-------------------|--------
Spawn 1  | (50, 95)          | South
Spawn 2  | (65, 92)          | South-West
Spawn 3  | (80, 85)          | West
Spawn 4  | (92, 70)          | West
Spawn 5  | (95, 50)          | West
Spawn 6  | (92, 30)          | North-West
Spawn 7  | (80, 15)          | North
Spawn 8  | (65, 8)           | North-East
Spawn 9  | (50, 5)           | North
Spawn 10 | (35, 8)           | North-East
Spawn 11 | (20, 15)          | East
Spawn 12 | (8, 30)           | East
```

### Shrine Positions
| Shrine | Type | Position (X, Y, Z) | Buff |
|--------|------|-------------------|------|
| North | Wisdom | (50, 0, 75) | +25% XP 30s |
| East | Power | (75, 0, 50) | +20% damage 30s |
| South | Speed | (50, 0, 25) | +15% speed 30s |
| West | Shield | (25, 0, 50) | Absorb 50 dmg |

### Rune Distribution
| Location | Runes | Points Total | Risk Level |
|----------|-------|--------------|------------|
| Ground (outer) | 8 Wisdom (10), 4 Shield (8) | 112 | Low |
| Low Platforms | 4 Speed (12), 4 Power (15) | 108 | Medium |
| Medium Platforms | 4 Arcane (25) | 100 | High |
| Tower Top | 1 Chaos (50) | 50 | Extreme |
| **TOTAL** | 25 runes | 370 pts | - |

### Arena Shrink Phases
| Phase | Time | Radius | Safe Zone | Speed |
|-------|------|--------|-----------|-------|
| Start | 0-30s | 50m | Full arena | - |
| Phase 1 | 30-45s | 50→40m | 80x80 area | 0.67 m/s |
| Phase 2 | 45-60s | 40→30m | 60x60 area | 0.67 m/s |
| Phase 3 | 60-75s | 30→20m | 40x40 area | 0.67 m/s |
| Final | 75-90s | 20→10m | 20x20 area | 0.67 m/s |

### Hazard Timing
- **T+0s**: Match start, all runes spawn
- **T+15s**: Warning - "Arena will shrink in 15 seconds"
- **T+30s**: Shrink begins, void wall advances
- **T+45s**: "50% of arena remaining"
- **T+60s**: Final warning - "Arena shrinking rapidly"
- **T+75s**: Chaos zone - very small safe area
- **T+90s**: Match end

---

## ARENA 2: The Proving Grounds (Codex Duels - 1v1)

**Mode:** 1v1 Duel (No time limit, elimination)
**Size:** 40 x 40 units (no shrink)
**Theme:** Symmetrical battle pit with strategic cover

### Layout Overview
```
              ╔═══════════════════════════════════╗
              ║            PLAYER 2               ║
              ║               ○                   ║
              ║         ┌───────────┐             ║
              ║         │ PLATFORM  │ ●A          ║
              ║    ●W   └───────────┘   ●W        ║
              ║                                   ║
              ║  ╔════╗               ╔════╗      ║
              ║  ║ S1 ║     ●C        ║ S2 ║      ║
              ║  ╚════╝               ╚════╝      ║
              ║         CENTER                    ║
              ║         ●CHAOS●                   ║
              ║  ╔════╗               ╔════╗      ║
              ║  ║ S3 ║     ●C        ║ S4 ║      ║
              ║  ╚════╝               ╚════╝      ║
              ║                                   ║
              ║    ●P   ┌───────────┐   ●P        ║
              ║         │ PLATFORM  │ ●A          ║
              ║         └───────────┘             ║
              ║               ○                   ║
              ║            PLAYER 1               ║
              ╚═══════════════════════════════════╝

Legend:
○ = Player Spawn
S1-S4 = Mini-Shrines (2 each type)
●W/P/A/C = Rune spawns
```

### Detailed 1v1 Layout
```
40 ┌────────────────────────────────────────┐
   │                  ○ P2                  │
   │              (20, 38)                  │
35 │         ╔══════════════════╗           │
   │         ║    PLATFORM 2    ║ ●A        │
   │   ●W    ║     (6m high)    ║   ●W      │
   │  (8,32) ╚══════════════════╝  (32,32)  │
30 │                                        │
   │   ╔════╗                   ╔════╗      │
   │   ║ W  ║                   ║ P  ║      │
   │   ║SHRI║                   ║SHRI║      │
25 │   ╚════╝                   ╚════╝      │
   │  (5,25)       ╔═════╗     (35,25)      │
   │               ║     ║                  │
   │               ║ ★   ║ ← CHAOS          │
20 │               ║CHAOS║   (center)       │
   │               ║     ║                  │
   │               ╚═════╝                  │
   │   ╔════╗                   ╔════╗      │
15 │   ║ S  ║                   ║ SH ║      │
   │   ║SHRI║                   ║SHRI║      │
   │   ╚════╝                   ╚════╝      │
   │  (5,15)                   (35,15)      │
10 │                                        │
   │   ●P    ╔══════════════════╗   ●P      │
   │  (8,8)  ║    PLATFORM 1    ║  (32,8)   │
   │         ║     (6m high)    ║ ●A        │
 5 │         ╚══════════════════╝           │
   │                                        │
   │                  ○ P1                  │
   │              (20, 2)                   │
 0 └────────────────────────────────────────┘
   0    5    10   15   20   25   30   35   40
```

### 1v1 Shrine Configuration
| Shrine | Type | Position | Notes |
|--------|------|----------|-------|
| S1 (NW) | Wisdom | (5, 25) | Near P2's side |
| S2 (NE) | Power | (35, 25) | Near P2's side |
| S3 (SW) | Speed | (5, 15) | Near P1's side |
| S4 (SE) | Shield | (35, 15) | Near P1's side |

### 1v1 Rune Spawns
| Rune | Type | Position | Points |
|------|------|----------|--------|
| 1 | Wisdom | (8, 32) | 10 |
| 2 | Wisdom | (32, 32) | 10 |
| 3 | Power | (8, 8) | 15 |
| 4 | Power | (32, 8) | 15 |
| 5 | Arcane | (30, 35) | 25 |
| 6 | Arcane | (30, 5) | 25 |
| 7 | **Chaos** | (20, 20) | 50 |

**Total Points Available:** 150

### Strategy Notes
- **Mirror symmetry** ensures fair starting positions
- **4 shrines** = both players can capture 2 each if aggressive
- **Chaos rune in center** = high-risk, high-reward zone
- **Elevated platforms** = defensive positions but exposed to ranged
- **No shrink** = focus on combat, not running

---

## ARENA 3: The Crucible (4-Player FFA / 2v2)

**Mode:** 4-Player Free-For-All or 2v2 Teams
**Size:** 60 x 60 units (shrinks to 30 x 30)
**Theme:** Four-corner arena with cross-shaped center

### Layout Overview
```
60 ┌──────────────────────────────────────────────────────────┐
   │  ○P1                                              ○P2    │
   │ (10,55)     ╔════╗                   ╔════╗     (50,55)  │
   │             ║ W  ║                   ║ P  ║              │
55 │    ●W●S     ╚════╝                   ╚════╝     ●P●S     │
   │              (20,55)                 (40,55)             │
   │                           │                              │
   │         ┌─────┐           │           ┌─────┐            │
50 │         │PLAT │    ●A     │    ●A     │PLAT │            │
   │         │ NW  │           │           │ NE  │            │
   │         └─────┘           │           └─────┘            │
   │                           │                              │
45 │                    ═══════╪═══════                       │
   │                           │                              │
   │                    ╔══════╧══════╗                       │
   │      ──────────────║    ★★★     ║──────────────          │
40 │                    ║   CHAOS    ║                        │
   │                    ║   ZONE     ║                        │
   │      ──────────────║            ║──────────────          │
   │                    ╚══════╤══════╝                       │
35 │                           │                              │
   │                    ═══════╪═══════                       │
   │                           │                              │
   │         ┌─────┐           │           ┌─────┐            │
30 │         │PLAT │    ●A     │    ●A     │PLAT │            │
   │         │ SW  │           │           │ SE  │            │
   │         └─────┘           │           └─────┘            │
   │                                                          │
25 │    ●S●W     ╔════╗                   ╔════╗     ●W●P     │
   │             ║ SH ║                   ║ SP ║              │
   │             ╚════╝                   ╚════╝              │
   │  ○P3                                              ○P4    │
20 │ (10,5)                                          (50,5)   │
   └──────────────────────────────────────────────────────────┘
    0    10    20    30    40    50    60
```

### 4-Player Spawn Positions
| Player | Position | Corner | Team (2v2) |
|--------|----------|--------|------------|
| P1 | (10, 55) | NW | Team A |
| P2 | (50, 55) | NE | Team B |
| P3 | (10, 5) | SW | Team A |
| P4 | (50, 5) | SE | Team B |

### 4-Player Shrines
| Shrine | Type | Position | Quadrant |
|--------|------|----------|----------|
| NW | Wisdom | (20, 55) | P1 territory |
| NE | Power | (40, 55) | P2 territory |
| SW | Shield | (20, 25) | P3 territory |
| SE | Speed | (40, 25) | P4 territory |

### Arena Shrink (4-Player)
| Phase | Time | Safe Zone |
|-------|------|-----------|
| Full | 0-20s | 60x60 |
| Phase 1 | 20-35s | 50x50 |
| Phase 2 | 35-50s | 40x40 |
| Final | 50-60s | 30x30 |

---

## ARENA 4: Sky Bridge (Elimination Mode)

**Mode:** Last Man Standing / Knockout
**Size:** 80 x 30 units (linear)
**Theme:** Narrow bridges over void

### Layout (Top View)
```
                    ═══════════════════════════════════════════════════════════
                   ║                                                           ║
    START          ║   ○○○○○○    [BRIDGE 1]    [BRIDGE 2]    [BRIDGE 3]       ║   END
    PLATFORM       ║   SPAWN     ════════════  ═══●═══════  ════════════      ║   PLATFORM
                   ║              (stable)     (breakaway)   (moving)         ║   ★CHAOS★
                   ║                                                           ║
                    ═══════════════════════════════════════════════════════════

Side View:
           ┌────┐                                                    ┌────┐
    8m     │    │         ═════════╗    ╔═════════    ═════════     │ ★  │
           │PLAT│                  ║    ║                           │GOAL│
    0m     │    │══════════════════╝    ╚══════════════════════════│    │
           └────┘                   VOID  VOID                      └────┘
              ↑                                                        ↑
           SPAWN                                                    CHAOS
```

### Bridge Hazards
1. **Bridge 1 (X: 15-30)**: Stable, safe passage
2. **Bridge 2 (X: 35-50)**: Breakaway sections (collapse after weight)
3. **Bridge 3 (X: 55-70)**: Moving platforms (oscillate left/right)

### Rune Placements
- Scattered along bridges
- Higher value runes on dangerous bridges
- Chaos rune at end platform

---

## ARENA 5: Crystal Cavern (Daily Trial - PvE)

**Mode:** Solo Challenge (Survive waves)
**Size:** 50 x 50 units (circular)
**Theme:** Underground crystal formation

### Layout
```
                    ╭──────────────────────────────────────╮
                   ╱              CRYSTAL                   ╲
                  ╱         FORMATIONS (cover)               ╲
                 │    ◊           ◊           ◊               │
                 │       ╔═════════════════╗                  │
                 │   ◊   ║   SAFE ZONE     ║   ◊              │
                 │       ║      ○          ║                  │
                 │       ║    PLAYER       ║                  │
                 │   ◊   ║    START        ║   ◊              │
                 │       ╚═════════════════╝                  │
                 │              ◊                              │
                  ╲                                           ╱
                   ╲    ENEMY SPAWN RING                     ╱
                    ╰──────────────────────────────────────╯

◊ = Crystal formations (destructible cover)
○ = Player spawn (center)
```

### Wave Configuration
| Wave | Enemies | Time | Rune Drops |
|------|---------|------|------------|
| 1 | 5 Wisps | 20s | 3 Wisdom |
| 2 | 8 Wisps + 2 Glyph | 25s | 2 Power, 2 Speed |
| 3 | 10 Glyph | 30s | 3 Arcane |
| 4 | 5 Ward + 3 Arcane | 35s | 2 Chaos |
| BOSS | 1 Ancient | 60s | Victory! |

---

## Implementation Checklist

### For Bevy Client

#### Arena 1 (Ritual Rush) Components:
- [ ] Ground plane (100x100)
- [ ] Void boundary walls (shrinking)
- [ ] 4 corner platforms (3m high)
- [ ] Central tower (14m high, 3 tiers)
- [ ] 4 shrines at cardinal positions
- [ ] 25 rune spawn points
- [ ] 12 player spawn points
- [ ] Shrink timer system
- [ ] Warning VFX

#### Arena 2 (1v1 Duel) Components:
- [ ] Ground plane (40x40)
- [ ] 2 elevated platforms (6m)
- [ ] 4 mini-shrines
- [ ] 7 rune spawn points
- [ ] 2 player spawn points
- [ ] No shrink needed

#### Arena 3 (4-Player) Components:
- [ ] Ground plane (60x60)
- [ ] Cross-shaped walkways
- [ ] 4 corner platforms
- [ ] Central chaos zone
- [ ] 4 shrines
- [ ] Shrink system (faster)

---

## Code Implementation Notes

### Arena Definition Structure (Rust)
```rust
pub struct ArenaConfig {
    pub name: String,
    pub size: Vec2,                    // Arena dimensions
    pub player_spawns: Vec<Vec3>,      // Spawn positions
    pub shrines: Vec<ShrineConfig>,    // Shrine placements
    pub runes: Vec<RuneSpawn>,         // Rune spawn points
    pub platforms: Vec<PlatformConfig>, // Platform definitions
    pub shrink_config: Option<ShrinkConfig>, // Arena shrink settings
}

pub struct ShrinkConfig {
    pub start_time: f32,      // When shrinking begins
    pub phases: Vec<ShrinkPhase>,
}

pub struct ShrinkPhase {
    pub duration: f32,        // Phase duration
    pub target_radius: f32,   // Final radius for this phase
    pub damage_per_sec: f32,  // Damage in danger zone
}
```

### Next Steps
1. Implement ArenaConfig system in Bevy
2. Create arena selection UI
3. Build Arena 1 (Ritual Rush) as playable prototype
4. Add shrink boundary system
5. Test with 12 player spawns
