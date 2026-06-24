export type ComponentType =
  | 'Button'
  | 'Image'
  | 'Text'
  | 'ScrollView'
  | 'InputField'
  | 'VerticalLayoutGroup'
  | 'HorizontalLayoutGroup'
  | 'GridLayoutGroup'
  | 'Unknown';

export interface ComponentInfo {
  type: ComponentType;
  confidence: number; // 置信度 0-1
  source: 'tag' | 'cv' | 'ai'; // 识别来源
  needsReview: boolean; // 是否需要人工审查
}
