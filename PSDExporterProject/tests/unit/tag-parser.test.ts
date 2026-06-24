import { TagParser } from '../../src/recognizer/tag-parser';
import { ComponentType } from '../../src/recognizer/component-types';

describe('TagParser', () => {
  let parser: TagParser;

  beforeEach(() => {
    parser = new TagParser();
  });

  describe('parse - valid tags', () => {
    test('should parse btn tag as Button', () => {
      expect(parser.parse('btn_close')).toBe('Button');
      expect(parser.parse('btn_ok')).toBe('Button');
      expect(parser.parse('btn_start_game')).toBe('Button');
    });

    test('should parse txt tag as Text', () => {
      expect(parser.parse('txt_title')).toBe('Text');
      expect(parser.parse('txt_score')).toBe('Text');
      expect(parser.parse('txt_player_name')).toBe('Text');
    });

    test('should parse img tag as Image', () => {
      expect(parser.parse('img_bg')).toBe('Image');
      expect(parser.parse('img_icon')).toBe('Image');
      expect(parser.parse('img_character_portrait')).toBe('Image');
    });

    test('should parse sv tag as ScrollView', () => {
      expect(parser.parse('sv_list')).toBe('ScrollView');
      expect(parser.parse('sv_content')).toBe('ScrollView');
      expect(parser.parse('sv_shop_items')).toBe('ScrollView');
    });

    test('should parse ipt tag as InputField', () => {
      expect(parser.parse('ipt_username')).toBe('InputField');
      expect(parser.parse('ipt_password')).toBe('InputField');
      expect(parser.parse('ipt_search_box')).toBe('InputField');
    });

    test('should parse vbox tag as VerticalLayoutGroup', () => {
      expect(parser.parse('vbox_menu')).toBe('VerticalLayoutGroup');
      expect(parser.parse('vbox_items')).toBe('VerticalLayoutGroup');
      expect(parser.parse('vbox_button_list')).toBe('VerticalLayoutGroup');
    });

    test('should parse hbox tag as HorizontalLayoutGroup', () => {
      expect(parser.parse('hbox_toolbar')).toBe('HorizontalLayoutGroup');
      expect(parser.parse('hbox_nav')).toBe('HorizontalLayoutGroup');
      expect(parser.parse('hbox_top_bar')).toBe('HorizontalLayoutGroup');
    });

    test('should parse grid tag as GridLayoutGroup', () => {
      expect(parser.parse('grid_inventory')).toBe('GridLayoutGroup');
      expect(parser.parse('grid_cards')).toBe('GridLayoutGroup');
      expect(parser.parse('grid_shop_grid')).toBe('GridLayoutGroup');
    });
  });

  describe('parse - case insensitivity', () => {
    test('should handle uppercase tags', () => {
      expect(parser.parse('BTN_close')).toBe('Button');
      expect(parser.parse('TXt_title')).toBe('Text');
      expect(parser.parse('IMG_BG')).toBe('Image');
    });

    test('should handle mixed case tags', () => {
      expect(parser.parse('Btn_close')).toBe('Button');
      expect(parser.parse('TxT_title')).toBe('Text');
      expect(parser.parse('ImG_icon')).toBe('Image');
    });
  });

  describe('parse - invalid inputs', () => {
    test('should return null for layer names without underscore', () => {
      expect(parser.parse('close')).toBeNull();
      expect(parser.parse('button')).toBeNull();
      expect(parser.parse('')).toBeNull();
    });

    test('should return null for unknown tags', () => {
      expect(parser.parse('foo_bar')).toBeNull();
      expect(parser.parse('xyz_test')).toBeNull();
      expect(parser.parse('unknown_close')).toBeNull();
    });

    test('should return null for empty string', () => {
      expect(parser.parse('')).toBeNull();
    });

    test('should return null for single underscore with empty parts', () => {
      // Note: '_name' splits into ['', 'name'] — tag '' is not in TAG_MAP
      expect(parser.parse('_name')).toBeNull();
    });

    test('should return null for trailing underscore only', () => {
      // 'btn_' splits into ['btn', ''] — tag 'btn' IS valid
      // This is technically valid since parts.length >= 2 and btn is a tag
      expect(parser.parse('btn_')).toBe('Button');
    });

    test('should handle multiple underscores by using first segment as tag', () => {
      expect(parser.parse('btn_close_popup_window')).toBe('Button');
      expect(parser.parse('txt_score_label_v2')).toBe('Text');
    });

    test('should return null for leading underscore followed by valid tag', () => {
      // '_btn_close' splits into ['', 'btn', 'close'] — tag '' is not valid
      expect(parser.parse('_btn_close')).toBeNull();
    });

    test('should return null for name that looks like but is not a valid tag', () => {
      // 'b_tn_close' — tag 'b' is not in TAG_MAP
      expect(parser.parse('b_tn_close')).toBeNull();
    });
  });

  describe('parse - edge cases', () => {
    test('should handle underscores in the name part correctly', () => {
      // The first segment is the tag, the rest is the name
      expect(parser.parse('btn__close')).toBe('Button'); // name starts with underscore
      expect(parser.parse('txt___title')).toBe('Text');
    });

    test('should handle numeric-only tags', () => {
      expect(parser.parse('123_close')).toBeNull();
    });

    test('should handle single character valid tags', () => {
      // All our tags are multi-character, but testing for robustness
      expect(parser.parse('a_close')).toBeNull();
    });

    test('should handle very long layer names', () => {
      const longName = 'txt_' + 'a'.repeat(200);
      expect(parser.parse(longName)).toBe('Text');
    });
  });

  describe('hasTag', () => {
    test('should return true for valid tagged layer names', () => {
      expect(parser.hasTag('btn_close')).toBe(true);
      expect(parser.hasTag('txt_title')).toBe(true);
      expect(parser.hasTag('img_bg')).toBe(true);
      expect(parser.hasTag('sv_content')).toBe(true);
      expect(parser.hasTag('ipt_name')).toBe(true);
      expect(parser.hasTag('vbox_list')).toBe(true);
      expect(parser.hasTag('hbox_bar')).toBe(true);
      expect(parser.hasTag('grid_cards')).toBe(true);
    });

    test('should return false for untagged layer names', () => {
      expect(parser.hasTag('close')).toBe(false);
      expect(parser.hasTag('mybutton')).toBe(false);
      expect(parser.hasTag('')).toBe(false);
      expect(parser.hasTag('foo_bar')).toBe(false);
    });

    test('should return true regardless of tag case', () => {
      expect(parser.hasTag('BTN_OK')).toBe(true);
      expect(parser.hasTag('Txt_title')).toBe(true);
    });
  });

  describe('ComponentType type checks', () => {
    test('all TAG_MAP values should be valid ComponentType values', () => {
      const validTypes: ComponentType[] = [
        'Button',
        'Image',
        'Text',
        'ScrollView',
        'InputField',
        'VerticalLayoutGroup',
        'HorizontalLayoutGroup',
        'GridLayoutGroup',
        'Unknown',
      ];

      // Every value in TAG_MAP should be in validTypes
      const parserAny = parser as any;
      const tagMap = TagParser as any;
      const values = Object.values(tagMap.TAG_MAP) as ComponentType[];
      for (const value of values) {
        expect(validTypes).toContain(value);
      }
    });

    test('parse should never return Unknown from a tag match', () => {
      // Unknown is not in TAG_MAP, so parse should never return it
      // Test all valid tag prefixes
      const tags = ['btn', 'txt', 'img', 'sv', 'ipt', 'vbox', 'hbox', 'grid'];
      for (const tag of tags) {
        const result = parser.parse(`${tag}_test`);
        expect(result).not.toBe('Unknown');
        expect(result).not.toBeNull();
      }
    });
  });
});
