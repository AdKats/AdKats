import type { Logger } from '../core/logger.js';
import type { Scheduler } from '../core/scheduler.js';
import type { AdKatsConfig } from '../core/config.js';
import type { BattleConAdapter } from '../core/battlecon-adapter.js';
import type { PlayerService } from './player.service.js';
import type { BanService } from './ban.service.js';
import type { AdKatsEventBus } from '../core/event-bus.js';
import type { APlayer } from '../models/player.js';
import type { BattlelogClient, BattlelogPlayerStats, BattlelogWeaponStats } from '../integrations/battlelog.js';

/**
 * Anti-cheat check result.
 */
export interface AntiCheatCheckResult {
  passed: boolean;
  violation: AntiCheatViolation | null;
  checkType: AntiCheatCheckType;
  player: APlayer;
  stats: BattlelogPlayerStats | null;
}

/**
 * Anti-cheat violation.
 */
export interface AntiCheatViolation {
  type: AntiCheatViolationType;
  weapon: BattlelogWeaponStats;
  value: number;
  threshold: number;
  message: string;
  code: string;
}

/**
 * Types of anti-cheat checks.
 */
export type AntiCheatCheckType = 'DPS' | 'HSK' | 'KPM' | 'ALL';

/**
 * Types of anti-cheat violations.
 */
export type AntiCheatViolationType = 'DAMAGE_MOD' | 'AIMBOT' | 'HIGH_KPM';

/**
 * Weapon damage data from stat library.
 */
export interface WeaponDamageData {
  id: string;
  damage_max: number;
  damage_min: number;
}

/**
 * Weapon stat library.
 */
export interface WeaponStatLibrary {
  BF3: WeaponDamageData[];
  BF4: WeaponDamageData[];
  BFHL: WeaponDamageData[];
  BFBC2: WeaponDamageData[];
}

/**
 * Anti-cheat configuration.
 */
export interface AntiCheatConfig {
  enableDpsChecker: boolean;
  enableHskChecker: boolean;
  enableKpmChecker: boolean;
  dpsBanMessage: string;
  hskBanMessage: string;
  kpmBanMessage: string;
  minKillsForDps: number;
  minKillsForHsk: number;
  minKillsForKpm: number;
  hskTriggerLevel: number;  // Percentage (e.g., 70 for 70%)
  kpmTriggerLevel: number;  // KPM threshold (e.g., 5.0)
  soldierHealth: number;    // Server soldier health (default 100)
  autoBanEnabled: boolean;
  checkIntervalMs: number;
}

/**
 * Default anti-cheat configuration.
 */
const DEFAULT_ANTICHEAT_CONFIG: AntiCheatConfig = {
  enableDpsChecker: true,
  enableHskChecker: true,
  enableKpmChecker: true,
  dpsBanMessage: 'Damage Mod Detected',
  hskBanMessage: 'Aimbot Detected',
  kpmBanMessage: 'Suspicious KPM Detected',
  minKillsForDps: 50,
  minKillsForHsk: 100,
  minKillsForKpm: 200,
  hskTriggerLevel: 70,
  kpmTriggerLevel: 5.0,
  soldierHealth: 100,
  autoBanEnabled: true,
  checkIntervalMs: 30000,
};

/**
 * Weapon categories allowed for DPS checking per game.
 */
const DPS_ALLOWED_CATEGORIES: Record<string, string[]> = {
  BF3: ['sub_machine_guns', 'assault_rifles', 'carbines', 'machine_guns', 'handheld_weapons'],
  BF4: ['pdws', 'assault_rifles', 'carbines', 'lmgs', 'handguns'],
  BFHL: ['assault_rifles', 'ar_standard', 'handguns', 'pistols', 'machine_pistols', 'revolvers', 'smg_mechanic', 'smg'],
};

/**
 * Weapon categories allowed for HSK checking per game.
 */
const HSK_ALLOWED_CATEGORIES: Record<string, string[]> = {
  BF3: ['sub_machine_guns', 'assault_rifles', 'carbines', 'machine_guns'],
  BF4: ['pdws', 'assault_rifles', 'carbines', 'lmgs'],
  BFHL: ['assault_rifles', 'ar_standard', 'machine_pistols', 'smg_mechanic', 'smg'],
};

/**
 * Weapon categories allowed for KPM checking per game.
 */
const KPM_ALLOWED_CATEGORIES: Record<string, string[]> = {
  BF3: ['assault_rifles', 'carbines', 'sub_machine_guns', 'machine_guns'],
  BF4: ['assault_rifles', 'carbines', 'dmrs', 'lmgs', 'sniper_rifles', 'pdws', 'shotguns'],
  BFHL: ['assault_rifles', 'ar_standard', 'sr_standard', 'br_standard', 'shotguns', 'smg_mechanic', 'sg_enforcer', 'smg'],
};

/**
 * Sidearm categories for DPS adjustment.
 */
const SIDEARM_CATEGORIES = [
  'handheld_weapons',
  'handguns',
  'pistols',
  'machine_pistols',
  'revolvers',
];

/**
 * Anti-cheat service.
 * Monitors players for suspicious statistics and takes action.
 */
export class AntiCheatService {
  private logger: Logger;
  private scheduler: Scheduler;
  private config: AdKatsConfig;
  private bcAdapter: BattleConAdapter;
  private playerService: PlayerService;
  private banService: BanService;
  private eventBus: AdKatsEventBus;
  private battlelogClient: BattlelogClient;
  private acConfig: AntiCheatConfig;
  private weaponLibrary: Map<string, WeaponDamageData>;
  private gameVersion: string;
  private serverId: number;

  // Queue of players to check
  private checkQueue: Map<string, APlayer> = new Map();

  // Set of already checked players (by GUID)
  private checkedPlayers: Set<string> = new Set();

  // Set of whitelisted player GUIDs
  private whitelist: Set<string> = new Set();

  // Job ID for scheduled check
  private readonly CHECK_JOB_ID = 'anticheat-check';

  constructor(
    logger: Logger,
    scheduler: Scheduler,
    config: AdKatsConfig,
    bcAdapter: BattleConAdapter,
    playerService: PlayerService,
    banService: BanService,
    eventBus: AdKatsEventBus,
    battlelogClient: BattlelogClient,
    weaponLibrary: WeaponStatLibrary,
    gameVersion: string,
    serverId: number,
    acConfig: Partial<AntiCheatConfig> = {}
  ) {
    this.logger = logger;
    this.scheduler = scheduler;
    this.config = config;
    this.bcAdapter = bcAdapter;
    this.playerService = playerService;
    this.banService = banService;
    this.eventBus = eventBus;
    this.battlelogClient = battlelogClient;
    this.gameVersion = gameVersion;
    this.serverId = serverId;
    this.acConfig = { ...DEFAULT_ANTICHEAT_CONFIG, ...acConfig };

    // Build weapon lookup map
    this.weaponLibrary = this.buildWeaponLibrary(weaponLibrary);
  }

  /**
   * Initialize the anti-cheat service.
   */
  initialize(): void {
    if (!this.config.enableAntiCheat) {
      this.logger.info('Anti-cheat is disabled in configuration');
      return;
    }

    // Listen for player spawn events to queue checks
    this.eventBus.onEvent('player:spawn', (player: APlayer) => {
      this.queuePlayerForCheck(player);
    });

    // Listen for player join events
    this.eventBus.onEvent('player:join', (player: APlayer) => {
      this.queuePlayerForCheck(player);
    });

    // Listen for player leave to clean up
    this.eventBus.onEvent('player:leave', (player: APlayer) => {
      this.checkQueue.delete(player.soldierName);
    });

    // Listen for round start to reset checked players
    this.eventBus.onEvent('server:levelLoaded', () => {
      this.handleRoundStart();
    });

    // Register scheduled job
    this.scheduler.registerIntervalJob(
      this.CHECK_JOB_ID,
      'Anti-Cheat Check',
      this.acConfig.checkIntervalMs,
      () => this.processCheckQueue()
    );

    this.logger.info({
      dpsEnabled: this.acConfig.enableDpsChecker,
      hskEnabled: this.acConfig.enableHskChecker,
      kpmEnabled: this.acConfig.enableKpmChecker,
      autoBan: this.acConfig.autoBanEnabled,
    }, 'Anti-cheat service initialized');
  }

  /**
   * Queue a player for anti-cheat checking.
   */
  queuePlayerForCheck(player: APlayer): void {
    // Skip if already checked this round
    if (this.checkedPlayers.has(player.guid)) {
      return;
    }

    // Skip if whitelisted
    if (this.isWhitelisted(player)) {
      this.logger.debug({ player: player.soldierName }, 'Player is whitelisted, skipping anti-cheat');
      return;
    }

    // Add to queue
    this.checkQueue.set(player.soldierName, player);
    this.logger.debug({ player: player.soldierName }, 'Player queued for anti-cheat check');
  }

  /**
   * Process the check queue.
   */
  async processCheckQueue(): Promise<void> {
    if (this.checkQueue.size === 0) {
      return;
    }

    // Get next player from queue
    const [playerName, player] = this.checkQueue.entries().next().value as [string, APlayer];
    this.checkQueue.delete(playerName);

    // Verify player is still online
    if (!player.isOnline) {
      return;
    }

    try {
      // Fetch Battlelog stats
      const stats = await this.battlelogClient.getPlayerStats(player.soldierName);

      if (!stats) {
        this.logger.debug({ player: player.soldierName }, 'Could not fetch Battlelog stats');
        // Re-queue for later check if player is still online
        if (player.isOnline) {
          this.checkQueue.set(playerName, player);
        }
        return;
      }

      // Mark as checked
      this.checkedPlayers.add(player.guid);

      // Run checks
      const result = await this.runAllChecks(player, stats);

      if (!result.passed && result.violation) {
        await this.handleViolation(player, result.violation, stats);
      } else {
        this.logger.debug({ player: player.soldierName }, 'Player passed anti-cheat checks');
      }

    } catch (error) {
      const msg = error instanceof Error ? error.message : String(error);
      this.logger.error({ player: player.soldierName, error: msg }, 'Error during anti-cheat check');
    }
  }

  /**
   * Run all enabled anti-cheat checks on a player.
   */
  async runAllChecks(player: APlayer, stats: BattlelogPlayerStats): Promise<AntiCheatCheckResult> {
    // Run HSK check first (most serious)
    if (this.acConfig.enableHskChecker) {
      const hskResult = this.checkHsk(player, stats);
      if (!hskResult.passed) {
        return hskResult;
      }
    }

    // Run DPS check
    if (this.acConfig.enableDpsChecker) {
      const dpsResult = this.checkDps(player, stats);
      if (!dpsResult.passed) {
        return dpsResult;
      }
    }

    // Run KPM check
    if (this.acConfig.enableKpmChecker) {
      const kpmResult = this.checkKpm(player, stats);
      if (!kpmResult.passed) {
        return kpmResult;
      }
    }

    return {
      passed: true,
      violation: null,
      checkType: 'ALL',
      player,
      stats,
    };
  }

  /**
   * Check for DPS (Damage Per Shot) violations indicating damage mods.
   */
  checkDps(player: APlayer, stats: BattlelogPlayerStats): AntiCheatCheckResult {
    const allowedCategories = DPS_ALLOWED_CATEGORIES[this.gameVersion] ?? [];

    // Sort weapons by kills descending
    const topWeapons = [...stats.weaponStats]
      .filter(w => allowedCategories.includes(w.category.toLowerCase()))
      .sort((a, b) => b.kills - a.kills);

    let worstViolation: AntiCheatViolation | null = null;
    let worstPercentage = -1;

    for (const weapon of topWeapons) {
      // Skip weapons with insufficient kills
      if (weapon.kills < this.acConfig.minKillsForDps) {
        continue;
      }

      // Get weapon damage data
      const weaponData = this.getWeaponDamage(weapon.weaponId);
      if (!weaponData || weaponData.damage_max >= 50) {
        // Skip high-damage weapons (snipers, etc.)
        continue;
      }

      // Skip if HSK checker is enabled and this weapon has high HSKR
      // (HSK checker handles aimbot, DPS handles damage mods)
      if (this.acConfig.enableHskChecker && weapon.hskr > (this.acConfig.hskTriggerLevel / 100)) {
        continue;
      }

      // Check for damage mod
      if (weapon.dps > weaponData.damage_max) {
        const isSidearm = SIDEARM_CATEGORIES.includes(weapon.category.toLowerCase());

        // Account for headshots in expected damage
        const expectedDmg = weaponData.damage_max * (1 + weapon.hskr);

        // Get the percentage over normal
        const percDiff = (weapon.dps - expectedDmg) / expectedDmg;

        // Trigger level varies by kill count and weapon type
        let triggerLevel = this.acConfig.soldierHealth > 65 ? 0.50 : 0.60;

        // Increase trigger level for low kill counts
        if (weapon.kills < 100) {
          triggerLevel *= 1.8;
        }

        // Increase trigger level for sidearms
        if (isSidearm) {
          triggerLevel *= 1.5;
        }

        if (percDiff > triggerLevel && percDiff > worstPercentage) {
          worstPercentage = percDiff;
          const formattedName = weapon.weaponId.replace(/-/g, '').replace(/ /g, '').toUpperCase();

          worstViolation = {
            type: 'DAMAGE_MOD',
            weapon,
            value: weapon.dps,
            threshold: expectedDmg * (1 + triggerLevel),
            message: this.acConfig.dpsBanMessage,
            code: `[4-${formattedName}-${Math.round(weapon.dps)}-${weapon.kills}-${weapon.headshots}-${weapon.hits}]`,
          };
        }
      }
    }

    return {
      passed: worstViolation === null,
      violation: worstViolation,
      checkType: 'DPS',
      player,
      stats,
    };
  }

  /**
   * Check for HSK (Headshot Kill Ratio) violations indicating aimbot.
   */
  checkHsk(player: APlayer, stats: BattlelogPlayerStats): AntiCheatCheckResult {
    const allowedCategories = HSK_ALLOWED_CATEGORIES[this.gameVersion] ?? [];
    const hskTrigger = this.acConfig.hskTriggerLevel / 100;

    // Sort weapons by kills descending
    const topWeapons = [...stats.weaponStats]
      .filter(w => allowedCategories.includes(w.category.toLowerCase()))
      .sort((a, b) => b.kills - a.kills);

    let worstViolation: AntiCheatViolation | null = null;
    let worstHskr = -1;

    for (const weapon of topWeapons) {
      // Skip weapons with insufficient kills
      if (weapon.kills < this.acConfig.minKillsForHsk) {
        continue;
      }

      // Get weapon damage data
      const weaponData = this.getWeaponDamage(weapon.weaponId);
      if (!weaponData || weaponData.damage_max >= 50) {
        // Skip high-damage weapons
        continue;
      }

      // Check HSKR
      this.logger.trace({
        weapon: weapon.weaponId,
        hskr: weapon.hskr,
        trigger: hskTrigger,
      }, 'Checking HSK');

      if (weapon.hskr > hskTrigger && weapon.hskr > worstHskr) {
        worstHskr = weapon.hskr;
        const formattedName = weapon.weaponId.replace(/-/g, '').replace(/ /g, '').toUpperCase();

        worstViolation = {
          type: 'AIMBOT',
          weapon,
          value: weapon.hskr * 100,
          threshold: this.acConfig.hskTriggerLevel,
          message: this.acConfig.hskBanMessage,
          code: `[6-${formattedName}-${Math.round(weapon.hskr * 100)}-${weapon.kills}-${weapon.headshots}-${weapon.hits}]`,
        };
      }
    }

    return {
      passed: worstViolation === null,
      violation: worstViolation,
      checkType: 'HSK',
      player,
      stats,
    };
  }

  /**
   * Check for KPM (Kills Per Minute) violations.
   */
  checkKpm(player: APlayer, stats: BattlelogPlayerStats): AntiCheatCheckResult {
    const allowedCategories = KPM_ALLOWED_CATEGORIES[this.gameVersion] ?? [];

    // Sort weapons by kills descending
    const topWeapons = [...stats.weaponStats]
      .filter(w => allowedCategories.includes(w.category.toLowerCase()))
      .filter(w => w.categorySid !== 'WARSAW_ID_P_CAT_GADGET' && w.categorySid !== 'WARSAW_ID_P_CAT_SIDEARM')
      .sort((a, b) => b.kills - a.kills);

    let worstViolation: AntiCheatViolation | null = null;
    let worstKpm = -1;

    for (const weapon of topWeapons) {
      // Skip weapons with insufficient kills
      if (weapon.kills < this.acConfig.minKillsForKpm) {
        continue;
      }

      // Check KPM
      this.logger.trace({
        weapon: weapon.weaponId,
        kpm: weapon.kpm,
        trigger: this.acConfig.kpmTriggerLevel,
      }, 'Checking KPM');

      if (weapon.kpm > this.acConfig.kpmTriggerLevel && weapon.kpm > worstKpm) {
        worstKpm = weapon.kpm;
        const formattedName = weapon.weaponId.replace(/-/g, '').replace(/ /g, '').toUpperCase();

        worstViolation = {
          type: 'HIGH_KPM',
          weapon,
          value: weapon.kpm,
          threshold: this.acConfig.kpmTriggerLevel,
          message: this.acConfig.kpmBanMessage,
          code: `[5-${formattedName}-${weapon.kpm.toFixed(2)}-${weapon.kills}-${weapon.headshots}-${weapon.hits}]`,
        };
      }
    }

    return {
      passed: worstViolation === null,
      violation: worstViolation,
      checkType: 'KPM',
      player,
      stats,
    };
  }

  /**
   * Handle a detected violation.
   */
  private async handleViolation(
    player: APlayer,
    violation: AntiCheatViolation,
    stats: BattlelogPlayerStats
  ): Promise<void> {
    this.logger.warn({
      player: player.soldierName,
      type: violation.type,
      weapon: violation.weapon.weaponId,
      value: violation.value,
      threshold: violation.threshold,
      code: violation.code,
    }, `Anti-cheat violation detected: ${violation.type}`);

    // Log the violation
    const logMessage = `${player.soldierName} flagged for ${violation.type}: ${violation.weapon.weaponId} ` +
      `(${violation.value.toFixed(2)} > ${violation.threshold.toFixed(2)}) ${violation.code}`;

    this.logger.info(logMessage);

    // Auto-ban if enabled
    if (this.acConfig.autoBanEnabled) {
      try {
        const banReason = `${violation.message} ${violation.code}`;

        await this.banService.banPlayer(
          player,
          null, // Automated source
          null, // Permanent ban
          banReason,
          {
            enforceGuid: true,
            enforceIp: false,
            enforceName: false,
          }
        );

        this.logger.info({
          player: player.soldierName,
          reason: banReason,
        }, 'Player auto-banned by anti-cheat');

        // Notify admins via adapter
        try {
          await this.bcAdapter.say(
            `[AdKats] ${player.soldierName} has been banned for ${violation.type}`
          );
        } catch {
          // Ignore messaging errors
        }

      } catch (error) {
        const msg = error instanceof Error ? error.message : String(error);
        this.logger.error({ player: player.soldierName, error: msg }, 'Failed to auto-ban player');
      }
    } else {
      // Just notify admins
      try {
        await this.bcAdapter.say(
          `[AdKats] ALERT: ${player.soldierName} flagged for ${violation.type}`
        );
      } catch {
        // Ignore messaging errors
      }
    }
  }

  /**
   * Handle round start - reset checked players.
   */
  private handleRoundStart(): void {
    this.checkedPlayers.clear();
    this.checkQueue.clear();
    this.logger.debug('Anti-cheat state reset for new round');
  }

  /**
   * Check if a player is whitelisted.
   */
  isWhitelisted(player: APlayer): boolean {
    return this.whitelist.has(player.guid);
  }

  /**
   * Add a player to the whitelist.
   */
  addToWhitelist(guid: string): void {
    this.whitelist.add(guid);
    this.logger.info({ guid }, 'Added to anti-cheat whitelist');
  }

  /**
   * Remove a player from the whitelist.
   */
  removeFromWhitelist(guid: string): void {
    this.whitelist.delete(guid);
    this.logger.info({ guid }, 'Removed from anti-cheat whitelist');
  }

  /**
   * Load whitelist from a set of GUIDs.
   */
  loadWhitelist(guids: Set<string> | string[]): void {
    this.whitelist = new Set(guids);
    this.logger.info({ count: this.whitelist.size }, 'Anti-cheat whitelist loaded');
  }

  /**
   * Get weapon damage data from the library.
   */
  private getWeaponDamage(weaponId: string): WeaponDamageData | null {
    // Try exact match first
    let data = this.weaponLibrary.get(weaponId.toLowerCase());
    if (data) {
      return data;
    }

    // Try normalized match (remove hyphens, spaces)
    const normalized = weaponId.toLowerCase().replace(/-/g, '').replace(/ /g, '');
    for (const [key, value] of this.weaponLibrary) {
      if (key.replace(/-/g, '').replace(/ /g, '') === normalized) {
        return value;
      }
    }

    return null;
  }

  /**
   * Build weapon lookup map from stat library.
   */
  private buildWeaponLibrary(library: WeaponStatLibrary): Map<string, WeaponDamageData> {
    const map = new Map<string, WeaponDamageData>();
    const gameWeapons = library[this.gameVersion as keyof WeaponStatLibrary] ?? [];

    for (const weapon of gameWeapons) {
      map.set(weapon.id.toLowerCase(), weapon);
    }

    this.logger.debug({ count: map.size, game: this.gameVersion }, 'Weapon library loaded');
    return map;
  }

  /**
   * Manually check a player (for admin commands).
   */
  async manualCheck(player: APlayer): Promise<AntiCheatCheckResult> {
    const stats = await this.battlelogClient.getPlayerStats(player.soldierName);

    if (!stats) {
      return {
        passed: true, // No stats means we can't detect anything
        violation: null,
        checkType: 'ALL',
        player,
        stats: null,
      };
    }

    return this.runAllChecks(player, stats);
  }

  /**
   * Get current queue size.
   */
  getQueueSize(): number {
    return this.checkQueue.size;
  }

  /**
   * Get number of checked players this round.
   */
  getCheckedCount(): number {
    return this.checkedPlayers.size;
  }

  /**
   * Get whitelist size.
   */
  getWhitelistSize(): number {
    return this.whitelist.size;
  }

  /**
   * Enable the anti-cheat service.
   */
  enable(): void {
    this.scheduler.enableJob(this.CHECK_JOB_ID);
    this.logger.info('Anti-cheat enabled');
  }

  /**
   * Disable the anti-cheat service.
   */
  disable(): void {
    this.scheduler.disableJob(this.CHECK_JOB_ID);
    this.logger.info('Anti-cheat disabled');
  }

  /**
   * Update anti-cheat configuration.
   */
  updateConfig(config: Partial<AntiCheatConfig>): void {
    this.acConfig = { ...this.acConfig, ...config };
    this.logger.info({ config: this.acConfig }, 'Anti-cheat configuration updated');
  }

  /**
   * Get current configuration.
   */
  getConfig(): AntiCheatConfig {
    return { ...this.acConfig };
  }
}

/**
 * Create a new anti-cheat service.
 */
export function createAntiCheatService(
  logger: Logger,
  scheduler: Scheduler,
  config: AdKatsConfig,
  bcAdapter: BattleConAdapter,
  playerService: PlayerService,
  banService: BanService,
  eventBus: AdKatsEventBus,
  battlelogClient: BattlelogClient,
  weaponLibrary: WeaponStatLibrary,
  gameVersion: string,
  serverId: number,
  acConfig?: Partial<AntiCheatConfig>
): AntiCheatService {
  return new AntiCheatService(
    logger,
    scheduler,
    config,
    bcAdapter,
    playerService,
    banService,
    eventBus,
    battlelogClient,
    weaponLibrary,
    gameVersion,
    serverId,
    acConfig
  );
}
