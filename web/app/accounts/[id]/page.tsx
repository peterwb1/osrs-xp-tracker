'use client';

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import Image from 'next/image';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { queryKeys } from '@/lib/queryKeys';
import { skillIconUrl } from '@/lib/skills';
import { ThemeToggle } from '@/components/ThemeToggle';
import { Spinner } from '@/components/Spinner';
import { AccountDashboard, type AccountSummary } from '@/components/AccountDashboard';

interface SkillSnapshot {
  skillId: number;
  skillName: string;
  displayOrder: number;
  xp: number | null;
  level: number | null;
  rank: number | null;
}

// Mirrors the server-side cooldown so the button disables before a 429.
const COOLDOWN_MS = 5 * 60 * 1000;

function formatCountdown(ms: number): string {
  const total = Math.ceil(ms / 1000);
  const minutes = Math.floor(total / 60);
  const seconds = total % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

export default function AccountDetailPage() {
  const { isAuthenticated } = useAuth();
  const router = useRouter();
  const { id } = useParams<{ id: string }>();

  useEffect(() => {
    if (!isAuthenticated) router.push('/login');
  }, [isAuthenticated, router]);

  const { data: skills, isLoading, isError } = useQuery<SkillSnapshot[]>({
    queryKey: queryKeys.skills(id),
    queryFn: () => api.get(`/api/accounts/${id}/skills`).then((r) => r.data),
    enabled: isAuthenticated && !!id,
  });

  const { data: summary } = useQuery<AccountSummary>({
    queryKey: queryKeys.summary(id),
    queryFn: () => api.get(`/api/accounts/${id}/summary`).then((r) => r.data),
    enabled: isAuthenticated && !!id,
  });

  const queryClient = useQueryClient();
  const [refreshError, setRefreshError] = useState<string | null>(null);

  // Tick every second so the cooldown countdown updates and the button
  // re-enables on its own once the window passes.
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(t);
  }, []);

  const lastPolledMs = summary?.lastPolledAt ? new Date(summary.lastPolledAt).getTime() : 0;
  const cooldownLeftMs = lastPolledMs ? Math.max(0, COOLDOWN_MS - (now - lastPolledMs)) : 0;
  const onCooldown = cooldownLeftMs > 0;

  const refresh = useMutation({
    mutationFn: () => api.post(`/api/accounts/${id}/refresh`),
    onSuccess: () => {
      setRefreshError(null);
      // Refetch this account's skills, summary and history; plus the list's last-polled time.
      queryClient.invalidateQueries({ queryKey: ['accounts', id] });
      queryClient.invalidateQueries({ queryKey: queryKeys.accounts(), exact: true });
    },
    onError: (err) => {
      setRefreshError(
        isAxiosError(err) && err.response?.status === 429
          ? 'Recently refreshed — please wait a moment.'
          : "Couldn't refresh right now. Try again shortly."
      );
    },
  });

  if (!isAuthenticated) return null;

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6">
        <div className="mb-6 flex items-center justify-between">
          <Link
            href="/accounts"
            className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
          >
            ← Accounts
          </Link>
          <ThemeToggle />
        </div>

        <div className="mb-1 flex items-center justify-between gap-3">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
            {summary?.displayName ?? 'Account'}
          </h1>
          <button
            type="button"
            onClick={() => refresh.mutate()}
            disabled={refresh.isPending || onCooldown}
            title={onCooldown ? 'Recently refreshed' : 'Check the hiscores now'}
            className="shrink-0 rounded-lg bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {refresh.isPending
              ? 'Refreshing…'
              : onCooldown
                ? `Wait ${formatCountdown(cooldownLeftMs)}`
                : 'Refresh'}
          </button>
        </div>
        {refreshError && (
          <p className="mb-2 text-sm text-red-600 dark:text-red-400">{refreshError}</p>
        )}
        <p className="mb-6 text-sm text-gray-500 dark:text-gray-400">
          Click a skill to view its history
        </p>

        {isLoading && <Spinner />}
        {isError && (
          <p className="text-red-600 dark:text-red-400">
            Failed to load skills. This account may not exist.
          </p>
        )}

        {skills && (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
            {/* Skills first in the DOM so it stays the priority on mobile */}
            <section className="lg:col-span-2">
              <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-700 dark:bg-gray-800">
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500 dark:bg-gray-700 dark:text-gray-400">
                      <tr>
                        <th className="whitespace-nowrap px-3 py-3 sm:px-4">Skill</th>
                        <th className="whitespace-nowrap px-3 py-3 text-right sm:px-4">Level</th>
                        <th className="whitespace-nowrap px-3 py-3 text-right sm:px-4">XP</th>
                        <th className="whitespace-nowrap px-3 py-3 text-right sm:px-4">Rank</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
                      {skills.map((skill) => (
                        <tr
                          key={skill.skillId}
                          className="cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-700"
                          onClick={() => router.push(`/accounts/${id}/skills/${skill.skillId}`)}
                        >
                          <td className="whitespace-nowrap px-3 py-3 sm:px-4">
                            <div className="flex items-center gap-2">
                              <Image
                                src={skillIconUrl(skill.skillName)}
                                width={20}
                                height={20}
                                alt=""
                                unoptimized
                              />
                              <span className="font-medium text-gray-900 dark:text-white">
                                {skill.skillName}
                              </span>
                            </div>
                          </td>
                          <td className="whitespace-nowrap px-3 py-3 text-right sm:px-4 tabular-nums text-gray-700 dark:text-gray-300">
                            {skill.level ?? '—'}
                          </td>
                          <td className="whitespace-nowrap px-3 py-3 text-right sm:px-4 tabular-nums text-gray-700 dark:text-gray-300">
                            {skill.xp != null ? skill.xp.toLocaleString() : '—'}
                          </td>
                          <td className="whitespace-nowrap px-3 py-3 text-right sm:px-4 tabular-nums text-gray-400 dark:text-gray-500">
                            {skill.rank != null ? skill.rank.toLocaleString() : '—'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </section>

            {/* Widgets: below the table on mobile, a sidebar on desktop */}
            <aside className="lg:col-span-1">
              {summary && <AccountDashboard summary={summary} />}
            </aside>
          </div>
        )}
      </div>
    </div>
  );
}
