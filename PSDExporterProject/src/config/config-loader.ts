import { readFileSync, existsSync } from 'fs';
import { join } from 'path';

export type Scale9Config = {
  unit: 'ratio' | 'pixels';
  left: number;
  top: number;
  right: number;
  bottom: number;
};

export interface FairyGuiFontMapping {
  default?: string;
  tmp?: string;
  ugui?: string;
}

export interface FairyGuiConfig {
  projectPath?: string;
  packageName: string;
  sourceRoot?: string;
  defaultScale9: Scale9Config;
  fontMappings: Record<string, FairyGuiFontMapping>;
  references: {
    local: Record<string, { resourceId: string }>;
    external: Record<string, { packageId: string; resourceId: string }>;
  };
  pages: Record<string, { pageId?: string; componentName?: string }>;
}

export interface Config {
  aiThreshold: number;       // AI 调用阈值（CV 置信度低于此值时调用 AI）
  cvConfidenceMin: number;   // CV 最低置信度（低于此值标记 needsReview）
  enableAI: boolean;         // 是否启用 AI 识别
  claudeApiKey?: string;     // Claude API Key
  debug: boolean;            // 是否启用 debug 模式
  fairyGui: FairyGuiConfig;
}

export class ConfigLoader {
  private static defaultConfig: Config = {
    aiThreshold: 0.7,
    cvConfidenceMin: 0.6,
    enableAI: true,
    debug: false,
    fairyGui: {
      packageName: 'PSDImport',
      defaultScale9: { unit: 'ratio', left: 0.3, top: 0.3, right: 0.3, bottom: 0.3 },
      fontMappings: {},
      references: { local: {}, external: {} },
      pages: {},
    },
  };

  /**
   * 加载配置文件
   */
  static load(configPath?: string): Config {
    let loaded: unknown = null;

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
      if (valid.fairyGui) {
        merged.fairyGui = valid.fairyGui;
      }
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
  private static tryLoadConfigFile(path: string): unknown | null {
    if (!existsSync(path)) {
      return null;
    }
    try {
      const content = readFileSync(path, 'utf-8');
      return JSON.parse(content) as unknown;
    } catch (error) {
      console.warn(`Failed to load config from ${path}:`, error);
      return null;
    }
  }

  /**
   * 校验配置值的类型和范围
   * 无效值会被静默移除，回退到默认值
   */
  private static validateConfig(input: unknown): Partial<Config> {
    const valid: Partial<Config> = {};
    if (!this.isRecord(input)) {
      return valid;
    }
    const raw = input;

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

    if (raw.fairyGui !== undefined && this.isRecord(raw.fairyGui)) {
      valid.fairyGui = this.validateFairyGuiConfig(raw.fairyGui);
    }

    return valid;
  }

  private static validateFairyGuiConfig(raw: Record<string, unknown>): FairyGuiConfig {
    const defaults = this.defaultConfig.fairyGui;
    const scale9 = this.validateScale9(raw.defaultScale9) ?? defaults.defaultScale9;
    const references = this.isRecord(raw.references) ? raw.references : {};

    return {
      ...(typeof raw.projectPath === 'string' ? { projectPath: raw.projectPath } : {}),
      packageName: typeof raw.packageName === 'string' && raw.packageName.trim()
        ? raw.packageName
        : defaults.packageName,
      ...(typeof raw.sourceRoot === 'string' ? { sourceRoot: raw.sourceRoot } : {}),
      defaultScale9: scale9,
      fontMappings: this.validateFontMappings(raw.fontMappings),
      references: {
        local: this.validateLocalReferences(references.local),
        external: this.validateExternalReferences(references.external),
      },
      pages: this.validatePages(raw.pages),
    };
  }

  private static validateScale9(value: unknown): Scale9Config | undefined {
    if (!this.isRecord(value)) return undefined;
    const unit = value.unit;
    const values = [value.left, value.top, value.right, value.bottom];
    if ((unit !== 'ratio' && unit !== 'pixels') || values.some(item => typeof item !== 'number' || item < 0)) {
      return undefined;
    }
    if (
      unit === 'ratio'
      && (
        values.some(item => (item as number) > 1)
        || (value.left as number) + (value.right as number) >= 1
        || (value.top as number) + (value.bottom as number) >= 1
      )
    ) {
      return undefined;
    }
    return {
      unit,
      left: value.left as number,
      top: value.top as number,
      right: value.right as number,
      bottom: value.bottom as number,
    };
  }

  private static validateFontMappings(value: unknown): FairyGuiConfig['fontMappings'] {
    if (!this.isRecord(value)) return {};
    const result: FairyGuiConfig['fontMappings'] = {};
    for (const [fontName, mapping] of Object.entries(value)) {
      if (!this.isRecord(mapping)) continue;
      const valid: FairyGuiFontMapping = {};
      if (typeof mapping.default === 'string') valid.default = mapping.default;
      if (typeof mapping.tmp === 'string') valid.tmp = mapping.tmp;
      if (typeof mapping.ugui === 'string') valid.ugui = mapping.ugui;
      result[fontName] = valid;
    }
    return result;
  }

  private static validateLocalReferences(value: unknown): FairyGuiConfig['references']['local'] {
    if (!this.isRecord(value)) return {};
    const result: FairyGuiConfig['references']['local'] = {};
    for (const [name, reference] of Object.entries(value)) {
      if (this.isRecord(reference) && typeof reference.resourceId === 'string' && reference.resourceId.trim()) {
        result[name] = { resourceId: reference.resourceId.trim() };
      }
    }
    return result;
  }

  private static validateExternalReferences(value: unknown): FairyGuiConfig['references']['external'] {
    if (!this.isRecord(value)) return {};
    const result: FairyGuiConfig['references']['external'] = {};
    for (const [name, reference] of Object.entries(value)) {
      if (
        this.isRecord(reference)
        && typeof reference.packageId === 'string'
        && reference.packageId.trim()
        && typeof reference.resourceId === 'string'
        && reference.resourceId.trim()
      ) {
        result[name] = { packageId: reference.packageId.trim(), resourceId: reference.resourceId.trim() };
      }
    }
    return result;
  }

  private static validatePages(value: unknown): FairyGuiConfig['pages'] {
    if (!this.isRecord(value)) return {};
    const result: FairyGuiConfig['pages'] = {};
    for (const [sourcePath, page] of Object.entries(value)) {
      if (!this.isRecord(page)) continue;
      result[sourcePath] = {
        ...(typeof page.pageId === 'string' && page.pageId.trim() ? { pageId: page.pageId.trim() } : {}),
        ...(typeof page.componentName === 'string' && page.componentName.trim() ? { componentName: page.componentName.trim() } : {}),
      };
    }
    return result;
  }

  private static isRecord(value: unknown): value is Record<string, any> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
  }
}
