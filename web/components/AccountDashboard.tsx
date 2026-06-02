'use client';

import type { ReactNode } from 'react';
import { formatDistanceToNow } from 'date-fns';
import { formatXp, formatXpDelta } from '@/lib/format';

export interface AccountSummary {
  displayName: string;
  osrsUsername: string;
  lastPolledAt: string | null;
  totalLevel: number;
  totalXp: number;
  combatLevel: number;
  xpGainedToday: number;
  xpGainedThisWeek: number;
  fastestSkill: { skillName: string; xpGained: number } | null;
  lastLevelUp: { skillName: string; level: number; at: string } | null;
}

function Widget({
  label,
  value,
  sub,
  size = 'lg',
}: {
  label: string;
  value: ReactNode;
  sub?: string;
  size?: 'lg' | 'sm';
}) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm dark:border-gray-700 dark:bg-gray-800">
      <p className="text-xs font-semibold uppercase tracking-wide text-gray-400 dark:text-gray-500">
        {label}
      </p>
      <p
        className={`mt-1 font-bold text-gray-900 dark:text-white tabular-nums ${
          size === 'lg' ? 'text-xl' : 'text-sm'
        }`}
      >
        {value}
      </p>
      {sub && <p className="mt-0.5 text-xs text-gray-400 dark:text-gray-500">{sub}</p>}
    </div>
  );
}

/**
 * At-a-glance widgets for a single account, fed by GET /api/accounts/{id}/summary.
 * Rendered as a 2-column grid on mobile and a single sidebar column on desktop.
 */
export function AccountDashboard({ summary }: { summary: AccountSummary }) {
  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-1">
      <Widget label="Combat" value={summary.combatLevel} />
      <Widget label="Total level" value={summary.totalLevel.toLocaleString('en-GB')} />
      <Widget label="Total XP" value={formatXp(summary.totalXp)} />
      <Widget label="XP today" value={formatXpDelta(summary.xpGainedToday)} />
      <Widget label="XP this week" value={formatXpDelta(summary.xpGainedThisWeek)} />
      <Widget
        label="Top skill this week"
        value={summary.fastestSkill?.skillName ?? '—'}
        sub={
          summary.fastestSkill
            ? `+${formatXp(summary.fastestSkill.xpGained)} xp`
            : 'No gains yet'
        }
      />
      <Widget
        label="Last level-up"
        value={
          summary.lastLevelUp
            ? `${summary.lastLevelUp.skillName} ${summary.lastLevelUp.level}`
            : '—'
        }
        sub={
          summary.lastLevelUp
            ? formatDistanceToNow(new Date(summary.lastLevelUp.at), { addSuffix: true })
            : 'None in the last 30 days'
        }
      />
      <Widget
        label="Last updated"
        size="sm"
        value={
          summary.lastPolledAt
            ? formatDistanceToNow(new Date(summary.lastPolledAt), { addSuffix: true })
            : 'Never'
        }
      />
    </div>
  );
}
