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
    expect(config.fairyGui.packageName).toBe('PSDImport');
    expect(config.fairyGui.defaultScale9).toEqual({
      unit: 'ratio', left: 0.3, top: 0.3, right: 0.3, bottom: 0.3,
    });
  });

  test('should load FairyGUI config and merge nested defaults', () => {
    writeFileSync(testConfigPath, JSON.stringify({
      fairyGui: {
        projectPath: '../UIProject',
        packageName: 'Menus',
        sourceRoot: '../UIArtifacts/psd',
        defaultScale9: { unit: 'pixels', left: 10, top: 12, right: 10, bottom: 12 },
        fontMappings: {
          'PingFang SC': { default: 'PingFang SC', tmp: 'ui://fontpkgfont01' },
        },
        references: {
          external: {
            CommonClose: { packageId: 'common01', resourceId: 'close001' },
          },
        },
      },
    }));

    const config = ConfigLoader.load(testConfigPath);

    expect(config.fairyGui.projectPath).toBe('../UIProject');
    expect(config.fairyGui.packageName).toBe('Menus');
    expect(config.fairyGui.sourceRoot).toBe('../UIArtifacts/psd');
    expect(config.fairyGui.defaultScale9.unit).toBe('pixels');
    expect(config.fairyGui.fontMappings['PingFang SC'].tmp).toBe('ui://fontpkgfont01');
    expect(config.fairyGui.references.external.CommonClose).toEqual({
      packageId: 'common01', resourceId: 'close001',
    });
    expect(config.fairyGui.references.local).toEqual({});
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

  test('should use env var for claudeApiKey when config file does not set it', () => {
    const original = process.env.CLAUDE_API_KEY;
    process.env.CLAUDE_API_KEY = 'sk-ant-env-key-789';

    try {
      const customConfig = {
        aiThreshold: 0.5,
        // no claudeApiKey
      };
      writeFileSync(testConfigPath, JSON.stringify(customConfig));

      const config = ConfigLoader.load(testConfigPath);
      expect(config.aiThreshold).toBe(0.5);
      expect(config.claudeApiKey).toBe('sk-ant-env-key-789');
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

  // Validation tests - runtime type checking
  test('should reject string aiThreshold and fall back to default', () => {
    const customConfig = {
      aiThreshold: '0.5',
    };
    writeFileSync(testConfigPath, JSON.stringify(customConfig));

    const config = ConfigLoader.load(testConfigPath);
    expect(config.aiThreshold).toBe(0.7); // default
  });

  test('should reject aiThreshold out of range and fall back to default', () => {
    const customConfig = {
      aiThreshold: 1.5,
    };
    writeFileSync(testConfigPath, JSON.stringify(customConfig));

    const config = ConfigLoader.load(testConfigPath);
    expect(config.aiThreshold).toBe(0.7); // default (out of 0-1 range)
  });

  test('should accept valid boundary aiThreshold values', () => {
    writeFileSync(testConfigPath, JSON.stringify({ aiThreshold: 0.0 }));
    let config = ConfigLoader.load(testConfigPath);
    expect(config.aiThreshold).toBe(0.0);

    writeFileSync(testConfigPath, JSON.stringify({ aiThreshold: 1.0 }));
    config = ConfigLoader.load(testConfigPath);
    expect(config.aiThreshold).toBe(1.0);
  });

  test('should reject non-boolean enableAI and fall back to default', () => {
    const customConfig = {
      enableAI: 'yes',
    };
    writeFileSync(testConfigPath, JSON.stringify(customConfig));

    const config = ConfigLoader.load(testConfigPath);
    expect(config.enableAI).toBe(true); // default
  });

  test('should reject non-boolean debug and fall back to default', () => {
    const customConfig = {
      debug: 1,
    };
    writeFileSync(testConfigPath, JSON.stringify(customConfig));

    const config = ConfigLoader.load(testConfigPath);
    expect(config.debug).toBe(false); // default
  });

  test('should reject non-number cvConfidenceMin and fall back to default', () => {
    const customConfig = {
      cvConfidenceMin: 'high',
    };
    writeFileSync(testConfigPath, JSON.stringify(customConfig));

    const config = ConfigLoader.load(testConfigPath);
    expect(config.cvConfidenceMin).toBe(0.6); // default
  });

  test('should reject non-string claudeApiKey and fall back to default', () => {
    const customConfig = {
      claudeApiKey: 12345,
    };
    writeFileSync(testConfigPath, JSON.stringify(customConfig));

    const config = ConfigLoader.load(testConfigPath);
    expect(config.claudeApiKey).toBeUndefined();
  });
});
