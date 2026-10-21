# BonkSquad Progression Plan

## Core loop

Enter an arena, build momentum, defeat every monster, choose an upgrade, and enter a harder arena. A complete run should last about 20–30 minutes and contain five arenas followed by a boss.

## Recommended first progression slice

1. Divide combat into three enemy waves in the existing arena.
2. Complete a wave when its monster count reaches zero.
3. Present three upgrade choices between waves.
4. Restore one health and preserve some earned speed between waves.
5. Complete the arena after wave three and show score, clear time, and highest speed.

This slice reuses the current mechanics and proves progression before additional levels are built.

## Level unlock structure

Use monster defeats to change the current arena, but require a full arena clear to unlock the next level:

- **Early-wave defeats:** activate additional yellow boost tiles and open a shortcut.
- **Wave cleared:** open the arena gate and release the next enemy group.
- **Final wave cleared:** unlock the exit portal and the next level.
- **Heavy enemy defeated:** award an upgrade choice or a large team momentum bonus.

This gives every defeat visible value without letting players accidentally leave an unfinished encounter. The HUD should show `Wave 1/3`, remaining monsters, and the next unlock condition.

Suggested initial level sequence:

1. **Training Yard:** open layout, small enemies, one heavy finale.
2. **Ricochet Factory:** narrow bounce lanes, moving barriers, charger enemies.
3. **Launch Foundry:** ramps, pits, elevated routes, ranged enemies.
4. **Crusher Arena:** timed hazards, shield enemies, multiple heavies.
5. **Boss Core:** a multi-phase boss that changes the arena surfaces.

## Upgrade categories

- **Momentum:** higher speed cap, lower natural speed loss, stronger wall boosts.
- **Control:** stronger steering, wider aim assist, improved braking.
- **Impact:** lower defeat thresholds, stronger knockback, combo speed rewards.
- **Survival:** extra maximum health, healing after a wave, longer invulnerability.
- **Power-ups:** shorter spawn delay, stronger pickups, an additional active pickup.

Upgrades should change one understandable number at a time. Offer three random choices and prevent the same upgrade from appearing twice in one selection.

## Difficulty progression

- Add enemies gradually instead of only increasing their health.
- Introduce one new enemy behavior or arena hazard per arena.
- Increase heavy-enemy presence in later waves.
- Use tighter obstacle layouts only after steering upgrades become available.
- Reserve the highest required impact speeds for telegraphed elite enemies and bosses.

## Long-term structure

- Unlock new robot shells with different starting bonuses.
- Award currency based on arena clears, time, combos, and remaining health.
- Spend currency on new starting options rather than permanent raw damage advantages.
- Track best clear time, highest speed, longest combo, and fewest hits taken.

## Next implementation milestone

Add a `RunManager` that owns the current wave, score, elapsed time, and upgrade selections. Enemy spawning and victory UI should report to this manager rather than relying directly on scene enemy objects.
