/**
 * 图层类型
 */
export type LayerType = 'group' | 'image' | 'text' | 'shape';

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
  color: { r: number; g: number; b: number; a: number };
  fontName: string;
  fontStyle: { bold: boolean; italic: boolean };
  alignment: { horizontal: 'left' | 'center' | 'right' | 'justify'; vertical: 'top' | 'middle' | 'bottom' };
  effects?: TextEffect[];
}

export interface TextEffect {
  type: 'stroke' | 'dropShadow' | 'innerShadow' | 'gradient' | 'outerGlow' | 'bevel';
  enabled: boolean;
  [key: string]: any;
}

export interface StrokeEffect extends TextEffect {
  type: 'stroke';
  color: { r: number; g: number; b: number; a: number };
  width: number;
  position: 'outside' | 'inside' | 'center';
}

export interface ShadowEffect extends TextEffect {
  type: 'dropShadow' | 'innerShadow';
  color: { r: number; g: number; b: number; a: number };
  offsetX: number;
  offsetY: number;
  blur: number;
}

export interface GradientEffect extends TextEffect {
  type: 'gradient';
  gradientType: 'linear' | 'radial';
  angle: number;
  colors: Array<{ r: number; g: number; b: number; a: number; position: number }>;
  degraded: boolean;
}

export interface GlowEffect extends TextEffect {
  type: 'outerGlow';
  color: { r: number; g: number; b: number; a: number };
  size: number;
  spread: number;
}

export interface BevelEffect extends TextEffect {
  type: 'bevel';
  style: string;
  depth: number;
  size: number;
  angle: number;
  highlightColor: { r: number; g: number; b: number; a: number };
  shadowColor: { r: number; g: number; b: number; a: number };
}
