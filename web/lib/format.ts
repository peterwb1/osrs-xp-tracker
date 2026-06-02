/** Compact XP formatting for widgets, e.g. 2_400_000_000 → "2.40B". */
export function formatXp(xp: number): string {
  if (xp >= 1_000_000_000) return (xp / 1_000_000_000).toFixed(2) + 'B';
  if (xp >= 1_000_000) return (xp / 1_000_000).toFixed(2) + 'M';
  if (xp >= 1_000) return (xp / 1_000).toFixed(1) + 'K';
  return xp.toLocaleString('en-GB');
}

/** A signed XP delta, e.g. 12_500 → "+12.5K", 0 → "—". */
export function formatXpDelta(xp: number): string {
  return xp > 0 ? '+' + formatXp(xp) : '—';
}
