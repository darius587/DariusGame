# Darius Game

Darius Game is a 2D pixel platformer developed in Unity 6.

The player controls a superhero named DariusMan. The main goal is to explore different levels, collect coins, defeat enemies, avoid obstacles, and reach the exit gate.

## Features

- Three playable campaign levels
- Boss fight with ShadowKing
- Enemy AI Arena with three unique enemy types
- Player movement and jumping
- Shooting system and enemy combat
- Coin collection and health system
- Main menu and stage selection
- Pause menu and settings
- Mobile joystick and buttons
- Sound effects and background music
- Victory and game-over screens
- ScriptableObject game balance system
- Data-driven level configuration
- JSON save and load system
- Automatic progress saving
- BestScore and TotalCoins tracking
- Locked and unlocked levels
- Reset Progress with confirmation
- Persistent game settings

## Game Stages

### Stage 1 — Campaign

The main campaign includes three levels and a boss fight.

#### Level 1 — Green Valley

The first level introduces basic movement and jumping.

There are 8 coins in this level. Collecting all coins is optional. The player can complete the level by reaching the exit gate.

#### Level 2 — Desert Canyon

The second level includes enemies, falling rocks, and more difficult obstacles.

There are 10 coins in this level. The player can collect any number of coins and reach the exit gate to complete the level.

#### Level 3 — Dark Castle

The third level includes moving platforms, falling bombs, and stronger enemies.

There are 12 coins in this level. Collecting every coin is not required to complete the level.

### Boss Fight — ShadowKing

ShadowKing is the main boss of Darius Game.

The player must avoid his attacks and use shooting to defeat him.

### Stage 2 — Enemy AI Arena

Enemy AI Arena is a separate stage featuring three different enemy types:

- **Patroller** — Patrols the area, chases the player, and attacks at close range.
- **Shooter** — Detects the player and attacks with projectiles.
- **Charger** — Prepares a powerful charging attack toward the player.

The player must survive enemy attacks, defeat enemies, collect three coins, and unlock the exit gate.

## Data and Save System

Darius Game uses ScriptableObjects to store player statistics, enemy statistics, item values, and level configurations.

Three campaign levels use LevelData assets.

Game progress is saved in a JSON file using `Application.persistentDataPath`.

The save system stores:

- BestScore — the highest coin score earned in one completed level.
- TotalCoins — the total coins earned across completed runs, including replays.
- Unlocked levels.
- Completed levels.

Progress is saved automatically after completing a level and when the application is paused.

The game also saves settings and restores them after restarting.

If the save file is missing or corrupted, the game loads default progress without crashing.

Players can reset their progress through a confirmation panel in the settings menu.

## Controls

### PC

- A / D or Arrow Keys — Move
- Space — Jump
- X or Left Mouse Button — Shoot
- ESC — Pause

### Mobile

- Joystick — Move
- JUMP Button — Jump
- FIRE Button — Shoot
- MENU Button — Pause

## Platforms

- Android
- Windows

## Engine

Unity 6

## Developer

Darius Studio