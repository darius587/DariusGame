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