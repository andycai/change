import { readFileSync, existsSync } from 'fs';
import { join } from 'path';

export interface Config {
  aiThreshold: number;       // AI 调用阈值（CV 置信度低于此值时调用 AI）
  cvConfidenceMin: number;   // CV 最低置信度（低于此值标记 needsReview）
  enableAI: boolean;         // 是否启用 AI 识别
  claudeApiKey?: string;     // Claude API Key
  debug: boolean;            // 是否启用 debug 模式
}

export class ConfigLoader {
  private static defaultConfig: Config = {
    aiThreshold: 0.7,
    cvConfidenceMin: 0.6,
    enableAI: true,
    debug: false,
  };

  /**
   * 加载配置文件
   */
  static load(configPath?: string): Config {
    let loaded: Partial<Config> | null = null;

    // 如果指定了路径且文件存在，优先使用
    if (configPath && existsSync(configPath)) {
      loaded = this.tryLoadConfigFile(configPath);
    }

    // 未指定路径时，查找项目根目录
    if (!configPath) {
      loaded = this.tryLoadConfigFile(
        join(process.cwd(), 'psd-exporter.config.json')
      );
    }

    // 合并：默认值 < 文件值 < 校验
    const merged = { ...this.defaultConfig };
    if (loaded) {
      const valid = this.validateConfig(loaded);
      Object.assign(merged, valid);
    }

    // 环境变量回退：文件未设置 claudeApiKey 时使用环境变量
    if (!merged.claudeApiKey && process.env.CLAUDE_API_KEY) {
      merged.claudeApiKey = process.env.CLAUDE_API_KEY;
    }

    return merged;
  }

  /**
   * 尝试从文件路径加载并解析 JSON 配置
   * 成功返回 Partial<Config>，失败返回 null
   */
  private static tryLoadConfigFile(path: string): Partial<Config> | null {
    if (!existsSync(path)) {
      return null;
    }
    try {
      const content = readFileSync(path, 'utf-8');
      return JSON.parse(content) as Partial<Config>;
    } catch (error) {
      console.warn(`Failed to load config from ${path}:`, error);
      return null;
    }
  }

  /**
   * 校验配置值的类型和范围
   * 无效值会被静默移除，回退到默认值
   */
  private static validateConfig(raw: Partial<Config>): Partial<Config> {
    const valid: Partial<Config> = {};

    if (raw.aiThreshold !== undefined) {
      if (typeof raw.aiThreshold === 'number' && raw.aiThreshold >= 0 && raw.aiThreshold <= 1) {
        valid.aiThreshold = raw.aiThreshold;
      }
    }

    if (raw.cvConfidenceMin !== undefined) {
      if (typeof raw.cvConfidenceMin === 'number' && raw.cvConfidenceMin >= 0 && raw.cvConfidenceMin <= 1) {
        valid.cvConfidenceMin = raw.cvConfidenceMin;
      }
    }

    if (raw.enableAI !== undefined) {
      if (typeof raw.enableAI === 'boolean') {
        valid.enableAI = raw.enableAI;
      }
    }

    if (raw.debug !== undefined) {
      if (typeof raw.debug === 'boolean') {
        valid.debug = raw.debug;
      }
    }

    if (raw.claudeApiKey !== undefined) {
      if (typeof raw.claudeApiKey === 'string') {
        valid.claudeApiKey = raw.claudeApiKey;
      }
    }

    return valid;
  }
}
