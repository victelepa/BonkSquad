# Enemy wave prototype

Open `Assets/Scenes/levels/Map01_RefractionArena.unity` and enter Play mode. The single-player main menu opens first. Play shows five short guided cards with Back, Next, and Skip Tutorial on every new run; How to Play returns to the menu afterward. Start each wave from its enemy preview carousel. The teaching sequence is four slow basic minions; one spawner plus three minions; one mine layer plus four minions; one mortar plus four minions; then one of each specialist plus four minions. Each wave lasts 180 seconds. Minions have a one-ball-diameter-wide collider and a wider matching silhouette to make aiming more forgiving. The developer-only `TryAllEnemyTypes` API remains available for validation; it is not shown in the player UI.

## Combat rules

- Specialists have two health segments. A ball impact at the shared small-enemy speed threshold removes one segment; lower speeds do no damage. Separate before hitting the same enemy again. Surviving enemies rebound the ball without reducing its speed.
- Green spawners produce one-hit, contact-damaging minions after a one-second warning. Each spawner can produce three minions over its lifetime, with two living children per spawner and an eight-second spawn interval. The shared generated-minion cap remains six; each teaching wave has only one spawner. These caps are separate from the starting minions. Destroy surviving minions to clear the wave.
- Orange mine layers wander and leave mines. Pulsing rings indicate the 1.5-second arming period. Mines expire after 12 seconds and have a blast radius of 1.5 ball diameters.
- Purple mortars fire at a snapshot of the player's position. Leave the marked two-diameter blast area before the shell lands two seconds later.
- Player health is shared across forms: 100 maximum, 20 damage per attack, and one second of protection after damage. Accepted enemy hits push the player away from the damage source with strength 6 and a 0.2-second recovery before steering/acceleration resumes. Sideways momentum is retained and total speed stays capped. The existing one-second protection also prevents repeated knockback. Minion contact damages robot form only; explosions affect both forms. Both forms respond, including a robot stunned by dizziness. Retry clears knockback. Tune strength and recovery on the player’s BonkRobotController.
- Accepted damage briefly tints both player forms red and flashes the screen edges with a faint screen tint, fading over 0.35 seconds. Protected hits do not retrigger feedback; retry clears it.
- Every defeat, including minions, rolls independently for a 5% chance of dropping +25 health. Full-health players leave pickups available. Drops persist until collection or the wave ends; there are no guaranteed healing objects.
- Health carries between successful waves. Death or timeout offers a full-health retry of the current wave with fresh positions. Restart Run returns to the first introduction.

## Pinball scoring and Impact Charge

- Each basic minion defeat awards 10 points; each specialist awards 50.
- A meaningful ball rebound from a wall or obstacle at attack speed lights Ricochet Shot for three seconds. The next defeat adds 25 bonus points and consumes that bounce opportunity. Ground contact, enemy collisions, and repeated callbacks from an unbroken surface contact do not light it.
- A second kill within three seconds adds Double Bonk (+30); a third adds Triple Bonk (+50). The window refreshes on each kill. Later kills in the same chain earn base points and any fresh ricochet bonus, without repeatedly awarding Triple Bonk.
- Damage interrupts the current chain and ricochet opportunity. Missed/expired combos never subtract earned points.
- Every 100 points automatically grants one Impact Charge; excess meter progress and charges are retained. The next speed-qualified ball hit consumes one charge to deal two health segments. Below-speed hits consume nothing. A charge also applies to a minion if that is the next qualifying target.
- The HUD shows score, charge count, progress to the next charge, and combo feedback. Bonus announcements also use a large gold-rimmed banner with a white-to-gold highlight, a brief size pulse, and a soft fade; interrupted combos do not trigger the reward banner. Successful kills preserve momentum and pass through; damage causes disruptive knockback.
- Score, meter progress, and charges carry between cleared waves. Retry restores the values from the beginning of that wave, preventing repeated failed attempts from farming rewards. Restart Run and the Try All shortcut begin a fresh score.
- Common minions wander at 0.65 ball diameters per second in wave one and 1.2 in later waves, on fixed patrol lines. Spawner children patrol at two diameters per second and arrive at safe positions throughout the map after a one-second warning. All must be defeated to clear the wave.

## Minimap

The unified runtime Canvas groups health, current form, speed readiness, dizziness, Impact Charges, and score at the bottom. The objective, enemy count, and time sit at top center. The legacy controls and acceleration-chart overlays remain available in other scenes but are suppressed in Map01.

The lower-right minimap shows the arena floor and obstacles with north fixed at the top. A large cyan P marks the player; green S marks a spawner, orange L a mine layer, purple M a mortar, and solid yellow squares mark minions. Markers use filled backgrounds rather than tiny text dots, and the player draws last so it remains visible. The legend updates with each wave’s roster and any living reinforcements. Markers follow living enemies and disappear immediately when defeated. The map uses the active player's position in either form.

## Onboarding and results

Every wave previews only its actual specialist roster and basic minions. Manual arrows change cards, and Start Wave is always available. Mesh-only copies of prefab visuals render in an isolated camera; they have no scripts, colliders, or rigidbodies. Names, colors, symbols, abilities, and kill tips are Inspector fields on CombatPresentation. Hit counts, speed thresholds, counts, and timers come from gameplay data.

Help reopens the guided cards while saving and pausing the simulation time scale. Wave time, enemy attacks, health protection, stun duration, dizziness, and player momentum remain frozen. Closing Help restores the prior page and time scale. A new run repeats the tour; retry starts the same wave directly.

Failure results distinguish health reaching zero from timeout and show elapsed time, remaining health, and score. Timeout also shows the remaining enemy count. Retry restores full health, resets the wave score checkpoint, and reshuffles enemies. Main Menu cleans up the encounter. Successful waves retain health and scoring; victory offers Restart Run.

World cues match preview/minimap symbols. Mines distinguish arming from armed, mortar circles say to move, incoming minions are labeled, and healing objects show +25 health or Health full while rejected.

`Tools > BonkSquad > Install Map01 Onboarding UI` adds the presentation component to an existing setup without changing encounter tuning. The full setup command below also installs it.

## Placement and setup

Every wave starts the player at the saved starting pose, with velocity and dizziness reset. Specialists are randomly positioned in four connected outer spawn strips, with at least six ball diameters from the player and four between enemies. Placement checks floor support, obstacle clearance, and hazard exclusion. Broad clearance keeps stationary enemies out of narrow passages. Bounded random attempts fall back to shuffled, validated corner positions.

`Tools > BonkSquad > Setup Map01 Enemy Waves` regenerates the reusable prefabs in `Assets/Prefabs/Combat` and configures Map01. It replaces the previous static enemies and can be rerun without duplicating encounter objects. Save custom prefab tuning before rerunning, because this command restores prototype defaults. SampleScene remains a separate legacy test scene.

The `Map01 Enemy Waves` object holds wave definitions, placement regions, and the HUD. Enemy behaviour, player health, hazard timings, radii, and drop chance are Inspector settings. The 5% drop default is independent of placement random rolls and can be supplied a deterministic random function for checks.

## Validation

`Tools > BonkSquad > Validate Combat (Play Mode)` runs scripted integration checks in Map01 and exits Play mode afterward. It covers seeded random and fallback placement, health and protection, exact drop boundaries, two-hit enemies, capped spawning, hazard timing, wave progression, death/clear priority, timeout, retries, and restart. It does not save the Play mode changes. `Tools > BonkSquad > Validate Onboarding (Play Mode)` separately drives actual UI buttons through menus, tour/back/skip, preview isolation, paused help, timeout/death, retries, next wave, mixed roster, and victory/restart, with text-fit and aspect-ratio checks.

Also manually play through the encounter to tune warning visibility, movement space, aim difficulty, and time limits. Visuals are primitive prototype models; final art remains future work. First-time-player comprehension still needs an external playtest.

### Recorded validation — October 30, 2026

Setup and Play Mode integration checks passed in an isolated Unity 6000.5.10f1 project copy, including 500 seeded random/fallback placement cases, arena-wide minion distribution, actual roaming movement, and accepted-hit flash trigger/reset/fade checks. Repeated setup leaves one encounter. The project was subsequently restored to its previously working Unity 6000.5.10f1 version with matching packages, and XCharts pooling was updated to avoid removed instance-ID APIs. The repository's older 6000.3.6f1 editor installation has an invalid application signature. Manual game-feel tuning remains.

### Teaching balance

Dizziness builds at half the previous rate using an Inspector multiplier of two, doubling time at a given speed before a warning. Recovery, warning duration, and stun duration remain unchanged. `Tools > BonkSquad > Apply Map01 Teaching Balance` reapplies the five-wave progression, wider minion prefab, and lower spawner budget without rebuilding arena geometry. Full setup uses the same wave definitions and prefab defaults.

### Pinball assistance

Fresh, meaningful wall or obstacle rebounds add 15% speed, capped at the existing maximum. Floor contact, enemy hits, glancing scrapes, and uninterrupted contact do not earn boosts. A short cooldown prevents multiple boost awards in one corner impact. Boosts flash the attack ring gold and play a short synthesized sound; attack-speed movement leaves a cyan trail.

At bonking speed, a visible cyan attack halo extends 0.3 ball diameters beyond the ball radius and catches nearby basic minions along the next physics step. It uses the normal defeat, combo, charge, and drop path, preserves momentum, and checks solid occlusion. Specialists still require physical impacts. Slow movement, robot form, and inactive encounters cannot use the halo. Wall bonus, minimum impact speed, cooldown, halo reach, and sound volume are Inspector settings on BouncingBallController. Aim assist and last-enemy movement changes are not enabled in this trial.

### Predictable minion routes

Starting minions and spawner children cycle through horizontal, vertical, and the two diagonal axes. Each follows a straight line at constant speed and reverses along that same line at an arena edge or solid obstacle, with a 0.2-second turnaround pause. Random direction timers no longer affect these minions. World labels show the current direction arrow, so players can plan an intercept. Specialist wandering is unchanged.
