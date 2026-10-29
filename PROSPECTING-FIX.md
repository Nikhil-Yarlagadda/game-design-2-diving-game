# Prospecting While Moving

Holding E can now start prospecting while swimming or coasting. Starting the scan immediately zeroes velocity and locks movement. Releasing E cancels the scan and restores movement. No new controls or UI elements were added.

The three-second duration, five-second cooldown after completion, cancellation without cooldown, and requirement for a fresh E press to start another scan are unchanged. Tutorial text explicitly allows starting while swimming and explains the automatic movement lock. The top-left HUD shows Ready, Scanning (movement locked; release E to cancel), or a cooldown countdown.

## Verification

Verified in Unity 6000.6.4f1 on 2026-10-29:

- Existing mining/tutorial setup checks passed with the updated text.
- A focused real-keyboard-input Play check passed 16 assertions, including starting while swimming, residual velocity, opposing movement keys, position lock, blocked placement, cancellation, resumed swimming, completion, cooldown, no automatic repeat, and ready/scanning/cooldown HUD text fitting its existing area.
- The player loop advanced 4,933 frames during the latest focused check.
- Existing Ocean Play check assertions were updated to accept scan starts during movement and coasting; its temporary input-routing configuration is restored afterward.

## Verification Boundary

Initial full Ocean Play runs encountered an initialization timing issue and a final-frame-only Rock 12 contact assertion. These check issues were subsequently diagnosed and corrected; see `ROCK12-COLLISION-CHECK.md`. The complete updated Ocean Play check passed 142 assertions, including prospecting.

No standalone player build was tested. Other pending gameplay bugs remain separate.
