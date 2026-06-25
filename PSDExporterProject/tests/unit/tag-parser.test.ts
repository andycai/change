import { TagParser } from '../../src/recognizer/tag-parser';
import { TagConfigLoader } from '../../src/recognizer/tag-config-loader';
import * as path from 'path';

describe('TagParser', () => {
  let parser: TagParser;
  const configPath = path.resolve(__dirname, '../../config/tag-config.json');

  beforeEach(() => {
    const config = TagConfigLoader.load(configPath);
    parser = new TagParser(config);
  });

  describe('parse - valid tags', () => {
    test('should parse bt tag as main family', () => {
      const result = parser.parse('close.bt');
      expect(result).not.toBeNull();
      expect(result!.families.main).toBe('bt');
      expect(result!.baseName).toBe('close');
    });

    test('should parse txt tag as main family', () => {
      const result = parser.parse('title.txt');
      expect(result).not.toBeNull();
      expect(result!.families.main).toBe('txt');
    });

    test('should parse img tag as main family', () => {
      const result = parser.parse('bg.img');
      expect(result).not.toBeNull();
      expect(result!.families.main).toBe('img');
    });

    test('should parse sv tag as main family', () => {
      const result = parser.parse('list.sv');
      expect(result).not.toBeNull();
      expect(result!.families.main).toBe('sv');
    });

    test('should parse ipt tag as main family', () => {
      const result = parser.parse('username.ipt');
      expect(result).not.toBeNull();
      expect(result!.families.main).toBe('ipt');
    });

    test('should parse vbox tag as main family', () => {
      const result = parser.parse('menu.vbox');
      expect(result).not.toBeNull();
      expect(result!.families.main).toBe('vbox');
    });

    test('should parse hbox tag as main family', () => {
      const result = parser.parse('toolbar.hbox');
      expect(result).not.toBeNull();
      expect(result!.families.main).toBe('hbox');
    });

    test('should parse grid tag as main family', () => {
      const result = parser.parse('cards.grid');
      expect(result).not.toBeNull();
      expect(result!.families.main).toBe('grid');
    });
  });

  describe('parse - invalid inputs', () => {
    test('should return null for layer names without tags', () => {
      expect(parser.parse('close')).toBeNull();
      expect(parser.parse('button')).toBeNull();
      expect(parser.parse('')).toBeNull();
    });

    test('should return null for unknown tags', () => {
      expect(parser.parse('foo.bar')).toBeNull();
      expect(parser.parse('test.xyz')).toBeNull();
    });

    test('should return null for empty string', () => {
      expect(parser.parse('')).toBeNull();
    });
  });

  describe('hasTag', () => {
    test('should return true for valid tagged layer names', () => {
      expect(parser.hasTag('close.bt')).toBe(true);
      expect(parser.hasTag('title.txt')).toBe(true);
      expect(parser.hasTag('bg.img')).toBe(true);
      expect(parser.hasTag('list.sv')).toBe(true);
      expect(parser.hasTag('username.ipt')).toBe(true);
      expect(parser.hasTag('menu.vbox')).toBe(true);
      expect(parser.hasTag('toolbar.hbox')).toBe(true);
      expect(parser.hasTag('cards.grid')).toBe(true);
    });

    test('should return false for untagged layer names', () => {
      expect(parser.hasTag('close')).toBe(false);
      expect(parser.hasTag('mybutton')).toBe(false);
      expect(parser.hasTag('')).toBe(false);
      expect(parser.hasTag('foo.bar')).toBe(false);
    });
  });
});
