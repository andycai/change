import { TagConfigLoader } from '../../src/recognizer/tag-config-loader';
import * as path from 'path';

describe('TagConfigLoader', () => {
  const validConfigPath = path.resolve(__dirname, '../../config/tag-config.json');
  const missingConfigPath = path.resolve(__dirname, '../../config/nonexistent.json');

  describe('load', () => {
    test('should load valid config file successfully', () => {
      const config = TagConfigLoader.load(validConfigPath);

      expect(config).toBeDefined();
      expect(config.version).toBe('1.0.0');
      expect(config.families.main.length).toBeGreaterThan(0);
      expect(config.families.textBackend.length).toBeGreaterThan(0);
      expect(config.families.imageType.length).toBeGreaterThan(0);
      expect(config.families.role.length).toBeGreaterThan(0);
    });

    test('should throw error when config file not found', () => {
      expect(() => {
        TagConfigLoader.load(missingConfigPath);
      }).toThrow('Config file not found');
    });

    test('should throw error when config file has invalid format', () => {
      const invalidPath = path.resolve(__dirname, '../fixtures/invalid-config.json');
      expect(() => {
        TagConfigLoader.load(invalidPath);
      }).toThrow();
    });
  });

  describe('validate', () => {
    test('should validate correct config data', () => {
      const validData = {
        version: '1.0.0',
        canonicalOrder: ['main'],
        families: {
          main: [{ id: 'bt', label: 'Button' }],
          textBackend: [],
          imageType: [],
          role: []
        }
      };

      const config = TagConfigLoader.validate(validData);
      expect(config).toEqual(validData);
    });

    test('should reject config missing version field', () => {
      const invalidData = {
        families: { main: [], textBackend: [], imageType: [], role: [] }
      };

      expect(() => {
        TagConfigLoader.validate(invalidData);
      }).toThrow();
    });

    test('should reject config with invalid tag structure', () => {
      const invalidData = {
        version: '1.0.0',
        canonicalOrder: ['main'],
        families: {
          main: [{ id: 'bt' }], // missing label
          textBackend: [],
          imageType: [],
          role: []
        }
      };

      expect(() => {
        TagConfigLoader.validate(invalidData);
      }).toThrow();
    });
  });
});
