// Mock ESM color-convert 模块，避免 Jest 在 CommonJS 环境下解析 ESM import 语法失败
jest.mock('color-convert', () => ({
  __esModule: true,
  default: {
    cmyk: { rgb: jest.fn().mockReturnValue([100, 200, 50] as [number, number, number]) },
    lab: { rgb: jest.fn().mockReturnValue([200, 50, 50] as [number, number, number]) },
  },
}));

import { TextStyleExtractor } from '../../src/parser/text-style-extractor';
import convert from 'color-convert';
import type { LayerTextData, Justification } from 'ag-psd';
import type { Color, LayerEffectsInfo } from 'ag-psd';

/** 获取模拟的 convert.cmyk.rgb 以便在测试中断言调用 */
const mockCmykRgb = convert.cmyk.rgb as unknown as jest.Mock;

/** 获取模拟的 convert.lab.rgb 以便在测试中断言调用 */
const mockLabRgb = convert.lab.rgb as unknown as jest.Mock;

/**
 * 创建最小的有效 LayerTextData 测试数据。
 * 仅包含 extract() 实际读取的字段子集，其余属性交由 TS 类型断言处理。
 */
function makeTextData(overrides: Partial<LayerTextData> = {}): LayerTextData {
  const base = {
    text: 'Hello World',
  };
  return { ...base, ...overrides } as unknown as LayerTextData;
}

/**
 * 创建包含样式信息的 LayerTextData 测试数据。
 */
function makeStyledTextData(overrides: {
  fontSize?: number;
  fillColor?: Color;
  fontName?: string;
  fauxBold?: boolean;
  fauxItalic?: boolean;
} = {}): LayerTextData {
  const style: Record<string, unknown> = {};
  if (overrides.fontSize !== undefined) style.fontSize = overrides.fontSize;
  if (overrides.fillColor !== undefined) style.fillColor = overrides.fillColor;
  if (overrides.fontName !== undefined) style.font = { name: overrides.fontName };
  if (overrides.fauxBold !== undefined) style.fauxBold = overrides.fauxBold;
  if (overrides.fauxItalic !== undefined) style.fauxItalic = overrides.fauxItalic;

  return makeTextData({ style } as Partial<LayerTextData>);
}

describe('TextStyleExtractor', () => {
  let extractor: TextStyleExtractor;

  beforeEach(() => {
    extractor = new TextStyleExtractor();
    jest.clearAllMocks();
  });

  // ---------------------------------------------------------------------------
  // extract() — 默认值处理
  // ---------------------------------------------------------------------------
  describe('extract() - 默认值处理', () => {
    test('textData 为 undefined 时返回默认样式', () => {
      const result = extractor.extract(undefined);

      expect(result.fontSize).toBe(12);
      expect(result.fontName).toBe('Arial');
      expect(result.color).toEqual({ r: 0, g: 0, b: 0, a: 1 });
      expect(result.fontStyle).toEqual({ bold: false, italic: false });
      expect(result.alignment).toEqual({ horizontal: 'left', vertical: 'top' });
    });

    test('textData.text 为空字符串时返回默认样式', () => {
      const emptyTextData = makeTextData({ text: '' });
      const result = extractor.extract(emptyTextData);

      expect(result.fontSize).toBe(12);
      expect(result.fontName).toBe('Arial');
      expect(result.color).toEqual({ r: 0, g: 0, b: 0, a: 1 });
    });
  });

  // ---------------------------------------------------------------------------
  // extract() — 基本属性提取
  // ---------------------------------------------------------------------------
  describe('extract() - 基本属性提取', () => {
    test('提取 fontSize', () => {
      const textData = makeStyledTextData({ fontSize: 24 });
      const result = extractor.extract(textData);

      expect(result.fontSize).toBe(24);
    });

    test('fontSize 缺失时使用默认值', () => {
      const textData = makeStyledTextData({});
      const result = extractor.extract(textData);

      expect(result.fontSize).toBe(12);
    });

    test('提取 fontName', () => {
      const textData = makeStyledTextData({ fontName: 'Impact' });
      const result = extractor.extract(textData);

      expect(result.fontName).toBe('Impact');
    });

    test('fontName 缺失时使用默认值', () => {
      const textData = makeStyledTextData({});
      const result = extractor.extract(textData);

      expect(result.fontName).toBe('Arial');
    });

    test('提取 fauxBold', () => {
      const textData = makeStyledTextData({ fauxBold: true });
      const result = extractor.extract(textData);

      expect(result.fontStyle.bold).toBe(true);
      expect(result.fontStyle.italic).toBe(false);
    });

    test('提取 fauxItalic', () => {
      const textData = makeStyledTextData({ fauxItalic: true });
      const result = extractor.extract(textData);

      expect(result.fontStyle.bold).toBe(false);
      expect(result.fontStyle.italic).toBe(true);
    });

    test('fauxBold 和 fauxItalic 均缺失时默认为 false', () => {
      const textData = makeStyledTextData({});
      const result = extractor.extract(textData);

      expect(result.fontStyle).toEqual({ bold: false, italic: false });
    });
  });

  // ---------------------------------------------------------------------------
  // extract() — 颜色转换（通过 style.fillColor 间接测试私有 convertColor）
  // ---------------------------------------------------------------------------
  describe('extract() - 颜色转换', () => {
    // RGBA/RGB 转换不依赖 color-convert，可直接测试实际逻辑
    test('RGBA 颜色正确转换并归一化到 0-1', () => {
      const textData = makeStyledTextData({
        fillColor: { r: 128, g: 64, b: 192, a: 255 },
      });
      const result = extractor.extract(textData);

      expect(result.color.r).toBeCloseTo(128 / 255, 5);
      expect(result.color.g).toBeCloseTo(64 / 255, 5);
      expect(result.color.b).toBeCloseTo(192 / 255, 5);
      expect(result.color.a).toBeCloseTo(1.0, 5);
    });

    test('RGBA 颜色半透明 alpha 正确转换', () => {
      const textData = makeStyledTextData({
        fillColor: { r: 255, g: 255, b: 255, a: 128 },
      });
      const result = extractor.extract(textData);

      expect(result.color.r).toBeCloseTo(1.0, 5);
      expect(result.color.g).toBeCloseTo(1.0, 5);
      expect(result.color.b).toBeCloseTo(1.0, 5);
      expect(result.color.a).toBeCloseTo(128 / 255, 5);
    });

    test('RGB 颜色（无 alpha 键）默认为不透明', () => {
      const textData = makeStyledTextData({
        fillColor: { r: 51, g: 102, b: 153 },
      });
      const result = extractor.extract(textData);

      expect(result.color.r).toBeCloseTo(51 / 255, 5);
      expect(result.color.g).toBeCloseTo(102 / 255, 5);
      expect(result.color.b).toBeCloseTo(153 / 255, 5);
      expect(result.color.a).toBe(1.0);
    });

    test('fillColor 为 undefined 时返回默认颜色', () => {
      const textData = makeStyledTextData({});
      const result = extractor.extract(textData);

      expect(result.color).toEqual({ r: 0, g: 0, b: 0, a: 1 });
    });

    test('CMYK 颜色通过 color-convert 转换并归一化', () => {
      // mockCmykRgb 返回 [100, 200, 50]
      const textData = makeStyledTextData({
        fillColor: { c: 0, m: 0.5, y: 1, k: 0 } as Color,
      });
      const result = extractor.extract(textData);

      // 验证调用参数：c,m,y,k 均乘以 100
      expect(mockCmykRgb).toHaveBeenCalledWith([0, 50, 100, 0]);
      // 验证归一化结果
      expect(result.color.r).toBeCloseTo(100 / 255, 5);
      expect(result.color.g).toBeCloseTo(200 / 255, 5);
      expect(result.color.b).toBeCloseTo(50 / 255, 5);
      expect(result.color.a).toBe(1.0);
    });

    test('Lab 颜色通过 color-convert 转换并归一化', () => {
      // mockLabRgb 返回 [200, 50, 50]
      const textData = makeStyledTextData({
        fillColor: { l: 53.23, a: 80.11, b: 67.22 } as Color,
      });
      const result = extractor.extract(textData);

      // 验证调用参数
      expect(mockLabRgb).toHaveBeenCalledWith([53.23, 80.11, 67.22]);
      // 验证归一化结果
      expect(result.color.r).toBeCloseTo(200 / 255, 5);
      expect(result.color.g).toBeCloseTo(50 / 255, 5);
      expect(result.color.b).toBeCloseTo(50 / 255, 5);
      expect(result.color.a).toBe(1.0);
    });
  });

  // ---------------------------------------------------------------------------
  // extract() — 对齐映射（通过 paragraphStyle.justification 和 shapeType 间接测试）
  // ---------------------------------------------------------------------------
  describe('extract() - 对齐映射', () => {
    function makeAlignmentTextData(
      justification: Justification | undefined,
      shapeType: 'point' | 'box' | undefined
    ): LayerTextData {
      const overrides: Record<string, unknown> = {};
      if (justification !== undefined) {
        overrides.paragraphStyle = { justification };
      }
      if (shapeType !== undefined) {
        overrides.shapeType = shapeType;
      }
      return makeTextData(overrides as Partial<LayerTextData>);
    }

    // 水平对齐测试
    test('justification: left → horizontal: left', () => {
      const textData = makeAlignmentTextData('left', 'point');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'left', vertical: 'top' });
    });

    test('justification: center → horizontal: center', () => {
      const textData = makeAlignmentTextData('center', 'point');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'center', vertical: 'top' });
    });

    test('justification: right → horizontal: right', () => {
      const textData = makeAlignmentTextData('right', 'point');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'right', vertical: 'top' });
    });

    test('justification: justify-left → horizontal: justify', () => {
      const textData = makeAlignmentTextData('justify-left', 'point');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'justify', vertical: 'top' });
    });

    test('justification: justify-right → horizontal: justify', () => {
      const textData = makeAlignmentTextData('justify-right', 'point');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'justify', vertical: 'top' });
    });

    test('justification: justify-center → horizontal: justify', () => {
      const textData = makeAlignmentTextData('justify-center', 'point');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'justify', vertical: 'top' });
    });

    test('justification: justify-all → horizontal: justify', () => {
      const textData = makeAlignmentTextData('justify-all', 'point');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'justify', vertical: 'top' });
    });

    // 垂直对齐测试
    test('shapeType: box → vertical: middle', () => {
      const textData = makeAlignmentTextData('left', 'box');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'left', vertical: 'middle' });
    });

    test('shapeType: point → vertical: top', () => {
      const textData = makeAlignmentTextData('center', 'point');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'center', vertical: 'top' });
    });

    // 默认值测试
    test('justification 为 undefined 时默认 horizontal: left', () => {
      const textData = makeAlignmentTextData(undefined, 'point');
      const result = extractor.extract(textData);
      expect(result.alignment.horizontal).toBe('left');
    });

    test('shapeType 为 undefined 时默认 vertical: top', () => {
      const textData = makeAlignmentTextData('left', undefined);
      const result = extractor.extract(textData);
      expect(result.alignment.vertical).toBe('top');
    });

    // 组合测试
    test('justification: right + shapeType: box → right/middle', () => {
      const textData = makeAlignmentTextData('right', 'box');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'right', vertical: 'middle' });
    });

    test('justification: justify-left + shapeType: box → justify/middle', () => {
      const textData = makeAlignmentTextData('justify-left', 'box');
      const result = extractor.extract(textData);
      expect(result.alignment).toEqual({ horizontal: 'justify', vertical: 'middle' });
    });
  });

  // ---------------------------------------------------------------------------
  // extract() — 综合场景
  // ---------------------------------------------------------------------------
  describe('extract() - 综合场景', () => {
    test('完整的文本数据提取所有字段', () => {
      const textData = makeTextData({
        style: {
          fontSize: 36,
          fillColor: { r: 255, g: 0, b: 0, a: 255 },
          font: { name: 'Impact' },
          fauxBold: true,
          fauxItalic: false,
        },
        paragraphStyle: { justification: 'center' as Justification },
        shapeType: 'box',
      } as Partial<LayerTextData>);

      const result = extractor.extract(textData);

      expect(result.fontSize).toBe(36);
      expect(result.fontName).toBe('Impact');
      expect(result.fontStyle).toEqual({ bold: true, italic: false });
      expect(result.color.r).toBeCloseTo(1.0, 5);
      expect(result.color.g).toBeCloseTo(0.0, 5);
      expect(result.color.b).toBeCloseTo(0.0, 5);
      expect(result.color.a).toBeCloseTo(1.0, 5);
      expect(result.alignment).toEqual({ horizontal: 'center', vertical: 'middle' });
    });
  });

  // ---------------------------------------------------------------------------
  // extract() — 效果提取
  // ---------------------------------------------------------------------------
  describe('extract() - 效果提取', () => {
    test('should extract stroke effect', () => {
      const textData = makeStyledTextData({ fontSize: 16 });
      const effects: LayerEffectsInfo = {
        stroke: [
          {
            enabled: true,
            size: { value: 2, units: 'Pixels' },
            position: 'outside',
            color: { r: 255, g: 0, b: 0, a: 255 },
          },
        ],
      } as LayerEffectsInfo;

      const result = extractor.extract(textData, effects);

      expect(result.effects).toBeDefined();
      expect(result.effects!.length).toBe(1);
      expect(result.effects![0].type).toBe('stroke');
      expect((result.effects![0] as any).width).toBe(2);
    });

    test('should extract multiple effects', () => {
      const textData = makeStyledTextData({ fontSize: 16 });
      const effects: LayerEffectsInfo = {
        stroke: [
          {
            enabled: true,
            size: { value: 2, units: 'Pixels' },
            position: 'outside',
            color: { r: 255, g: 0, b: 0, a: 255 },
          },
        ],
        dropShadow: [
          {
            enabled: true,
            size: { value: 5, units: 'Pixels' },
            angle: 45,
            distance: { value: 3, units: 'Pixels' },
            color: { r: 0, g: 0, b: 0, a: 255 },
          },
        ],
        outerGlow: {
          enabled: true,
          size: { value: 4, units: 'Pixels' },
          choke: { value: 0, units: 'Pixels' },
          color: { r: 255, g: 255, b: 0, a: 255 },
        },
      } as LayerEffectsInfo;

      const result = extractor.extract(textData, effects);

      expect(result.effects).toBeDefined();
      expect(result.effects!.length).toBe(3);
      const types = result.effects!.map((e) => e.type);
      expect(types).toEqual(['stroke', 'dropShadow', 'outerGlow']);
    });

    test('should skip disabled effects', () => {
      const textData = makeStyledTextData({ fontSize: 16 });
      const effects: LayerEffectsInfo = {
        stroke: [
          {
            enabled: false,
            size: { value: 2, units: 'Pixels' },
            position: 'outside',
            color: { r: 255, g: 0, b: 0, a: 255 },
          },
        ],
        dropShadow: [
          {
            enabled: true,
            size: { value: 5, units: 'Pixels' },
            angle: 0,
            distance: { value: 3, units: 'Pixels' },
            color: { r: 0, g: 0, b: 0, a: 255 },
          },
        ],
      } as LayerEffectsInfo;

      const result = extractor.extract(textData, effects);

      expect(result.effects).toBeDefined();
      expect(result.effects!.length).toBe(1);
      expect(result.effects![0].type).toBe('dropShadow');
    });

    test('should degrade radial gradient to linear', () => {
      const textData = makeStyledTextData({ fontSize: 16 });
      const effects: LayerEffectsInfo = {
        gradientOverlay: [
          {
            enabled: true,
            type: 'radial' as any,
            angle: 0,
            gradient: {
              type: 'solid' as const,
              name: 'test',
              colorStops: [
                {
                  color: { r: 255, g: 0, b: 0, a: 255 } as Color,
                  location: 0,
                  midpoint: 50,
                },
                {
                  color: { r: 0, g: 0, b: 255, a: 255 } as Color,
                  location: 255,
                  midpoint: 50,
                },
              ],
              opacityStops: [],
            },
          },
        ],
      } as any;

      const result = extractor.extract(textData, effects);

      expect(result.effects).toBeDefined();
      expect(result.effects!.length).toBe(1);
      const gradEffect = result.effects![0] as any;
      expect(gradEffect.type).toBe('gradient');
      expect(gradEffect.gradientType).toBe('linear');
      expect(gradEffect.degraded).toBe(true);
    });

    test('should degrade diagonal gradient angle', () => {
      const textData = makeStyledTextData({ fontSize: 16 });
      const effects: LayerEffectsInfo = {
        gradientOverlay: [
          {
            enabled: true,
            type: 'linear' as any,
            angle: 45,
            gradient: {
              type: 'solid' as const,
              name: 'test',
              colorStops: [
                {
                  color: { r: 255, g: 0, b: 0, a: 255 } as Color,
                  location: 0,
                  midpoint: 50,
                },
                {
                  color: { r: 0, g: 0, b: 255, a: 255 } as Color,
                  location: 255,
                  midpoint: 50,
                },
              ],
              opacityStops: [],
            },
          },
        ],
      } as any;

      const result = extractor.extract(textData, effects);

      expect(result.effects).toBeDefined();
      expect(result.effects!.length).toBe(1);
      const gradEffect = result.effects![0] as any;
      expect(gradEffect.type).toBe('gradient');
      // 45 snaps to 90 (nearest cardinal)
      expect(gradEffect.angle).toBe(90);
      expect(gradEffect.degraded).toBe(true);
    });

    test('should degrade multi-stop gradient', () => {
      const textData = makeStyledTextData({ fontSize: 16 });
      const effects: LayerEffectsInfo = {
        gradientOverlay: [
          {
            enabled: true,
            type: 'linear' as any,
            angle: 90,
            gradient: {
              type: 'solid' as const,
              name: 'test',
              colorStops: [
                {
                  color: { r: 255, g: 0, b: 0, a: 255 } as Color,
                  location: 0,
                  midpoint: 50,
                },
                {
                  color: { r: 0, g: 255, b: 0, a: 255 } as Color,
                  location: 128,
                  midpoint: 50,
                },
                {
                  color: { r: 0, g: 0, b: 255, a: 255 } as Color,
                  location: 255,
                  midpoint: 50,
                },
              ],
              opacityStops: [],
            },
          },
        ],
      } as any;

      const result = extractor.extract(textData, effects);

      expect(result.effects).toBeDefined();
      expect(result.effects!.length).toBe(1);
      const gradEffect = result.effects![0] as any;
      expect(gradEffect.type).toBe('gradient');
      // 3 color stops → 2
      expect(gradEffect.colors.length).toBe(2);
      expect(gradEffect.degraded).toBe(true);
    });

    test('no effects key when effects param is not provided', () => {
      const textData = makeStyledTextData({ fontSize: 16 });
      const result = extractor.extract(textData);
      expect(result.effects).toBeUndefined();
    });
  });
});
