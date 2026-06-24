import { ComponentType } from './component-types';

/**
 * 标签解析器 - 兼容 PSD2UGUI-LayerTagMenu.jsx 标签体系
 */
export class TagParser {
  // PSD2UGUI 标签映射表
  private static readonly TAG_MAP: Record<string, ComponentType> = {
    btn: 'Button',
    txt: 'Text',
    img: 'Image',
    sv: 'ScrollView',
    ipt: 'InputField',
    vbox: 'VerticalLayoutGroup',
    hbox: 'HorizontalLayoutGroup',
    grid: 'GridLayoutGroup',
  };

  /**
   * 解析图层名标签，返回组件类型或 null
   */
  parse(layerName: string): ComponentType | null {
    // 标签格式：<tag>_<name>，例如 btn_close, txt_title
    const parts = layerName.split('_');
    if (parts.length < 2) {
      return null;
    }

    const tag = parts[0].toLowerCase();
    return TagParser.TAG_MAP[tag] || null;
  }

  /**
   * 检查图层名是否包含有效标签
   */
  hasTag(layerName: string): boolean {
    return this.parse(layerName) !== null;
  }
}
