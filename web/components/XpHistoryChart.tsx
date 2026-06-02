'use client';

import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
} from 'recharts';

export interface HistoryPoint {
  capturedAt: string;
  xp: number;
  level: number;
  rank: number;
}

export type Metric = 'xp' | 'rank';

export interface SeriesPoint {
  t: number; // epoch ms
  value: number;
}

/**
 * Turns raw history into a plottable series for the chosen metric.
 *
 * For rank we drop OSRS's `-1` sentinel (unranked: the account is below the
 * hiscores cutoff for this skill), otherwise it would wreck the axis.
 *
 * Exposed so callers can check `length` to decide between a chart and an
 * empty state without duplicating the transform.
 */
export function metricSeries(history: HistoryPoint[], metric: Metric): SeriesPoint[] {
  return history
    .map((h) => ({ t: new Date(h.capturedAt).getTime(), value: metric === 'rank' ? h.rank : h.xp }))
    .filter((p) => (metric === 'rank' ? p.value >= 0 : true));
}

/** Format a Y tick. Decimals adapt to the visible range so small gains aren't hidden. */
function xpTickFormatter(range: number) {
  return (v: number) => {
    if (v >= 1_000_000) {
      const decimals = range < 200_000 ? 3 : range < 2_000_000 ? 2 : 1;
      return (v / 1_000_000).toFixed(decimals) + 'M';
    }
    if (v >= 1_000) {
      return (v / 1_000).toFixed(range < 2_000 ? 2 : 0) + 'k';
    }
    return Math.round(v).toLocaleString('en-GB');
  };
}

interface Props {
  data: SeriesPoint[];
  metric: Metric;
}

/**
 * Reusable line chart for a single account/skill time-series.
 *
 * - Time-based X-axis so unevenly-spaced points (e.g. after a manual refresh)
 *   sit at their true position rather than being bucketed by day.
 * - Y-axis fits the data range (`dataMin`/`dataMax` + padding) so a small gain
 *   on a large total is still visible, instead of looking flat against a
 *   zero-based scale.
 * - For rank the Y-axis is reversed (lower rank = better = higher on the chart).
 */
export function XpHistoryChart({ data, metric }: Props) {
  const isRank = metric === 'rank';

  const values = data.map((p) => p.value);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const spread = max - min;
  // Pad so the line isn't glued to the chart edges; never zero (flat series).
  const pad = spread === 0 ? Math.max(1, Math.abs(max) * 0.01) : spread * 0.1;
  const yDomain: [number, number] = [Math.max(0, min - pad), max + pad];

  const formatValue = (v: number) =>
    isRank ? v.toLocaleString('en-GB') : Math.round(v).toLocaleString('en-GB');

  return (
    <ResponsiveContainer width="100%" height={320}>
      <LineChart data={data} margin={{ top: 4, right: 8, bottom: 4, left: 8 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
        <XAxis
          dataKey="t"
          type="number"
          scale="time"
          domain={['dataMin', 'dataMax']}
          tickFormatter={(t: number) =>
            new Date(t).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
          }
          tick={{ fontSize: 11, fill: '#9ca3af' }}
          stroke="#9ca3af"
          minTickGap={24}
        />
        <YAxis
          domain={yDomain}
          reversed={isRank}
          allowDecimals={false}
          tickFormatter={isRank ? (v: number) => v.toLocaleString('en-GB') : xpTickFormatter(spread)}
          tick={{ fontSize: 11, fill: '#9ca3af' }}
          stroke="#9ca3af"
          width={isRank ? 72 : 60}
        />
        <Tooltip
          labelFormatter={(t) =>
            new Date(Number(t)).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' })
          }
          formatter={(v) => [
            typeof v === 'number' ? formatValue(v) : v,
            isRank ? 'Rank' : 'XP',
          ]}
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
          dataKey="value"
          stroke="#6366f1"
          strokeWidth={2}
          dot={false}
          activeDot={{ r: 4 }}
        />
      </LineChart>
    </ResponsiveContainer>
  );
}
