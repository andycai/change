/**
 * 图层类型
 */
export type LayerType = 'group' | 'image' | 'text' | 'shape';

/**
 * RGBA 颜色
 */
export interface RGBA {
  r: number;
  g: number;
  b: number;
  a: number;
}

/**
 * 矩形区域
 */
export interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

/**
 * 图层节点
 */
export interface Layer {
  id: string; // 唯一标识符
  name: string; // 图层名称
  type: LayerType; // 图层类型
  bounds: Rect; // 位置和大小
  visible: boolean; // 可见性
  opacity: number; // 不透明度 0-1
  children?: Layer[]; // 子图层（仅 group 类型）
  assetPath?: string; // 导出的图片路径（仅 image/shape 类型）
  textStyles?: TextStyles; // 文本样式（仅 text 类型）
}

/**
 * PSD 元数据
 */
export interface PsdMetadata {
  psdPath: string;
  canvasSize: Rect;
  timestamp: string;
}

/**
 * 图层树
 */
export interface LayerTree {
  root: Layer;
  metadata: PsdMetadata;
}

/**
 * 文本样式
 */
export interface TextStyles {
  fontSize: number;
  color: RGBA;
  fontName: string;
  fontStyle: { bold: boolean; italic: boolean };
  alignment: { horizontal: 'left' | 'center' | 'right' | 'justify'; vertical: 'top' | 'middle' | 'bottom' };
  effects?: TextEffect[];
}

/**
 * 文本效果基础接口
 */
export interface TextEffect {
  type: 'stroke' | 'dropShadow' | 'innerShadow' | 'gradient' | 'outerGlow' | 'bevel';
  enabled: boolean;
}

/**
 * 描边效果
 */
export interface StrokeEffect extends TextEffect {
  type: 'stroke';
  color: RGBA;
  width: number;
  position: 'outside' | 'inside' | 'center';
}

/**
 * 阴影效果（投影/内阴影）
 */
export interface ShadowEffect extends TextEffect {
  type: 'dropShadow' | 'innerShadow';
  color: RGBA;
  offsetX: number;
  offsetY: number;
  blur: number;
}

/**
 * 渐变效果
 */
export interface GradientEffect extends TextEffect {
  type: 'gradient';
  gradientType: 'linear' | 'radial';
  angle: number;
  colors: Array<RGBA & { position: number }>;
  degraded: boolean;
}

/**
 * 外发光效果
 */
export interface GlowEffect extends TextEffect {
  type: 'outerGlow';
  color: RGBA;
  size: number;
  spread: number;
}

/**
 * 斜面/浮雕效果
 */
export interface BevelEffect extends TextEffect {
  type: 'bevel';
  style: 'innerBevel' | 'outerBevel' | 'emboss' | 'pillowEmboss' | 'strokeEmboss';
  depth: number;
  size: number;
  angle: number;
  highlightColor: RGBA;
  shadowColor: RGBA;
}
