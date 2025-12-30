# Rune Relic: Complete Asset Requirements

**Version:** 1.0
**Created:** December 2024
**Purpose:** Comprehensive list of 3D assets, 2D sprites, VFX, and audio needed for full game implementation

---

## Current Asset Inventory (complete-uiux-kit)

### Already Have:
| Category | Assets | Status |
|----------|--------|--------|
| **Backgrounds** | arena_bg, defeat_bg, menu_bg, victory_bg | Ready |
| **Bars** | exp_bar, health_bar, mana_bar, progress_bar, timer_bar | Ready |
| **Buttons** | back, cancel, close, confirm, forward, info, menu, play_large, play_small, settings, upgrade | Ready |
| **Decorative** | border_chain, corner_ornament, divider_horizontal, divider_vertical | Ready |
| **Effects** | energy_burst, glow_amber, glow_blue, sparkles_gold | Ready |
| **Frames** | card_frame, dialog_box, footer_bar, header_bar, inventory_slot, main_panel, popup_window, sidebar, skill_slot, tooltip | Ready |
| **Icons** | gem_emerald, gem_ruby, gem_sapphire, gold_coins, health_heart, key, lock, magic_spell, mana_crystal, potion_health, potion_mana, scroll, shield_defense, star, sword_attack, trophy, unlock | Ready |
| **Rune Cubes** | arcane, earth, fire, holy, ice, lightning, void | Ready |
| **Textures** | leather, metal_bronze, stone_dark, stone_light, wood_dark | Ready |

---

## PHASE 1: Core 3D Game Assets

### 1.1 Player Forms (5 Models + Animations)

Each form needs a distinct 3D model with evolution visual progression:

| Form | Description | Size | Color | Style Notes |
|------|-------------|------|-------|-------------|
| **Spark** | Small wisp/spirit orb | 0.5 radius | Light Blue | Ethereal, simple, glowing core |
| **Glyph** | Floating rune symbol | 0.7 radius | Green | Geometric, runic patterns emerge |
| **Ward** | Protected spirit entity | 1.0 radius | Gold | Shield-like outer layer, ornate |
| **Arcane** | Powerful magical being | 1.4 radius | Purple | Complex, multiple floating parts |
| **Ancient** | Ultimate evolved form | 2.0 radius | Orange/Fire | Majestic, ancient runes orbit |

**Animations Needed Per Form:**
- Idle (floating, breathing)
- Move (glide/fly direction)
- Collect rune (absorb effect)
- Take damage (flash/recoil)
- Evolution transition (morph to next form)
- Death/Elimination (dissolve/shatter)
- Victory pose
- Ability activation

**VFX Per Form:**
- Ambient glow/aura
- Trail effect (matches form color)
- Form-specific particle system

---

### 1.2 Rune Collectibles (6 Types)

3D crystal/gem objects that float and rotate:

| Rune Type | Color | Points | Visual Style |
|-----------|-------|--------|--------------|
| **Wisdom** | Blue | 10 | Sapphire crystal, book/scroll glyph |
| **Power** | Red | 15 | Ruby crystal, sword/flame glyph |
| **Speed** | Yellow | 12 | Amber crystal, lightning bolt glyph |
| **Shield** | Green | 8 | Emerald crystal, shield glyph |
| **Arcane** | Purple | 25 | Amethyst crystal, arcane circle glyph |
| **Chaos** | Pink | 50 | Multi-colored crystal, chaotic pattern |

**Per Rune:**
- 3D model (crystal capsule shape)
- Rotation animation
- Float bob animation
- Collection burst VFX
- Glow/emission material
- Spawn appear VFX

---

### 1.3 Shrines (4 Types)

Capturable power stations with distinct visual themes:

| Shrine Type | Color | Buff Effect | Visual Style |
|-------------|-------|-------------|--------------|
| **Wisdom** | Blue | +25% XP | Stone altar with floating tome |
| **Power** | Red | +20% damage | Volcanic brazier, flames |
| **Speed** | Yellow | +15% speed | Wind-swept obelisk, lightning |
| **Shield** | Green | Shield absorbs 50 dmg | Nature altar, vines, leaves |

**Per Shrine:**
- Base platform (circular, 2.5 unit radius)
- Central structure (capsule/pillar)
- Orbiting particles (6-8 orbs)
- Active state (glowing, particles moving)
- Inactive state (dim, particles slow)
- Capturing state (beam to player)
- Captured burst VFX
- Cooldown visual (timer ring)

---

### 1.4 Arena Environment (Modular Kit)

#### Floor Tiles (1x1 unit grid-aligned)
| Asset | Variants | Notes |
|-------|----------|-------|
| **Stone Floor** | 4 | Intact, cracked, mossy, runed |
| **Platform Top** | 3 | Wood, stone, metal |
| **Edge Tiles** | 8 | N, S, E, W, NE, NW, SE, SW |
| **Center Piece** | 2 | Ritual circle, focal point |
| **Hazard Tile** | 3 | Lava, void, unstable |

#### Walls & Barriers (2m segments)
| Asset | Variants | Notes |
|-------|----------|-------|
| **Low Wall** | 4 | Intact, broken, with torch, with banner |
| **Pillar** | 3 | Short (2m), medium (4m), tall (8m) |
| **Gate/Arch** | 2 | Stone arch, runed portal |
| **Barrier** | 2 | Energy wall, crystal wall |

#### Platforms (Various Heights)
| Asset | Size | Height | Notes |
|-------|------|--------|-------|
| **Small Platform** | 3x3 | 2m | Floating stone |
| **Medium Platform** | 5x5 | 4m | With ramp access |
| **Large Platform** | 8x8 | 6m | Multiple access points |
| **Tower Base** | 6x6 | 10m | Central tower structure |
| **Floating Island** | Organic | 8m | Irregular shape, vegetation |

#### Decorative Props
| Asset | Variants | Notes |
|-------|----------|-------|
| **Crystals** | 6 | Match rune colors, various sizes |
| **Torches/Braziers** | 3 | Wall, standing, hanging |
| **Vegetation** | 5 | Mushrooms, ferns, vines, moss, flowers |
| **Debris** | 4 | Rocks, rubble, bones, artifacts |
| **Statues** | 3 | Guardian, ancient being, broken |
| **Banners** | 4 | Different factions/colors |
| **Rune Stones** | 6 | Standing stones with glyphs |

#### Hazards
| Asset | Animation | Notes |
|-------|-----------|-------|
| **Lava Pool** | Bubble, glow pulse | Damage on contact |
| **Void Pit** | Swirl, dark particles | Instant elimination |
| **Spike Trap** | Extend/retract | Timed damage |
| **Falling Rocks** | Fall from sky | Periodic hazard |
| **Energy Field** | Pulse, crackle | Slows movement |

---

## PHASE 2: Map Configurations

### 2.1 Arena Types (Per Game Design)

| Arena Name | Size | Theme | Key Features |
|------------|------|-------|--------------|
| **Ancient Courtyard** | 50x50 | Stone ruins | Central obelisk, 4 corner shrines |
| **Glass Bridge** | 30x60 | Sky platforms | Narrow bridges, falling hazard |
| **Lava Sanctum** | 45x45 | Volcanic | Lava cracks, erupting geysers |
| **Arcane Library** | 40x40 | Indoor ruins | Bookshelf cover, dim lighting |
| **Crystal Cavern** | 55x55 | Underground | Crystal formations, glowing pools |
| **Sky Temple** | 50x50 | Floating | Multiple elevation, wind gusts |

### 2.2 Arena Shrink Mechanics (Ritual Rush)

Visual assets for arena boundary closing:
- Encroaching void/mist wall
- Warning indicators (ground glow)
- Safe zone boundary markers
- Shrink animation (gradual)

---

## PHASE 3: VFX & Particles

### 3.1 Player VFX
| Effect | Trigger | Style |
|--------|---------|-------|
| **Trail** | Movement | Form-colored ribbon |
| **Evolution Burst** | Form change | Spiral particles, screen flash |
| **Damage Flash** | Hit | Red screen edges, character flash |
| **Heal/Shield** | Buff applied | Green/blue pulse |
| **Speed Boost** | Speed buff | Yellow wind lines |
| **Elimination** | Death | Shatter into particles |
| **Spawn In** | Match start | Materialize from light |

### 3.2 Rune VFX
| Effect | Trigger | Style |
|--------|---------|-------|
| **Idle Glow** | Always | Pulsing aura |
| **Collection Burst** | Collected | 12 particles outward |
| **Spawn Appear** | Rune spawns | Fade in with sparkle |

### 3.3 Shrine VFX
| Effect | Trigger | Style |
|--------|---------|-------|
| **Active Aura** | Shrine ready | Orbiting particles |
| **Capture Beam** | Player capturing | Beam to player |
| **Capture Burst** | Captured | Expanding ring |
| **Cooldown Spin** | On cooldown | Timer ring |
| **Reactivation** | Cooldown done | Flash and particles |

### 3.4 Environment VFX
| Effect | Location | Style |
|--------|----------|-------|
| **Torch Fire** | Braziers | Animated flames |
| **Crystal Glow** | Decorative crystals | Pulse emission |
| **Lava Bubbles** | Lava hazards | Rising bubbles, glow |
| **Arena Shrink** | Boundary | Dark mist, red glow |
| **Rune Spawn Point** | Ground | Circle glyph appears |

---

## PHASE 4: UI Sprites (Need to Create)

### 4.1 Form Evolution Icons (5)
Visual icons for HUD showing current/next form:
- Spark icon (blue wisp)
- Glyph icon (green rune)
- Ward icon (gold shield)
- Arcane icon (purple magic)
- Ancient icon (orange fire)

### 4.2 Rune Type Icons (6)
For collection tracking and HUD:
- Wisdom icon (blue book)
- Power icon (red sword)
- Speed icon (yellow bolt)
- Shield icon (green shield)
- Arcane icon (purple circle)
- Chaos icon (pink chaos)

### 4.3 Buff/Status Icons
- Speed buff active
- Shield buff active
- Wisdom buff active
- Power buff active
- Combo counter (x2, x3, x5, x10)
- Capturing indicator

### 4.4 Ranked Tier Badges (7)
- Bronze badge
- Silver badge
- Gold badge
- Platinum badge
- Diamond badge
- Master badge
- Grandmaster badge

### 4.5 Currency Icons (Already Partial)
- DUST icon (soft currency)
- SIGIL icon (skill currency)
- SAGE icon (premium/verified)

### 4.6 Battle Pass Elements
- Tier number frames
- Lock/unlock states
- Premium track indicator
- Free track indicator
- Reward preview frames

---

## PHASE 5: Audio Assets

### 5.1 Music Tracks
| Track | Usage | Style |
|-------|-------|-------|
| **Main Menu** | Lobby | Epic, mysterious, orchestral |
| **Matchmaking** | Queue | Tense, building anticipation |
| **Arena (Calm)** | Early match | Atmospheric, mystical |
| **Arena (Intense)** | Late match/shrink | Fast-paced, urgent |
| **Victory** | Win screen | Triumphant, celebratory |
| **Defeat** | Lose screen | Somber but not depressing |

### 5.2 Sound Effects
| Category | Sounds Needed |
|----------|---------------|
| **Player** | Move, jump, land, damage, death, evolve |
| **Runes** | Collect (per type), spawn |
| **Shrines** | Capture start, progress tick, capture complete, reactivate |
| **UI** | Button click, menu open/close, notification, tier up |
| **Combo** | x2, x3, x5, x10 (escalating) |
| **Environment** | Ambient, hazard warning, shrink warning |
| **Match** | Countdown (3, 2, 1, GO!), time warning, match end |

---

## PHASE 6: Cosmetic Assets (Future)

### 6.1 Rune Trails (50 total per spec)
Categories:
- Element trails (fire, ice, lightning, etc.)
- Seasonal trails (snow, leaves, flowers)
- Ranked trails (bronze through GM)
- Mythic trails (animated, multi-color)

### 6.2 Aura Rings (40 total per spec)
Visual rings around player:
- Simple glows
- Particle orbits
- Animated patterns
- Seasonal themes

### 6.3 Victory Poses (30 total per spec)
Post-match character animations:
- Celebration dances
- Power displays
- Themed poses

### 6.4 Emotes (40 total per spec)
In-match expressions:
- Happy/sad faces
- Taunts
- GG/thumbs up
- Seasonal

### 6.5 Shrine Effects (20 total per spec)
Custom capture visuals:
- Different beam styles
- Unique auras
- Themed particles

### 6.6 Profile Frames (25 total per spec)
Profile border decorations:
- Ranked frames
- Achievement frames
- Seasonal frames

---

## Asset Priority Matrix

### Immediate (MVP)
1. Player forms (5 models with basic animations)
2. Rune models (6 types)
3. Shrine models (4 types)
4. Basic arena floor/wall kit (10 pieces)
5. Core VFX (trails, collection, evolution)
6. Essential UI icons

### Short-Term (Beta)
1. Full arena modular kit (30+ pieces)
2. 3 complete arena layouts
3. All hazard types
4. Full VFX suite
5. Music tracks
6. Sound effects library

### Medium-Term (Launch)
1. 6 arena variants
2. Cosmetic trails (10 varieties)
3. Aura rings (10 varieties)
4. Victory poses (5 varieties)
5. Ranked badges
6. Battle Pass UI elements

### Long-Term (Post-Launch)
1. Full cosmetic catalog (235 items per spec)
2. Seasonal content
3. Event-specific assets
4. Additional arenas
5. New game mode assets

---

## Technical Specifications

### 3D Assets
- **Polycount**: Low-poly stylized (500-2000 tris for characters, 100-500 for props)
- **Texture Resolution**: 512x512 for props, 1024x1024 for characters
- **Format**: FBX/GLTF for Bevy compatibility
- **Materials**: PBR with emission support for glowing effects

### 2D Assets
- **Resolution**: 128x128 for icons, 512x512 for larger UI
- **Format**: PNG with transparency
- **Style**: Consistent with "stylized stone magic" theme

### Audio
- **Format**: OGG for music, WAV for SFX
- **Sample Rate**: 44.1kHz
- **Music**: Loop-ready, 2-3 minute tracks

---

## Next Steps

1. **Review existing rune cube assets** - These could be adapted for in-game rune models
2. **Create player form concepts** - Design progression from Spark to Ancient
3. **Build modular arena kit** - Start with basic floor/wall pieces
4. **Prototype VFX** - Trail and collection effects in Bevy
5. **Source audio** - Find royalty-free or commission original music/SFX

---

*Document maintained by: Game Development Team*
*Last updated: December 2024*
