'use client';

interface Option<T> {
  label: string;
  value: T;
}

interface Props<T extends string | number> {
  options: Option<T>[];
  value: T;
  onChange: (value: T) => void;
  'aria-label'?: string;
}

/**
 * A small segmented toggle. Used for chart metric (XP/Rank) and time range.
 * Reuses the app's card/border tokens so it sits nicely in chart headers.
 */
export function Segmented<T extends string | number>({
  options,
  value,
  onChange,
  'aria-label': ariaLabel,
}: Props<T>) {
  return (
    <div
      role="group"
      aria-label={ariaLabel}
      className="inline-flex rounded-lg border border-gray-200 bg-white p-0.5 dark:border-gray-700 dark:bg-gray-800"
    >
      {options.map((o) => {
        const active = o.value === value;
        return (
          <button
            key={String(o.value)}
            type="button"
            aria-pressed={active}
            onClick={() => onChange(o.value)}
            className={`rounded-md px-3 py-1 text-xs font-medium transition-colors ${
              active
                ? 'bg-blue-600 text-white'
                : 'text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200'
            }`}
          >
            {o.label}
          </button>
        );
      })}
    </div>
  );
}
