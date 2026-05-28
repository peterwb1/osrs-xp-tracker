export const queryKeys = {
  accounts: () => ['accounts'] as const,
  skills: (accountId: string | number) =>
    ['accounts', accountId, 'skills'] as const,
  skillHistory: (accountId: string | number, skillId: string | number, days = 30) =>
    ['accounts', accountId, 'skills', skillId, 'history', days] as const,
};
