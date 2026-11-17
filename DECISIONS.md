# Development Decisions — Darius Game

## Week 10 — Enemy AI

- **Goal:** Implement three different enemy AI behaviors for Stage 2 — Enemy AI Arena.
- **AI Assistance:** I used ChatGPT and Codex to understand and implement finite state machines, enemy detection, raycasting, and attack cooldowns.
- **My Decisions:** I chose three enemy types: Patroller, Shooter, and Charger. I also decided to create a separate arena without changing the main campaign.
- **Problems and Solutions:** Enemy attacks needed to be predictable, so I added warning phases before attacks. Raycasting was used to prevent detection through walls.
- **Result:** I implemented three enemy behaviors, a stage selection menu, arena background music, victory and defeat sounds.

## Week 10 — AI Tuning

- **Shooter:** Increased attack wind-up from 0.6 to 0.8 seconds to give the player more time to react.
- **Charger:** Reduced charge speed from 8 to 6 to make charging attacks easier to avoid.
- **Testing:** I tested both changes in Unity and confirmed that Shooter's attack became easier to anticipate and Charger's attack became slower.
- **Decision:** I kept both changes to improve gameplay balance and fairness.

### AI Tuning Results

| Enemy | Parameter | Before | After |
|---|---|---|---|
| Shooter | Wind Up Seconds | 0.6 s | 0.8 s |
| Charger | Charge Speed | 8 | 6 |

## Week 11 — Data and saves

16.11.2026 — I asked AI to move player, enemy, and item balance values into ScriptableObject assets. I tested the system by changing player health from 3 to 6 without editing code.

16.11.2026 — I asked AI to implement JSON saving for coins, best score, unlocked levels, and completed levels. I tested a corrupted save file and confirmed that the game loaded default progress without crashing.

17.11.2026 — I decided to make coins optional in the three campaign levels because I wanted players to choose how many coins to collect before reaching the exit.

17.11.2026 — I kept BestScore as the highest coin score from one completed level and TotalCoins as the accumulated coins from all completed runs.

17.11.2026 — I tested Reset Progress and confirmed that Cancel preserves progress while Confirm resets it. I also verified that settings persist after restarting Unity Play Mode.