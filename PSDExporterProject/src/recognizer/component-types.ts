export type ComponentType =
  | 'Button'
  | 'Image'
  | 'RawImage'
  | 'Text'
  | 'ScrollView'
  | 'InputField'
  | 'Dropdown'
  | 'Toggle'
  | 'Slider'
  | 'Mask'
  | 'FillColor'
  | 'VerticalLayoutGroup'
  | 'HorizontalLayoutGroup'
  | 'GridLayoutGroup'
  | 'Unknown';

export interface ComponentInfo {
  type: ComponentType;
  textBackend?: 'tmp' | 'ugui';
  imageType?: 'simple' | 'sliced' | 'tiled' | 'filled';
  role?: string;
  confidence: number; // 置信度 0-1
  source: 'tag' | 'cv' | 'ai'; // 识别来源
  needsReview: boolean; // 是否需要人工审查
}
