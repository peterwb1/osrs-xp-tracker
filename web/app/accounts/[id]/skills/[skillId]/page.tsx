'use client';

import { useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import Image from 'next/image';
import { useQuery } from '@tanstack/react-query';
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
} from 'recharts';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { queryKeys } from '@/lib/queryKeys';
import { skillIconUrl } from '@/lib/skills';
import { ThemeToggle } from '@/components/ThemeToggle';
import { Spinner } from '@/components/Spinner';

interface HistoryPoint {
  capturedAt: string;
  xp: number;
  level: number;
  rank: number;
}

interface SkillSnapshot {
  skillId: number;
  skillName: string;
  displayOrder: number;
  xp: number | null;
  level: number | null;
  rank: number | null;
}

export default function SkillHistoryPage() {
  const { isAuthenticated } = useAuth();
  const router = useRouter();
  const { id, skillId } = useParams<{ id: string; skillId: string }>();

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
    queryKey: queryKeys.skillHistory(id, skillId),
    queryFn: () =>
      api
        .get(`/api/accounts/${id}/skills/${skillId}/history?days=30`)
        .then((r) => r.data),
    enabled: isAuthenticated && !!id && !!skillId,
  });

  const chartData = history?.map((s) => ({
    date: new Date(s.capturedAt).toLocaleDateString('en-GB', {
      day: 'numeric',
      month: 'short',
    }),
    xp: s.xp,
  }));

  if (!isAuthenticated) return null;

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      <div className="mx-auto max-w-3xl p-6">
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
            {skill?.skillName ?? 'Skill'} — XP History
          </h1>
        </div>

        {isLoading && <Spinner />}

        {isError && (
          <p className="text-red-600 dark:text-red-400">Failed to load history.</p>
        )}

        {!isLoading && !isError && chartData && chartData.length < 2 && (
          <div className="rounded-xl border border-gray-200 bg-white p-8 text-center dark:border-gray-700 dark:bg-gray-800">
            <p className="text-gray-500 dark:text-gray-400">
              No history yet — this account needs at least two polls to show a chart.
            </p>
            <p className="mt-1 text-xs text-gray-400 dark:text-gray-500">
              The poller runs every 6 hours. Check back later.
            </p>
          </div>
        )}

        {chartData && chartData.length >= 2 && (
          <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <p className="mb-4 text-xs text-gray-400 dark:text-gray-500">Last 30 days</p>
            <ResponsiveContainer width="100%" height={320}>
              <LineChart data={chartData} margin={{ top: 4, right: 8, bottom: 4, left: 8 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
                <XAxis
                  dataKey="date"
                  tick={{ fontSize: 11, fill: '#9ca3af' }}
                  stroke="#9ca3af"
                />
                <YAxis
                  tickFormatter={(v: number) => (v / 1_000_000).toFixed(1) + 'M'}
                  tick={{ fontSize: 11, fill: '#9ca3af' }}
                  stroke="#9ca3af"
                  width={52}
                />
                <Tooltip
                  formatter={(v) => [typeof v === 'number' ? v.toLocaleString() : v, 'XP']}
                  labelStyle={{ color: '#f9fafb' }}
                  contentStyle={{
                    backgroundColor: '#1f2937',
                    border: '1px solid #374151',
                    borderRadius: '8px',
                    color: '#f9fafb',
                  }}
                />
                <Line
                  type="monotone"
                  dataKey="xp"
                  stroke="#6366f1"
                  strokeWidth={2}
                  dot={false}
                  activeDot={{ r: 4 }}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>
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
