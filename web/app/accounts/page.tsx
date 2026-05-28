'use client';

import { useEffect, useState, FormEvent } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow } from 'date-fns';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import { queryKeys } from '@/lib/queryKeys';
import { ThemeToggle } from '@/components/ThemeToggle';
import { Spinner } from '@/components/Spinner';

interface Account {
  id: number;
  osrsUsername: string;
  displayName: string;
  createdAt: string;
  lastPolledAt: string | null;
}

function relativeTime(dateStr: string | null): string {
  if (!dateStr) return 'Never';
  return formatDistanceToNow(new Date(dateStr), { addSuffix: true });
}

export default function AccountsPage() {
  const { isAuthenticated, logout } = useAuth();
  const router = useRouter();
  const queryClient = useQueryClient();

  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [addError, setAddError] = useState('');

  useEffect(() => {
    if (!isAuthenticated) router.push('/login');
  }, [isAuthenticated, router]);

  const { data: accounts, isLoading } = useQuery<Account[]>({
    queryKey: queryKeys.accounts(),
    queryFn: () => api.get('/api/accounts').then((r) => r.data),
    enabled: isAuthenticated,
  });

  const addMutation = useMutation({
    mutationFn: (body: { osrsUsername: string; displayName: string }) =>
      api.post('/api/accounts', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.accounts() });
      setUsername('');
      setDisplayName('');
      setAddError('');
    },
    onError: (err: unknown) => {
      const msg =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
      setAddError(msg ?? 'Failed to add account.');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => api.delete(`/api/accounts/${id}`),
    onSuccess: (_data, id) => {
      // Prefix-invalidate: clears accounts list + all cached data for this account
      queryClient.invalidateQueries({ queryKey: queryKeys.accounts() });
      queryClient.removeQueries({ queryKey: ['accounts', id] });
    },
  });

  async function handleAdd(e: FormEvent) {
    e.preventDefault();
    setAddError('');
    addMutation.mutate({ osrsUsername: username, displayName: displayName || username });
  }

  if (!isAuthenticated) return null;

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      <div className="mx-auto max-w-2xl p-6">
        <div className="mb-6 flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Tracked Accounts</h1>
          <div className="flex items-center gap-2">
            <ThemeToggle />
            <button
              onClick={() => { logout(); router.push('/login'); }}
              className="text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
            >
              Sign out
            </button>
          </div>
        </div>

        {/* Add account form */}
        <form
          onSubmit={handleAdd}
          className="mb-8 rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-700 dark:bg-gray-800"
        >
          <h2 className="mb-4 font-semibold text-gray-900 dark:text-white">Add account</h2>
          <div className="flex gap-3">
            <input
              type="text"
              placeholder="OSRS username"
              required
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="flex-1 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white dark:placeholder-gray-400"
            />
            <input
              type="text"
              placeholder="Display name (optional)"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              className="flex-1 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white dark:placeholder-gray-400"
            />
            <button
              type="submit"
              disabled={addMutation.isPending}
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 dark:bg-blue-500 dark:hover:bg-blue-600"
            >
              {addMutation.isPending ? 'Adding…' : 'Add'}
            </button>
          </div>
          {addError && <p className="mt-2 text-sm text-red-600 dark:text-red-400">{addError}</p>}
        </form>

        {/* Account list */}
        {isLoading ? (
          <Spinner />
        ) : accounts?.length === 0 ? (
          <p className="text-gray-500 dark:text-gray-400">No accounts tracked yet. Add one above.</p>
        ) : (
          <ul className="space-y-3">
            {accounts?.map((account) => (
              <li
                key={account.id}
                className="flex items-center justify-between rounded-xl border border-gray-200 bg-white px-5 py-4 shadow-sm dark:border-gray-700 dark:bg-gray-800"
              >
                <div>
                  <Link
                    href={`/accounts/${account.id}`}
                    className="font-medium text-gray-900 hover:text-blue-600 dark:text-white dark:hover:text-blue-400"
                  >
                    {account.displayName}
                  </Link>
                  <p className="text-xs text-gray-400 dark:text-gray-500">
                    {account.osrsUsername} · Last polled: {relativeTime(account.lastPolledAt)}
                  </p>
                </div>
                <button
                  onClick={() => deleteMutation.mutate(account.id)}
                  disabled={deleteMutation.isPending}
                  className="text-sm text-red-500 hover:text-red-700 disabled:opacity-50 dark:text-red-400 dark:hover:text-red-300"
                >
                  Remove
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
