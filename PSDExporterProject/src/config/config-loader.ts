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
    // 如果指定了路径且文件存在，优先使用
    if (configPath && existsSync(configPath)) {
      try {
        const content = readFileSync(configPath, 'utf-8');
        const loaded = JSON.parse(content);
        return { ...this.defaultConfig, ...loaded };
      } catch (error) {
        console.warn(`Failed to load config from ${configPath}:`, error);
      }
    }

    // 未指定路径时，查找当前目录和项目根目录
    if (!configPath) {
      const searchPaths = [
        './psd-exporter.config.json',
        join(process.cwd(), 'psd-exporter.config.json'),
      ];

      for (const path of searchPaths) {
        if (existsSync(path)) {
          try {
            const content = readFileSync(path, 'utf-8');
            const loaded = JSON.parse(content);
            return { ...this.defaultConfig, ...loaded };
          } catch (error) {
            console.warn(`Failed to load config from ${path}:`, error);
          }
        }
      }
    }

    // 尝试从环境变量加载 API Key
    const config = { ...this.defaultConfig };
    if (process.env.CLAUDE_API_KEY) {
      config.claudeApiKey = process.env.CLAUDE_API_KEY;
    }

    return config;
  }
}
