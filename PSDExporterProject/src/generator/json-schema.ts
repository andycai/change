import { z } from 'zod';

export const RectSchema = z.object({
  x: z.number(),
  y: z.number(),
  width: z.number(),
  height: z.number(),
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
