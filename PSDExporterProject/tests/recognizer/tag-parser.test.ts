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

  describe('parse', () => {
    // =========================================================================
    // 多标签叠加 (Multi-tag Stacking)
    // =========================================================================

    test('should parse multi-tag stacking: close.bt.tmp.bg', () => {
      const result = parser.parse('close.bt.tmp.bg');

      expect(result).not.toBeNull();
      expect(result!.prefix).toBeUndefined();
      expect(result!.baseName).toBe('close');
      expect(result!.families.main).toBe('bt');
      expect(result!.families.textBackend).toBe('tmp');
      expect(result!.families.role).toBe('bg');
    });

    test('should parse multi-tag stacking: icon.img.sliced', () => {
      const result = parser.parse('icon.img.sliced');

      expect(result).not.toBeNull();
      expect(result!.prefix).toBeUndefined();
      expect(result!.baseName).toBe('icon');
      expect(result!.families.main).toBe('img');
      expect(result!.families.imageType).toBe('sliced');
    });

    test('should parse four-family full stack: panel.bt.tmp.sliced.bg', () => {
      const result = parser.parse('panel.bt.tmp.sliced.bg');

      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('panel');
      expect(result!.families.main).toBe('bt');
      expect(result!.families.textBackend).toBe('tmp');
      expect(result!.families.imageType).toBe('sliced');
      expect(result!.families.role).toBe('bg');
    });

    // =========================================================================
    // 右到左优先级 (Right-to-Left Priority)
    // =========================================================================

    test('should apply right-to-left priority: panel.bt.dpd (dpd wins)', () => {
      const result = parser.parse('panel.bt.dpd');

      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('panel');
      // dpd appears last (rightmost), so it should override bt
      expect(result!.families.main).toBe('dpd');
    });

    test('should apply right-to-left priority: icon.sliced.simple (simple wins)', () => {
      const result = parser.parse('icon.sliced.simple');

      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('icon');
      // simple is rightmost, overrides sliced
      expect(result!.families.imageType).toBe('simple');
    });

    test('should apply right-to-left priority: field.tmp.ugui (ugui wins)', () => {
      const result = parser.parse('field.tmp.ugui');

      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('field');
      expect(result!.families.textBackend).toBe('ugui');
    });

    // =========================================================================
    // Prefix 处理 (ref / refp)
    // =========================================================================

    test('should parse ref prefix: ref icon.img', () => {
      const result = parser.parse('ref icon.img');

      expect(result).not.toBeNull();
      expect(result!.prefix).toBe('ref');
      expect(result!.baseName).toBe('icon');
      expect(result!.families.main).toBe('img');
    });

    test('should parse refp prefix: refp panel.bt', () => {
      const result = parser.parse('refp panel.bt');

      expect(result).not.toBeNull();
      expect(result!.prefix).toBe('refp');
      expect(result!.baseName).toBe('panel');
      expect(result!.families.main).toBe('bt');
    });

    test('should parse ref prefix with multi-word baseName: ref close button.bt.tmp', () => {
      const result = parser.parse('ref close button.bt.tmp');

      expect(result).not.toBeNull();
      expect(result!.prefix).toBe('ref');
      expect(result!.baseName).toBe('close button');
      expect(result!.families.main).toBe('bt');
      expect(result!.families.textBackend).toBe('tmp');
    });

    test('should parse refp prefix with multi-word baseName: refp main panel.img.sliced', () => {
      const result = parser.parse('refp main panel.img.sliced');

      expect(result).not.toBeNull();
      expect(result!.prefix).toBe('refp');
      expect(result!.baseName).toBe('main panel');
      expect(result!.families.main).toBe('img');
      expect(result!.families.imageType).toBe('sliced');
    });

    // =========================================================================
    // 未知标签跳过 (Unknown Tag Skipping)
    // =========================================================================

    test('should skip unknown tags: name.unknowntag.bt', () => {
      const result = parser.parse('name.unknowntag.bt');

      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('name');
      expect(result!.families.main).toBe('bt');
    });

    test('should skip unknown tags and keep valid ones: icon.newtag.img.sliced', () => {
      const result = parser.parse('icon.newtag.img.sliced');

      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('icon');
      expect(result!.families.main).toBe('img');
      expect(result!.families.imageType).toBe('sliced');
    });

    test('should handle unknown tag between known tags: panel.bt.faketag.bg', () => {
      const result = parser.parse('panel.bt.faketag.bg');

      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('panel');
      expect(result!.families.main).toBe('bt');
      expect(result!.families.role).toBe('bg');
    });

    // =========================================================================
    // 无标签情况 (No Tags)
    // =========================================================================

    test('should return null for layer name with no tags: background', () => {
      const result = parser.parse('background');

      expect(result).toBeNull();
    });

    test('should return null when all tags are unknown: name.xyz.abc', () => {
      const result = parser.parse('name.xyz.abc');

      expect(result).toBeNull();
    });

    test('should return null for empty string', () => {
      const result = parser.parse('');

      expect(result).toBeNull();
    });

    // =========================================================================
    // 边界情况 (Edge Cases)
    // =========================================================================

    test('should handle layer name with only tags (no baseName): bt.tmp', () => {
      const result = parser.parse('bt.tmp');

      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('');
      expect(result!.families.main).toBe('bt');
      expect(result!.families.textBackend).toBe('tmp');
    });

    test('should handle single tag: close.bt', () => {
      const result = parser.parse('close.bt');

      expect(result).not.toBeNull();
      expect(result!.prefix).toBeUndefined();
      expect(result!.baseName).toBe('close');
      expect(result!.families.main).toBe('bt');
    });

    test('should handle baseName with dots: my.close.button.bt', () => {
      const result = parser.parse('my.close.button.bt');

      // The last segment that matches a tag should be treated as a tag
      // bt is the main tag, everything before it is baseName
      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('my.close.button');
      expect(result!.families.main).toBe('bt');
    });
  });

  // =========================================================================
  // hasTag 方法
  // =========================================================================

  describe('hasTag', () => {
    test('should return true for layer name with valid tags: close.bt', () => {
      expect(parser.hasTag('close.bt')).toBe(true);
    });

    test('should return true for multi-tag layer name: close.bt.tmp.bg', () => {
      expect(parser.hasTag('close.bt.tmp.bg')).toBe(true);
    });

    test('should return true for prefixed layer name: ref icon.img', () => {
      expect(parser.hasTag('ref icon.img')).toBe(true);
    });

    test('should return false for layer name with no tags: background', () => {
      expect(parser.hasTag('background')).toBe(false);
    });

    test('should return false for layer name with all unknown tags: name.xyz.abc', () => {
      expect(parser.hasTag('name.xyz.abc')).toBe(false);
    });

    test('should return false for empty string', () => {
      expect(parser.hasTag('')).toBe(false);
    });
  });
});
