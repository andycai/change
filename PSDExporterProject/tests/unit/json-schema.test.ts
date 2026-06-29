import {
  TextStylesSchema,
  StrokeEffectSchema,
  ShadowEffectSchema,
  GradientEffectSchema,
  GlowEffectSchema,
  BevelEffectSchema,
  TextEffectSchema,
  RGBASchema,
  LayerConfigSchema,
} from '../../src/generator/json-schema';

// ---------------------------------------------------------------------------
// Group 1: TextStylesSchema – valid inputs
// ---------------------------------------------------------------------------
describe('TextStylesSchema', () => {
  describe('valid inputs', () => {
    test('accepts valid TextStyles with all required fields', () => {
      const result = TextStylesSchema.safeParse({
        fontSize: 36,
        color: { r: 1, g: 1, b: 1, a: 1 },
        fontName: 'Arial',
        fontStyle: { bold: true, italic: false },
        alignment: { horizontal: 'center', vertical: 'middle' },
        effects: [],
      });
      expect(result.success).toBe(true);
    });

    test('accepts TextStyles without effects field (optional)', () => {
      const result = TextStylesSchema.safeParse({
        fontSize: 36,
        color: { r: 1, g: 1, b: 1, a: 1 },
        fontName: 'Arial',
        fontStyle: { bold: true, italic: false },
        alignment: { horizontal: 'center', vertical: 'middle' },
      });
      expect(result.success).toBe(true);
    });

    test('accepts TextStyles with effects containing a stroke effect', () => {
      const result = TextStylesSchema.safeParse({
        fontSize: 36,
        color: { r: 1, g: 1, b: 1, a: 1 },
        fontName: 'Arial',
        fontStyle: { bold: true, italic: false },
        alignment: { horizontal: 'center', vertical: 'middle' },
        effects: [
          {
            type: 'stroke',
            enabled: true,
            color: { r: 0, g: 0, b: 0, a: 1 },
            width: 2,
            position: 'outside',
          },
        ],
      });
      expect(result.success).toBe(true);
    });
  });

  // -----------------------------------------------------------------------
  // Group 2: TextStylesSchema – rejection rules
  // -----------------------------------------------------------------------
  describe('rejection rules', () => {
    test('rejects negative fontSize', () => {
      const result = TextStylesSchema.safeParse({
        fontSize: -12,
        color: { r: 1, g: 1, b: 1, a: 1 },
        fontName: 'Arial',
        fontStyle: { bold: true, italic: false },
        alignment: { horizontal: 'center', vertical: 'middle' },
      });
      expect(result.success).toBe(false);
    });

    test('rejects color values outside [0,1] range', () => {
      const result = TextStylesSchema.safeParse({
        fontSize: 36,
        color: { r: 1.5, g: 1, b: 1, a: 1 },
        fontName: 'Arial',
        fontStyle: { bold: true, italic: false },
        alignment: { horizontal: 'center', vertical: 'middle' },
      });
      expect(result.success).toBe(false);
    });

    test('rejects invalid horizontal alignment value', () => {
      const result = TextStylesSchema.safeParse({
        fontSize: 36,
        color: { r: 1, g: 1, b: 1, a: 1 },
        fontName: 'Arial',
        fontStyle: { bold: true, italic: false },
        alignment: { horizontal: 'invalid', vertical: 'middle' },
      });
      expect(result.success).toBe(false);
    });

    test('rejects missing required field (no fontName)', () => {
      const result = TextStylesSchema.safeParse({
        fontSize: 36,
        color: { r: 1, g: 1, b: 1, a: 1 },
        fontStyle: { bold: true, italic: false },
        alignment: { horizontal: 'center', vertical: 'middle' },
      });
      expect(result.success).toBe(false);
    });
  });
});

// ---------------------------------------------------------------------------
// Group 3: Effect schemas – valid inputs
// ---------------------------------------------------------------------------
describe('Effect schemas', () => {
  describe('valid inputs', () => {
    test('accepts valid StrokeEffect', () => {
      const result = StrokeEffectSchema.safeParse({
        type: 'stroke',
        enabled: true,
        color: { r: 0, g: 0, b: 0, a: 1 },
        width: 2,
        position: 'outside',
      });
      expect(result.success).toBe(true);
    });

    test('accepts valid ShadowEffect (dropShadow)', () => {
      const result = ShadowEffectSchema.safeParse({
        type: 'dropShadow',
        enabled: true,
        color: { r: 0, g: 0, b: 0, a: 0.5 },
        offsetX: 2,
        offsetY: 2,
        blur: 4,
      });
      expect(result.success).toBe(true);
    });

    test('accepts valid ShadowEffect (innerShadow)', () => {
      const result = ShadowEffectSchema.safeParse({
        type: 'innerShadow',
        enabled: true,
        color: { r: 0, g: 0, b: 0, a: 0.5 },
        offsetX: 1,
        offsetY: 1,
        blur: 2,
      });
      expect(result.success).toBe(true);
    });

    test('accepts valid GradientEffect', () => {
      const result = GradientEffectSchema.safeParse({
        type: 'gradient',
        enabled: true,
        gradientType: 'linear',
        angle: 90,
        colors: [
          { r: 0, g: 0, b: 0, a: 1, position: 0 },
          { r: 1, g: 1, b: 1, a: 1, position: 1 },
        ],
        degraded: false,
      });
      expect(result.success).toBe(true);
    });

    test('accepts valid GlowEffect (outerGlow)', () => {
      const result = GlowEffectSchema.safeParse({
        type: 'outerGlow',
        enabled: true,
        color: { r: 1, g: 1, b: 0, a: 1 },
        size: 5,
        spread: 0,
      });
      expect(result.success).toBe(true);
    });

    test('accepts valid BevelEffect', () => {
      const result = BevelEffectSchema.safeParse({
        type: 'bevel',
        enabled: true,
        style: 'innerBevel',
        depth: 3,
        size: 2,
        angle: 120,
        highlightColor: { r: 1, g: 1, b: 1, a: 1 },
        shadowColor: { r: 0, g: 0, b: 0, a: 0.5 },
      });
      expect(result.success).toBe(true);
    });
  });

  // -----------------------------------------------------------------------
  // Group 4: Effect schemas – rejection
  // -----------------------------------------------------------------------
  describe('rejection', () => {
    test('rejects stroke effect with invalid position', () => {
      const result = StrokeEffectSchema.safeParse({
        type: 'stroke',
        enabled: true,
        color: { r: 0, g: 0, b: 0, a: 1 },
        width: 2,
        position: 'top',
      });
      expect(result.success).toBe(false);
    });

    test('rejects bevel effect with invalid style', () => {
      const result = BevelEffectSchema.safeParse({
        type: 'bevel',
        enabled: true,
        style: 'invalid',
        depth: 3,
        size: 2,
        angle: 120,
        highlightColor: { r: 1, g: 1, b: 1, a: 1 },
        shadowColor: { r: 0, g: 0, b: 0, a: 0.5 },
      });
      expect(result.success).toBe(false);
    });

    test('rejects gradient color with position outside [0,1]', () => {
      const result = GradientEffectSchema.safeParse({
        type: 'gradient',
        enabled: true,
        gradientType: 'linear',
        angle: 90,
        colors: [
          { r: 0, g: 0, b: 0, a: 1, position: 2 },
          { r: 1, g: 1, b: 1, a: 1, position: 1 },
        ],
        degraded: false,
      });
      expect(result.success).toBe(false);
    });
  });
});

// ---------------------------------------------------------------------------
// TextEffectSchema union
// ---------------------------------------------------------------------------
describe('TextEffectSchema union', () => {
  test('accepts each effect type wrapped in TextStyles effects array', () => {
    const effects = [
      {
        type: 'stroke' as const,
        enabled: true,
        color: { r: 0, g: 0, b: 0, a: 1 },
        width: 2,
        position: 'outside' as const,
      },
      {
        type: 'dropShadow' as const,
        enabled: true,
        color: { r: 0, g: 0, b: 0, a: 0.5 },
        offsetX: 2,
        offsetY: 2,
        blur: 4,
      },
      {
        type: 'gradient' as const,
        enabled: true,
        gradientType: 'linear' as const,
        angle: 90,
        colors: [
          { r: 0, g: 0, b: 0, a: 1, position: 0 },
          { r: 1, g: 1, b: 1, a: 1, position: 1 },
        ],
        degraded: false,
      },
    ];

    const result = TextStylesSchema.safeParse({
      fontSize: 36,
      color: { r: 1, g: 1, b: 1, a: 1 },
      fontName: 'Arial',
      fontStyle: { bold: true, italic: false },
      alignment: { horizontal: 'center', vertical: 'middle' },
      effects,
    });
    expect(result.success).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Group 5: LayerConfigSchema integration
// ---------------------------------------------------------------------------
describe('LayerConfigSchema integration', () => {
  test('accepts LayerConfig with textStyles field present', () => {
    const result = LayerConfigSchema.safeParse({
      id: 'layer_1',
      name: 'Title',
      type: 'text',
      bounds: { x: 0, y: 0, width: 200, height: 50 },
      visible: true,
      opacity: 1,
      textStyles: {
        fontSize: 24,
        color: { r: 1, g: 1, b: 1, a: 1 },
        fontName: 'Arial',
        fontStyle: { bold: true, italic: false },
        alignment: { horizontal: 'center', vertical: 'middle' },
      },
    });
    expect(result.success).toBe(true);
  });

  test('accepts LayerConfig without textStyles field (optional)', () => {
    const result = LayerConfigSchema.safeParse({
      id: 'layer_1',
      name: 'Background',
      type: 'image',
      bounds: { x: 0, y: 0, width: 1920, height: 1080 },
      visible: true,
      opacity: 1,
    });
    expect(result.success).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// RGBASchema boundary tests
// ---------------------------------------------------------------------------
describe('RGBASchema', () => {
  test('accepts boundary values 0.0 and 1.0', () => {
    expect(RGBASchema.safeParse({ r: 0, g: 0, b: 0, a: 0 }).success).toBe(true);
    expect(RGBASchema.safeParse({ r: 1, g: 1, b: 1, a: 1 }).success).toBe(true);
  });

  test('rejects negative alpha', () => {
    expect(RGBASchema.safeParse({ r: 0, g: 0, b: 0, a: -0.1 }).success).toBe(false);
  });
});
