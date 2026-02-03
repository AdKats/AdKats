/**
 * Calculate Levenshtein distance between two strings.
 * This is used for fuzzy name matching in commands.
 */
export function levenshteinDistance(a: string, b: string): number {
  if (a.length === 0) return b.length;
  if (b.length === 0) return a.length;

  const matrix: number[][] = [];

  // Initialize first column
  for (let i = 0; i <= b.length; i++) {
    matrix[i] = [i];
  }

  // Initialize first row
  for (let j = 0; j <= a.length; j++) {
    matrix[0]![j] = j;
  }

  // Fill in the rest of the matrix
  for (let i = 1; i <= b.length; i++) {
    for (let j = 1; j <= a.length; j++) {
      if (b.charAt(i - 1) === a.charAt(j - 1)) {
        matrix[i]![j] = matrix[i - 1]![j - 1]!;
      } else {
        matrix[i]![j] = Math.min(
          matrix[i - 1]![j - 1]! + 1, // substitution
          matrix[i]![j - 1]! + 1,     // insertion
          matrix[i - 1]![j]! + 1      // deletion
        );
      }
    }
  }

  return matrix[b.length]![a.length]!;
}

/**
 * Find the best match for a name in a list using Levenshtein distance.
 */
export function findBestMatch<T>(
  needle: string,
  haystack: T[],
  getName: (item: T) => string,
  maxDistance: number = 3
): { match: T; distance: number } | null {
  const lowerNeedle = needle.toLowerCase();
  let bestMatch: T | null = null;
  let bestDistance = maxDistance + 1;

  for (const item of haystack) {
    const name = getName(item).toLowerCase();
    const distance = levenshteinDistance(lowerNeedle, name);

    if (distance < bestDistance) {
      bestDistance = distance;
      bestMatch = item;
    }

    // Early exit on exact match
    if (distance === 0) {
      break;
    }
  }

  if (bestMatch === null || bestDistance > maxDistance) {
    return null;
  }

  return { match: bestMatch, distance: bestDistance };
}

/**
 * Find all matches within a distance threshold.
 */
export function findAllMatches<T>(
  needle: string,
  haystack: T[],
  getName: (item: T) => string,
  maxDistance: number = 3
): Array<{ match: T; distance: number }> {
  const lowerNeedle = needle.toLowerCase();
  const matches: Array<{ match: T; distance: number }> = [];

  for (const item of haystack) {
    const name = getName(item).toLowerCase();
    const distance = levenshteinDistance(lowerNeedle, name);

    if (distance <= maxDistance) {
      matches.push({ match: item, distance });
    }
  }

  // Sort by distance (best matches first)
  matches.sort((a, b) => a.distance - b.distance);

  return matches;
}

/**
 * Check if a string starts with another string (case-insensitive).
 */
export function startsWithIgnoreCase(str: string, prefix: string): boolean {
  return str.toLowerCase().startsWith(prefix.toLowerCase());
}

/**
 * Check if a string contains another string (case-insensitive).
 */
export function containsIgnoreCase(str: string, search: string): boolean {
  return str.toLowerCase().includes(search.toLowerCase());
}
