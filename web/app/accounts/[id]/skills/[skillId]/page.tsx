'use client';

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import Image from 'next/image';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { queryKeys } from '@/lib/queryKeys';
import { skillIconUrl } from '@/lib/skills';
import { ThemeToggle } from '@/components/ThemeToggle';
import { Spinner } from '@/components/Spinner';
import { Segmented } from '@/components/Segmented';
import { XpHistoryChart, metricSeries, type Metric, type HistoryPoint } from '@/components/XpHistoryChart';

interface SkillSnapshot {
  skillId: number;
  skillName: string;
  displayOrder: number;
  xp: number | null;
  level: number | null;
  rank: number | null;
}

const RANGE_OPTIONS = [
  { label: '7d', value: 7 },
  { label: '30d', value: 30 },
  { label: '90d', value: 90 },
  { label: 'All', value: 36500 }, // ~100 years; effectively unbounded
];

const METRIC_OPTIONS: { label: string; value: Metric }[] = [
  { label: 'XP', value: 'xp' },
  { label: 'Rank', value: 'rank' },
];

export default function SkillHistoryPage() {
  const { isAuthenticated } = useAuth();
  const router = useRouter();
  const { id, skillId } = useParams<{ id: string; skillId: string }>();

  const [days, setDays] = useState(30);
  const [metric, setMetric] = useState<Metric>('xp');

  useEffect(() => {
    if (!isAuthenticated) router.push('/login');
  }, [isAuthenticated, router]);

  // Skills list may already be in cache from the detail page — free hit
  const { data: skills } = useQuery<SkillSnapshot[]>({
    queryKey: queryKeys.skills(id),
    queryFn: () => api.get(`/api/accounts/${id}/skills`).then((r) => r.data),
    enabled: isAuthenticated && !!id,
  });

  const skill = skills?.find((s) => s.skillId === Number(skillId));

  const { data: history, isLoading, isError } = useQuery<HistoryPoint[]>({
    queryKey: queryKeys.skillHistory(id, skillId, days),
    queryFn: () =>
      api
        .get(`/api/accounts/${id}/skills/${skillId}/history?days=${days}`)
        .then((r) => r.data),
    enabled: isAuthenticated && !!id && !!skillId,
    placeholderData: keepPreviousData, // keep the chart visible while switching range
  });

  const series = history ? metricSeries(history, metric) : [];
  // Rank exists in the data but every point is unranked (-1) for this skill.
  const rankUnavailable =
    metric === 'rank' && !!history && history.length >= 2 && series.length < 2;

  if (!isAuthenticated) return null;

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      <div className="mx-auto max-w-3xl px-4 py-6 sm:px-6">
        <div className="mb-6 flex items-center justify-between">
          <Link
            href={`/accounts/${id}`}
            className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
          >
            ← Skills
          </Link>
          <ThemeToggle />
        </div>

        <div className="mb-6 flex items-center gap-3">
          {skill && (
            <Image
              src={skillIconUrl(skill.skillName)}
              width={32}
              height={32}
              alt=""
              unoptimized
            />
          )}
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
            {skill?.skillName ?? 'Skill'} — History
          </h1>
        </div>

        {isLoading && <Spinner />}

        {isError && (
          <p className="text-red-600 dark:text-red-400">Failed to load history.</p>
        )}

        {!isLoading && !isError && history && (
          <>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <Segmented
                options={METRIC_OPTIONS}
                value={metric}
                onChange={setMetric}
                aria-label="Metric"
              />
              <Segmented
                options={RANGE_OPTIONS}
                value={days}
                onChange={setDays}
                aria-label="Time range"
              />
            </div>

            {series.length >= 2 ? (
              <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800">
                <XpHistoryChart data={series} metric={metric} />
              </div>
            ) : (
              <div className="rounded-xl border border-gray-200 bg-white p-8 text-center dark:border-gray-700 dark:bg-gray-800">
                {rankUnavailable ? (
                  <p className="text-gray-500 dark:text-gray-400">
                    Not ranked in this skill yet — no rank history to show.
                  </p>
                ) : (
                  <>
                    <p className="text-gray-500 dark:text-gray-400">
                      No history yet — this account needs at least two polls to show a chart.
                    </p>
                    <p className="mt-1 text-xs text-gray-400 dark:text-gray-500">
                      The poller runs every 6 hours. Check back later.
                    </p>
                  </>
                )}
              </div>
            )}
          </>
        )}

        {/* Current snapshot stats */}
        {skill && (
          <div className="mt-6 grid grid-cols-3 gap-4">
            {[
              { label: 'Level', value: skill.level ?? '—' },
              { label: 'XP', value: skill.xp != null ? skill.xp.toLocaleString() : '—' },
              { label: 'Rank', value: skill.rank != null ? skill.rank.toLocaleString() : '—' },
            ].map(({ label, value }) => (
              <div
                key={label}
                className="rounded-xl border border-gray-200 bg-white p-4 text-center shadow-sm dark:border-gray-700 dark:bg-gray-800"
              >
                <p className="text-xs font-semibold uppercase tracking-wide text-gray-400 dark:text-gray-500">
                  {label}
                </p>
                <p className="mt-1 text-xl font-bold text-gray-900 dark:text-white tabular-nums">
                  {value}
                </p>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
