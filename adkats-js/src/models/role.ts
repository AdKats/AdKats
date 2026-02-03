import type { ACommand } from './command.js';

/**
 * Role model - represents an AdKats permission role.
 * Corresponds to adkats_roles in the database.
 */
export interface ARole {
  roleId: number;
  roleKey: string;
  roleName: string;

  // Runtime associations (not persisted)
  allowedCommands: Set<number>;
}

/**
 * Built-in role keys.
 */
export const RoleKeys = {
  GUEST: 'guest_default',
  ADMIN: 'admin_full',
} as const;

/**
 * Create a new role instance.
 */
export function createRole(roleKey: string, roleName: string): ARole {
  return {
    roleId: 0,
    roleKey,
    roleName,
    allowedCommands: new Set(),
  };
}

/**
 * Check if a role can use a command.
 */
export function roleCanUseCommand(role: ARole, command: ACommand): boolean {
  return role.allowedCommands.has(command.commandId);
}

/**
 * Check if a role can use a command by ID.
 */
export function roleCanUseCommandId(role: ARole, commandId: number): boolean {
  return role.allowedCommands.has(commandId);
}

/**
 * Database row representation.
 */
export interface RoleDbRow {
  role_id: number;
  role_key: string;
  role_name: string;
}

/**
 * Convert database row to ARole.
 */
export function roleFromDbRow(row: RoleDbRow): ARole {
  return {
    roleId: row.role_id,
    roleKey: row.role_key,
    roleName: row.role_name,
    allowedCommands: new Set(),
  };
}

/**
 * Convert ARole to database insert/update values.
 */
export function roleToDbValues(role: ARole): Record<string, unknown> {
  return {
    role_key: role.roleKey,
    role_name: role.roleName,
  };
}

/**
 * Role-command link for database operations.
 */
export interface RoleCommandDbRow {
  role_id: number;
  command_id: number;
}
