import { ConfigLoader, Config } from '../../src/config/config-loader';
import { existsSync, writeFileSync, unlinkSync, mkdirSync } from 'fs';
import { join } from 'path';

const testConfigPath = join(__dirname, '__test_config.json');

describe('ConfigLoader', () => {
  afterEach(() => {
    // Clean up test config files
    if (existsSync(testConfigPath)) {
      unlinkSync(testConfigPath);
    }
  });

  test('should return default config when no config file exists', () => {
    const config = ConfigLoader.load('/nonexistent/path/config.json');
    expect(config.aiThreshold).toBe(0.7);
    expect(config.cvConfidenceMin).toBe(0.6);
    expect(config.enableAI).toBe(true);
    expect(config.debug).toBe(false);
    expect(config.claudeApiKey).toBeUndefined();
  });

  test('should load config from a file and merge with defaults', () => {
    const customConfig = {
      aiThreshold: 0.5,
      debug: true,
    };
    writeFileSync(testConfigPath, JSON.stringify(customConfig));

    const config = ConfigLoader.load(testConfigPath);
    expect(config.aiThreshold).toBe(0.5);
    expect(config.cvConfidenceMin).toBe(0.6); // default
    expect(config.enableAI).toBe(true); // default
    expect(config.debug).toBe(true);
  });

  test('should load claudeApiKey from config file', () => {
    const customConfig = {
      claudeApiKey: 'sk-ant-test-key-123',
    };
    writeFileSync(testConfigPath, JSON.stringify(customConfig));

    const config = ConfigLoader.load(testConfigPath);
    expect(config.claudeApiKey).toBe('sk-ant-test-key-123');
  });

  test('should load claudeApiKey from environment variable', () => {
    const original = process.env.CLAUDE_API_KEY;
    process.env.CLAUDE_API_KEY = 'sk-ant-env-key-456';

    try {
      const config = ConfigLoader.load('/nonexistent/path/config.json');
      expect(config.claudeApiKey).toBe('sk-ant-env-key-456');
    } finally {
      // Restore original env
      if (original === undefined) {
        delete process.env.CLAUDE_API_KEY;
      } else {
        process.env.CLAUDE_API_KEY = original;
      }
    }
  });

  test('should prefer config file value over environment variable', () => {
    const original = process.env.CLAUDE_API_KEY;
    process.env.CLAUDE_API_KEY = 'sk-ant-env-key';

    try {
      const customConfig = {
        claudeApiKey: 'sk-ant-file-key',
      };
      writeFileSync(testConfigPath, JSON.stringify(customConfig));

      const config = ConfigLoader.load(testConfigPath);
      expect(config.claudeApiKey).toBe('sk-ant-file-key');
    } finally {
      if (original === undefined) {
        delete process.env.CLAUDE_API_KEY;
      } else {
        process.env.CLAUDE_API_KEY = original;
      }
    }
  });

  test('should handle malformed JSON gracefully', () => {
    writeFileSync(testConfigPath, '{ invalid json }');

    const config = ConfigLoader.load(testConfigPath);
    // Should fall back to defaults
    expect(config.aiThreshold).toBe(0.7);
    expect(config.cvConfidenceMin).toBe(0.6);
    expect(config.debug).toBe(false);
  });

  test('should disable AI when enableAI is false', () => {
    const customConfig = {
      enableAI: false,
    };
    writeFileSync(testConfigPath, JSON.stringify(customConfig));

    const config = ConfigLoader.load(testConfigPath);
    expect(config.enableAI).toBe(false);
  });
});
