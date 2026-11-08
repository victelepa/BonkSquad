# Map01 pinball combat

Open `Assets/Scenes/levels/Map01_RefractionArena.unity` and enter Play Mode. Start opens a preview of that wave's actual specialist roster, counts, models, abilities, and kill tips. Start Wave begins the 180-second timer. The separate playable Tutorial and in-arena Help remain available.

## Waves

| Wave | Specialists, one of each listed | Starting basic minions |
|---|---|---:|
| 1 — Mortar practice | Mortar | 3 |
| 2 — Blasts and mines | Mortar, mine layer | 3 |
| 3 — Spawn and snare | Spawner, net shooter | 4 |
| 4 — Moving targets | Mortar, mine layer, runner | 4 |
| 5 — Final pursuit | Spawner, net shooter, runner | 4 |

Positions change every attempt. Broad specialist clearance, supported floor checks, minimum separation, bounded sampling, and fallback positions remain in use. Bat sweep circles are reserved during placement. Basic minions follow horizontal, vertical, or diagonal patrols and reverse at obstacles; their visible attack halo catch range and wider silhouette remain unchanged. The first wave's minions move at 0.65 ball diameters/second; later starting minions use 1.2.

## Enemies and damage

- **Spawner (S, green):** two health segments, eight-second spawn interval, one-second warning, two living children, three lifetime children. Surviving a hit preserves its spawn schedule and pending warning. A blocked placement retries safely; defeat immediately cancels its warning. Surviving children must still be cleared.
- **Mine layer (L, orange):** two health segments; leaves mines with a 1.5-second arming warning, 12-second lifetime, and 1.5-ball-diameter blast radius. An accepted hit shows `Mine blast −20`, an expanding orange blast, the shared player flash, and a short blast sound.
- **Mortar (M, purple):** two health segments; records the player's location when firing. The warning and blast share a 2.75-ball-diameter radius, with two seconds to leave. Its own shell removes one segment if the mortar remains in its own blast. Other enemies are not hit by that shell. Self-defeats award 50 base points and one health-drop roll, without a ball-hit combo bonus.
- **Net shooter (N, cyan):** two health segments; telegraphs for one second, then fires a straight projectile every six seconds. Projectiles move at seven ball diameters/second, expire after three seconds, and stop on solid obstacles. A net deals no direct health damage. It stops either form for 0.6 seconds, shows a net cage and release countdown, and interrupts the combo. Nets cannot extend an active snare or catch a dizziness-stunned player. Release grants 1.5 seconds of net immunity.
- **Runner (R, red):** three health segments and a predictable diagonal patrol. Speed increases from 0.8 to 1.2 to 1.6 ball diameters/second after surviving hits, with hit feedback and a `FASTER!` cue.

Specialists require physical ball impacts at the shared small-enemy speed threshold. Impact can remove two segments; the minion halo excludes specialists. Below-speed hits do no damage and consume no charge. Surviving collision hits rebound at preserved speed; killing hits pass through. Separate before hitting the same enemy again.

Player health is shared across forms: 100 maximum, 20 damage per attack, and one second of protection after accepted damage. Minion contact hurts robot form; explosions hurt both. Accepted damage pushes away from the source and flashes the player/screen. Rejected hits do not change the damage explanation or retrigger the flash.

Every actual defeat, including minions and mortar self-defeats, independently rolls a 5% chance to drop +25 health. Pickups cap health at maximum and remain available when health is full. Cleanup and expiry do not roll drops. Health carries between cleared waves.

## Score and green Q charges

Start with four green charges below health. Every 100 points automatically adds another charge, including above four; there is no separate token purchase screen. Q spends one charge to Dash in ball form, keeping the existing five-second cooldown. A popup announces earned charges. Score, progress, charges, and cooldown carry between waves and restore to the wave-start checkpoint on retry.

Wave previews show abilities and **How to Kill**, without roster counts, speed thresholds, or bat instructions. Mortars patrol predictable routes at 0.5 ball diameters per second. Minions move at 0.9 in wave one and 1.5 in later waves, including spawned reinforcements.

## Rotating bats and map readability

Two cyan rods rotate in validated open areas, at least 14 ball diameters apart. Their visible arrows show the launch direction. A fresh ball contact launches along that direction with at least 12 world units/second or a 15% increase over the incoming speed, capped at the player's maximum. Robot form is not launched. The bat requires separation and a 0.4-second cooldown before another launch, and bat impacts do not also receive a wall boost.

Ordinary meaningful wall/object impacts retain their 15% speed bonus, gold feedback, short sound, and speed cap. Floor contact, glancing scraping, enemy hits, and uninterrupted contacts cannot farm wall boosts.

The map uses matching specialist letters, solid minion markers, a larger P for the player, + for health, and B for bats. Its legend updates for each roster and living reinforcements. World cues identify arming mines, armed mines, incoming minions, net-shot warnings, runner speed increases, and accepted damage sources. Help pauses the wave, protection, cooldown, snare, and enemy attack timers.

## Setup and validation

`Tools > BonkSquad > Install Pinball Expansion` updates existing Map01 combat with the agreed roster, reusable NetShooter/Runner prefabs and materials, larger mortar radius, four starting charges, and exactly two bats. It preserves arena geometry and the existing player movement tuning. It can be rerun without duplicating bats. The full `Setup Map01 Enemy Waves` command creates the same progression but restores prototype prefab defaults.

Tuning lives on the relevant prefab/components: score threshold and bonuses on PinballScore; net speed/lifetime/snare/immunity on NetShooterEnemy; runner speed stages on RunnerEnemy; blast radius/timing on CombatHazard; rotation/launch/cooldown on RotatingPinballBat; initial Dash and capacity on BonkRobotController.

- **Validate Pinball Expansion (Play Mode):** actual roster/preview flow, mine labels/protection, mortar self-damage and single defeat, token boundaries and purchases, full-Dash rejection, retry checkpoints, surviving-spawner scheduling/caps/cancellation, net cover and immunity, pause preservation, runner stages, bat direction/caps/forms, and all five wave transitions.
- **Validate Combat (Play Mode):** larger legacy stress encounters, 500 seeded normal/fallback placement cases, health/drop boundaries, speed-qualified multi-hit combat, spawn budgets, hazards, wave priority, retry, and restart.
- **Validate Onboarding (Play Mode):** HUD, skill-token messaging, wave-preview flow, Dash, Help, and the existing playable tutorial with real keyboard input.

These validations exit Play Mode without saving gameplay changes. Visual polish, warning readability, bat routes, and mixed-wave difficulty still benefit from a human playtest.
