# Shared elite respawn and completed location quests

User approved five-minute respawn shared by all players. The existing persisted victory grant ledger owns the last kill time; a targeted monster/time index supports the shared lookup. No duplicate timer state or new infrastructure is needed. Concurrent fights already admitted before the first death can finish; this slice prevents new selection/start during the respawn period, not global single-encounter ownership.

Add explicit 300-second respawn content to open-world elite encounters, validate durations, exclude cooling targets from exploration and selection, and revalidate at combat creation. Scene returns respawn deadlines and filters COMPLETED quest offers, preserving READY_TO_CLAIM. Vue renders server-based countdowns and refreshes the scene after quest actions. Verify cross-player denial, stale encounter denial, exact expiry, completed versus ready quest visibility, restart persistence and frontend disabled state.

Completion filtering includes modern states, legacy contract completions and reward grants, matching the quest journal. Existing rare hourly schedules remain additional restrictions.
