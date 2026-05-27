'use client';

import { useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { ThemeToggle } from '@/components/ThemeToggle';

interface SkillSnapshot {
  skillId: number;
  skillName: string;
  displayOrder: number;
  xp: number | null;
  level: number | null;
  rank: number | null;
}

export default function AccountDetailPage() {
  const { isAuthenticated } = useAuth();
  const router = useRouter();
  const { id } = useParams<{ id: string }>();

  useEffect(() => {
    if (!isAuthenticated) router.push('/login');
  }, [isAuthenticated, router]);

  const { data: skills, isLoading, isError } = useQuery<SkillSnapshot[]>({
    queryKey: ['skills', id],
    queryFn: () => api.get(`/api/accounts/${id}/skills`).then((r) => r.data),
    enabled: isAuthenticated && !!id,
  });

  if (!isAuthenticated) return null;

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      <div className="mx-auto max-w-3xl p-6">
        <div className="mb-6 flex items-center justify-between">
          <Link
            href="/accounts"
            className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
          >
            ← Accounts
          </Link>
          <ThemeToggle />
        </div>

        <h1 className="mb-6 text-2xl font-bold text-gray-900 dark:text-white">Skills</h1>

        {isLoading && <p className="text-gray-500 dark:text-gray-400">Loading…</p>}
        {isError && (
          <p className="text-red-600 dark:text-red-400">
            Failed to load skills. This account may not exist.
          </p>
        )}

        {skills && (
          <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-700 dark:bg-gray-800">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500 dark:bg-gray-700 dark:text-gray-400">
                <tr>
                  <th className="px-4 py-3">Skill</th>
                  <th className="px-4 py-3 text-right">Level</th>
                  <th className="px-4 py-3 text-right">XP</th>
                  <th className="px-4 py-3 text-right">Rank</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
                {skills.map((skill) => (
                  <tr key={skill.skillId} className="hover:bg-gray-50 dark:hover:bg-gray-700">
                    <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">{skill.skillName}</td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-700 dark:text-gray-300">
                      {skill.level ?? '—'}
                    </td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-700 dark:text-gray-300">
                      {skill.xp != null ? skill.xp.toLocaleString() : '—'}
                    </td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-400 dark:text-gray-500">
                      {skill.rank != null ? skill.rank.toLocaleString() : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
