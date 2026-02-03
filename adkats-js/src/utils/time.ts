/**
 * Time utilities for AdKats.
 */

/**
 * Format a duration in minutes to a human-readable string.
 */
export function formatDuration(minutes: number): string {
  if (minutes <= 0) {
    return '0 minutes';
  }

  if (minutes < 60) {
    return `${Math.ceil(minutes)} minute${minutes !== 1 ? 's' : ''}`;
  }

  const hours = minutes / 60;
  if (hours < 24) {
    const h = Math.floor(hours);
    const m = Math.round((hours - h) * 60);
    if (m === 0) {
      return `${h} hour${h !== 1 ? 's' : ''}`;
    }
    return `${h} hour${h !== 1 ? 's' : ''} ${m} minute${m !== 1 ? 's' : ''}`;
  }

  const days = hours / 24;
  if (days < 7) {
    const d = Math.floor(days);
    return `${d} day${d !== 1 ? 's' : ''}`;
  }

  const weeks = days / 7;
  if (weeks < 4) {
    const w = Math.floor(weeks);
    return `${w} week${w !== 1 ? 's' : ''}`;
  }

  const months = days / 30;
  if (months < 12) {
    const mo = Math.floor(months);
    return `${mo} month${mo !== 1 ? 's' : ''}`;
  }

  const years = days / 365;
  const y = Math.floor(years);
  return `${y} year${y !== 1 ? 's' : ''}`;
}

/**
 * Format a duration in seconds to a human-readable string.
 */
export function formatDurationSeconds(seconds: number): string {
  if (seconds < 60) {
    return `${Math.ceil(seconds)} second${seconds !== 1 ? 's' : ''}`;
  }
  return formatDuration(seconds / 60);
}

/**
 * Parse a duration string to minutes.
 * Supports formats like: 5m, 1h, 2d, 1w, 1mo, perm
 */
export function parseDuration(input: string): number | null {
  const trimmed = input.trim().toLowerCase();

  // Permanent ban
  if (trimmed === 'perm' || trimmed === 'permanent') {
    return null; // null indicates permanent
  }

  // Match number + unit
  const match = trimmed.match(/^(\d+)\s*([a-z]+)?$/);
  if (!match) {
    return undefined as unknown as number; // Invalid format
  }

  const value = parseInt(match[1]!, 10);
  const unit = match[2] || 'm'; // Default to minutes

  switch (unit) {
    case 's':
    case 'sec':
    case 'second':
    case 'seconds':
      return value / 60;

    case 'm':
    case 'min':
    case 'minute':
    case 'minutes':
      return value;

    case 'h':
    case 'hr':
    case 'hour':
    case 'hours':
      return value * 60;

    case 'd':
    case 'day':
    case 'days':
      return value * 60 * 24;

    case 'w':
    case 'wk':
    case 'week':
    case 'weeks':
      return value * 60 * 24 * 7;

    case 'mo':
    case 'month':
    case 'months':
      return value * 60 * 24 * 30;

    case 'y':
    case 'yr':
    case 'year':
    case 'years':
      return value * 60 * 24 * 365;

    default:
      return undefined as unknown as number; // Invalid unit
  }
}

/**
 * Format a date to ISO string without milliseconds.
 */
export function formatDate(date: Date): string {
  return date.toISOString().replace(/\.\d{3}Z$/, 'Z');
}

/**
 * Format a date relative to now (e.g., "2 hours ago").
 */
export function formatRelative(date: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMinutes = diffMs / 60000;

  if (diffMinutes < 0) {
    return 'in the future';
  }

  if (diffMinutes < 1) {
    return 'just now';
  }

  if (diffMinutes < 60) {
    const m = Math.floor(diffMinutes);
    return `${m} minute${m !== 1 ? 's' : ''} ago`;
  }

  const diffHours = diffMinutes / 60;
  if (diffHours < 24) {
    const h = Math.floor(diffHours);
    return `${h} hour${h !== 1 ? 's' : ''} ago`;
  }

  const diffDays = diffHours / 24;
  if (diffDays < 7) {
    const d = Math.floor(diffDays);
    return `${d} day${d !== 1 ? 's' : ''} ago`;
  }

  const diffWeeks = diffDays / 7;
  if (diffWeeks < 4) {
    const w = Math.floor(diffWeeks);
    return `${w} week${w !== 1 ? 's' : ''} ago`;
  }

  // For older dates, just show the date
  return date.toLocaleDateString();
}

/**
 * Add minutes to a date.
 */
export function addMinutes(date: Date, minutes: number): Date {
  return new Date(date.getTime() + minutes * 60000);
}

/**
 * Check if a date is in the past.
 */
export function isPast(date: Date): boolean {
  return date.getTime() < Date.now();
}

/**
 * Check if a date is in the future.
 */
export function isFuture(date: Date): boolean {
  return date.getTime() > Date.now();
}
