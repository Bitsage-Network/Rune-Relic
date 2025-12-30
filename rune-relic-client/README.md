# Rune Relic - Unity Client

Battle royale arena game where players collect runes, evolve through forms, and eliminate opponents.

## Requirements

- Unity 2022.3 LTS or newer
- .NET Standard 2.1

## Setup

1. Open project in Unity Hub
2. Install required packages (should auto-install from manifest.json):
   - TextMeshPro
   - Input System
   - Universal Render Pipeline

3. For WebSocket support, add NativeWebSocket via Package Manager:
   - Window → Package Manager → + → Add package from git URL
   - Enter: `https://github.com/endel/NativeWebSocket.git#upm`

## Project Structure

```
Assets/
├── Scripts/
│   ├── Network/          # WebSocket client & message types
│   │   ├── GameClient.cs
│   │   ├── WebSocketClient.cs
│   │   └── Messages/
│   ├── Game/             # Core game logic
│   │   ├── GameManager.cs
│   │   ├── MatchState.cs
│   │   ├── PlayerController.cs
│   │   ├── EntityManager.cs
│   │   └── ArenaController.cs
│   ├── Entities/         # Game entities
│   │   ├── Rune.cs
│   │   ├── Shrine.cs
│   │   └── PlayerVisuals.cs
│   ├── UI/               # User interface
│   │   ├── MainMenu.cs
│   │   ├── GameHUD.cs
│   │   ├── MatchEndUI.cs
│   │   └── SettingsMenu.cs
│   ├── Audio/            # Sound management
│   │   └── AudioManager.cs
│   └── Utils/            # Utilities
│       ├── Constants.cs
│       └── FixedPoint.cs
├── Prefabs/              # Player forms, runes, shrines
├── Scenes/               # MainMenu, Game
└── Materials/            # Visual materials
```

## Game Mechanics

### Forms (Evolution Tiers)

| Form | Radius | Speed | Score | Ability |
|------|--------|-------|-------|---------|
| Spark | 0.5 | 6.0 | 0 | Dash |
| Glyph | 0.7 | 5.5 | 100 | Phase Shift |
| Ward | 1.0 | 5.0 | 300 | Repel |
| Arcane | 1.4 | 4.5 | 600 | Gravity Well |
| Ancient | 2.0 | 4.0 | 1000 | Consume |

### Runes

- **Wisdom** (Blue): 10 points
- **Power** (Red): 15 points
- **Speed** (Yellow): 12 points + speed buff
- **Shield** (Green): 8 points + shield buff
- **Arcane** (Purple): 25 points
- **Chaos** (Rainbow): 50 points + random buff

### Shrines

Channel for 5 seconds to capture. 60 second cooldown.
- **Wisdom**: Score multiplier
- **Power**: Damage boost
- **Speed**: Movement speed
- **Shield**: Damage reduction

## Controls

- **WASD**: Movement
- **Space / Left Click**: Use ability
- **Escape**: Pause menu

## Server Connection

Default server: `ws://localhost:8080`

Run the Rust server:
```bash
cd ../rune-relic-server
cargo run
```

## Build

1. File → Build Settings
2. Select platform (Windows/Mac/WebGL)
3. Build

For WebGL, ensure WebSocket permissions are configured.
