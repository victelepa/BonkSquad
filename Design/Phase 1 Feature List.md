# BonkSquad Phase 1 Feature List

## Phase goal

Prove the core loop: the human positions, the ball ricochets, a fast ball kills, and chasers punish mistakes.

This phase builds on the current prototype values:

- Ball maximum speed: 18
- Human speed: 6
- Small enemy speed: 2.5
- Large enemy speed: 1.0

## Combat rules

| Feature | Notes | Priority |
|---|---|---|
| Speed gated kill | Kill only if contact speed meets the monster threshold, measured from the pre-collision relative velocity | Must |
| Ball is immune and human takes damage | Monsters only hurt the player in human form | Must |
| Weak hit response | A below-threshold hit bounces or knocks the monster back and shows a too slow cue | Must |
| Player health | Three hits, brief invulnerability after a hit, and small knockback | Must |
| Two speed tiers | Small monsters die at a lower speed and heavy monsters require a higher speed | Must |

## Ball movement changes

| Feature | Notes | Priority |
|---|---|---|
| Weaker ball acceleration | Speed is hard to gain without help, so positioning matters | Must |
| Natural speed decay | The ball slows over time and cannot circle forever | Must |
| Wall ricochet retention | Normal walls keep most speed and red panels add a boost | Must |
| Temporary speed overcap | Red panels can push the ball past 18 before speed decays toward the normal cap, making the heavy tier reachable | Must |
| Ball cannot climb ramps or platforms | Slopes and raised areas are human only | Must |
| Ricochet soft cap | Prevents infinite bounce speed stacking | Should |

## Human form

| Feature | Notes | Priority |
|---|---|---|
| Slope and ramp climbing | Reaches floating platforms | Must |
| Pickup collection | Collects items by walking into them | Must |
| Momentum carry into ball | Keeps the existing drift and velocity transfer, tuned for setup shots | Must |
| Transform feedback | Shows a clear visual and sound cue when switching | Should |

## Monsters

| Feature | Notes | Priority |
|---|---|---|
| Chase AI | Chases the human only and ignores or wanders when the player is a ball | Must |
| Chaser speed below human speed | Keeps escape possible, with a starting target of 4 versus 6 | Must |
| Heavy monster | Moves slowly, chases, and requires the top speed tier to kill | Must |
| Contact damage with cooldown | Prevents instant multi-hit deaths | Must |
| Flee from fast ball | Optional behavior that makes the ball feel threatening | Stretch |

## Map pieces

| Feature | Notes | Priority |
|---|---|---|
| Red bounce panel | Produces an instant fast bounce with a flash and cooldown | Must |
| Ramp and floating platform with pickup | Creates a human-only reward area | Must |
| Modular layout system | Spawns pieces at anchor points from a layout preset | Must |
| Three hand-built layouts | Provides variations of the same arena | Must |
| Ice patch | Adds a different control feel with low implementation cost | Should |
| Speed gate | Teaches thresholds as a double-edged map piece and can wait for Phase 2 | Stretch |

## Pickups

| Feature | Notes | Priority |
|---|---|---|
| Health pickup | Often appears on platforms | Must |
| Speed surge | Temporarily raises the speed cap | Should |
| One curse pickup | Provides a visually distinct risk and reward choice | Stretch |

## Game loop

| Feature | Notes | Priority |
|---|---|---|
| Wave manager | Spawns monsters, detects wave clear, and swaps layouts | Must |
| Three waves | Introduces one new element in each wave | Must |
| Short pause between waves | Previews the layout change and resets the arena | Must |
| Win and lose screens with instant restart | Uses a single key to retry | Must |
| Endless mode | Continues after wave 3 with random layouts and scaling difficulty | Stretch |

## User interface and feedback

| Feature | Notes | Priority |
|---|---|---|
| Speed gauge with threshold marks | Always shows why a hit worked or failed | Must |
| Ball color or trail by tier | Makes the active tier readable at a glance from the top-down view | Must |
| Health wave and enemies remaining display | Provides the basic heads-up display | Must |
| Kill fail and damage feedback | Uses particles, flashes, and simple sounds | Must |
| Updated controls overlay | Reflects the new rules | Should |
| Light camera shake on heavy kills | Keeps heavy impacts readable without obscuring play | Stretch |

## Technical foundation

| Feature | Notes | Priority |
|---|---|---|
| Tuning configuration | Stores thresholds, speeds, and health in one ScriptableObject | Must |
| Debug overlay | Shows live speed and the current tier and supports quick value changes | Should |
| Prefab based map pieces and monsters | Makes content easy to reuse and swap | Must |

## Explicitly outside Phase 1

- Squadmates and rescue objectives
- Procedural layout generation
- Mid-wave arena transformations
- Permanent upgrades or meta progression
- Boss fights
- Multiplayer
- A large monster roster beyond chaser and heavy types

## Suggested wave outline

1. Wave 1 uses small chasers and two red panels to teach the basic loop.
2. Wave 2 introduces a new layout, a platform with a health pickup, and more chasers.
3. Wave 3 introduces a heavy monster, an ice patch, and a layout that requires a strong red-panel launch.

## Phase success check

Phase 1 is complete when a new player can:

1. Understand why a hit killed or failed.
2. Feel a reason to switch forms at least every 10 seconds.
3. Finish or fail and then restart without help.
4. Want to try again to improve.
