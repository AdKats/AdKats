import type { ARole } from './role.js';

/**
 * User model - represents an AdKats admin user.
 * Corresponds to adkats_users in the database.
 */
export interface AUser {
  userId: number;
  userName: string;
  userEmail: string | null;
  userPhone: string | null;
  userRole: number;

  // Runtime associations (not persisted)
  role: ARole | null;
  soldierIds: number[];
  soldierNames: string[];
}

/**
 * Create a new user instance.
 */
export function createUser(userName: string, roleId: number = 1): AUser {
  return {
    userId: 0,
    userName,
    userEmail: null,
    userPhone: null,
    userRole: roleId,
    role: null,
    soldierIds: [],
    soldierNames: [],
  };
}

/**
 * Database row representation.
 */
export interface UserDbRow {
  user_id: number;
  user_name: string;
  user_email: string | null;
  user_phone: string | null;
  user_role: number;
}

/**
 * Convert database row to AUser.
 */
export function userFromDbRow(row: UserDbRow): AUser {
  return {
    userId: row.user_id,
    userName: row.user_name,
    userEmail: row.user_email,
    userPhone: row.user_phone,
    userRole: row.user_role,
    role: null,
    soldierIds: [],
    soldierNames: [],
  };
}

/**
 * Convert AUser to database insert/update values.
 */
export function userToDbValues(user: AUser): Record<string, unknown> {
  return {
    user_name: user.userName,
    user_email: user.userEmail,
    user_phone: user.userPhone,
    user_role: user.userRole,
  };
}

/**
 * User-soldier link for database operations.
 */
export interface UserSoldierDbRow {
  user_id: number;
  player_id: number;
}
