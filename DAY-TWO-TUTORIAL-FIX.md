# Day-Two Tutorial Return and Retry

## Behavior

- Day two cannot end through the submarine until every tutorial stage is complete. Both direct return requests and normal submarine interaction use this guard.
- Low gas does not bypass the guard. This follows the later bug-fix checklist, which supersedes the older design document's below-15-percent early-return exception.
- Running out of gas on day two reloads the current ocean scene and restarts training from its first instruction, without entering Results or advancing the day.
- Mining state and pending purchases are restored from their values at the start of the failed excursion. Starting packages are delivered again, including packages collected during the failed attempt.
- Retry restores initial position, gas, machines and movement through fresh scene initialization. Credits and previous dialogue are preserved; no failed-attempt income or settlement is applied.
- The nearby submarine prompt explains why return is locked. The final tutorial reminder shows a countdown until return becomes available.
- Completing training permits the normal Results path. Later days retain their existing return and gas-exhaustion paths.

## Verification

The final focused Play run in Unity 6000.6.4f1 on 2026-10-29 passed 19 assertions covering blocked early return, the low-gas guard, gas exhaustion while scanning, restored mining and delivery state, fresh tutorial state, final-reminder countdown layout, return confirmation after completed training, and normal settlement to day three.

Existing tutorial setup checks passed. The updated day/delivery Play regression passed 50 assertions, including successful day-two return after training, later-day deliveries, manual return confirmation, and later-day gas exhaustion using the Results path.

## Boundaries

Return confirmation was added separately in `RETURN-CONFIRMATION.md`. No ordinary pause fix, scene fade, or minimap machine marker is included here. No standalone player build was tested. The previously reported Rock 12 assertion was subsequently resolved as a check issue, and the updated full Ocean Play check passed 142 assertions; see `ROCK12-COLLISION-CHECK.md`.
