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
