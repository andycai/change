import { z } from 'zod';

export const RectSchema = z.object({
  x: z.number(),
  y: z.number(),
  width: z.number(),
  height: z.number(),
});

/** RGBA 颜色 */
export const RGBASchema = z.object({
  r: z.number().min(0).max(1),
  g: z.number().min(0).max(1),
  b: z.number().min(0).max(1),
  a: z.number().min(0).max(1),
});

/** 描边效果 */
export const StrokeEffectSchema = z.object({
  type: z.literal('stroke'),
  enabled: z.boolean(),
  color: RGBASchema,
  width: z.number(),
  position: z.enum(['outside', 'inside', 'center']),
});

/** 阴影效果（投影/内阴影） */
export const ShadowEffectSchema = z.object({
  type: z.enum(['dropShadow', 'innerShadow']),
  enabled: z.boolean(),
  color: RGBASchema,
  offsetX: z.number(),
  offsetY: z.number(),
  blur: z.number(),
});

/** 渐变效果 */
export const GradientEffectSchema = z.object({
  type: z.literal('gradient'),
  enabled: z.boolean(),
  gradientType: z.enum(['linear', 'radial']),
  angle: z.number(),
  colors: z.array(RGBASchema.extend({ position: z.number() })),
  degraded: z.boolean(),
});

/** 外发光效果 */
export const GlowEffectSchema = z.object({
  type: z.literal('outerGlow'),
  enabled: z.boolean(),
  color: RGBASchema,
  size: z.number(),
  spread: z.number(),
});

/** 斜面/浮雕效果 */
export const BevelEffectSchema = z.object({
  type: z.literal('bevel'),
  enabled: z.boolean(),
  style: z.enum(['innerBevel', 'outerBevel', 'emboss', 'pillowEmboss', 'strokeEmboss']),
  depth: z.number(),
  size: z.number(),
  angle: z.number(),
  highlightColor: RGBASchema,
  shadowColor: RGBASchema,
});

/**
 * 文本效果联合类型
 * 使用 z.union 而非 z.discriminatedUnion，因为 ShadowEffect 的 type 字段
 * 包含两个字面量值（'dropShadow' | 'innerShadow'），无法用单一 discriminator 区分
 */
export const TextEffectSchema = z.union([
  StrokeEffectSchema,
  ShadowEffectSchema,
  GradientEffectSchema,
  GlowEffectSchema,
  BevelEffectSchema,
]);

/** 文本样式 */
export const TextStylesSchema = z.object({
  fontSize: z.number(),
  color: RGBASchema,
  fontName: z.string(),
  fontStyle: z.object({
    bold: z.boolean(),
    italic: z.boolean(),
  }),
  alignment: z.object({
    horizontal: z.enum(['left', 'center', 'right', 'justify']),
    vertical: z.enum(['top', 'middle', 'bottom']),
  }),
  effects: z.array(TextEffectSchema).optional(),
});

export const LayerConfigSchema: z.ZodType<any> = z.lazy(() =>
  z.object({
    id: z.string(),
    name: z.string(),
    type: z.enum(['group', 'image', 'text', 'shape']),
    bounds: RectSchema,
    visible: z.boolean(),
    opacity: z.number().min(0).max(1),
    children: z.array(LayerConfigSchema).optional(),
    assetPath: z.string().optional(),
    textStyles: TextStylesSchema.optional(),
  })
);

export const ComponentInfoSchema = z.object({
  type: z.enum([
    'Button', 'Image', 'RawImage', 'Text', 'ScrollView', 'InputField',
    'Dropdown', 'Toggle', 'Slider', 'Mask', 'FillColor',
    'VerticalLayoutGroup', 'HorizontalLayoutGroup', 'GridLayoutGroup', 'Unknown',
  ]),
  textBackend: z.enum(['tmp', 'ugui']).optional(),
  imageType: z.enum(['simple', 'sliced', 'tiled', 'filled']).optional(),
  role: z.string().optional(),
  confidence: z.number().min(0).max(1),
  source: z.enum(['tag', 'cv', 'ai']),
  needsReview: z.boolean(),
});

export const LayerComponentMappingSchema = z.object({
  layerId: z.string(),
  component: ComponentInfoSchema,
});

export const MetadataSchema = z.object({
  psdPath: z.string(),
  canvasSize: RectSchema,
  timestamp: z.string(),
  generatedAt: z.string(),
  generator: z.string(),
  version: z.string(),
});

export const JsonConfigSchema = z.object({
  metadata: MetadataSchema,
  layers: z.array(LayerConfigSchema),
  components: z.array(LayerComponentMappingSchema),
});

export type JsonConfig = z.infer<typeof JsonConfigSchema>;
export type LayerConfig = z.infer<typeof LayerConfigSchema>;
export type LayerComponentMapping = z.infer<typeof LayerComponentMappingSchema>;
export type JsonMetadata = z.infer<typeof MetadataSchema>;
