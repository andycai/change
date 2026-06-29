import type { Color, Justification } from 'ag-psd';
import type { LayerTextData } from 'ag-psd';
import type { TextStyles, RGBA } from './layer-tree';

export class TextStyleExtractor {
  /** 默认字体大小 */
  private static readonly DEFAULT_FONT_SIZE = 12;

  /** 默认字体名称 */
  private static readonly DEFAULT_FONT_NAME = 'Arial';

  /** 默认颜色（黑色） */
  private static readonly DEFAULT_COLOR: RGBA = { r: 0, g: 0, b: 0, a: 1 };

  /** 默认对齐方式 */
  private static readonly DEFAULT_ALIGNMENT: TextStyles['alignment'] = {
    horizontal: 'left',
    vertical: 'top'
  };

  /**
   * 从 ag-psd 的 LayerTextData 提取文本样式
   *
   * 当 textData 缺失或 textData.text 为空时，返回默认文本样式，
   * 确保调用方始终能获得可用的 TextStyles 对象。
   *
   * @param textData - ag-psd 解析出的文本图层数据，可能为 undefined
   * @returns 提取到的文本样式，或默认样式（不会返回 null）
   */
  extract(textData: LayerTextData | undefined): TextStyles {
    if (!textData || !textData.text) {
      return this.getDefaultTextStyles();
    }

    return {
      fontSize: this.convertFontSize(
        textData.style?.fontSize ?? TextStyleExtractor.DEFAULT_FONT_SIZE
      ),
      color: this.convertColor(textData.style?.fillColor),
      fontName: textData.style?.font?.name ?? TextStyleExtractor.DEFAULT_FONT_NAME,
      fontStyle: {
        bold: textData.style?.fauxBold || false,
        italic: textData.style?.fauxItalic || false
      },
      alignment: this.convertAlignment(
        textData.paragraphStyle?.justification,
        textData.shapeType
      )
    };
  }

  /**
   * 获取默认文本样式
   *
   * 当 PSD 文本图层数据不可用时，返回一套安全的默认样式。
   *
   * @returns 默认的 TextStyles 对象
   */
  private getDefaultTextStyles(): TextStyles {
    return {
      fontSize: TextStyleExtractor.DEFAULT_FONT_SIZE,
      color: { ...TextStyleExtractor.DEFAULT_COLOR },
      fontName: TextStyleExtractor.DEFAULT_FONT_NAME,
      fontStyle: { bold: false, italic: false },
      alignment: { ...TextStyleExtractor.DEFAULT_ALIGNMENT }
    };
  }

  /**
   * 转换字体大小
   *
   * 将 PSD 中的字体大小通过缩放因子转换为目标 UI 系统的字体大小。
   * 当前缩放因子为 1.0，待实际测试后确定准确值。
   *
   * @param psdFontSize - PSD 中记录的字体大小（像素）
   * @returns 转换后的字体大小
   */
  private convertFontSize(psdFontSize: number): number {
    // 初始缩放因子 1.0，通过实际测试确定
    const scaleFactor = 1.0;
    return psdFontSize * scaleFactor;
  }

  /**
   * 转换颜色
   *
   * 将 ag-psd 的 Color 类型转换为内部 RGBA 格式。
   * 当前为桩实现，将在后续任务中完善。
   *
   * @param _color - ag-psd 中的颜色值，可能为 undefined
   * @returns 转换后的 RGBA 颜色对象
   */
  private convertColor(_color: Color | undefined): RGBA {
    // 将在任务 5 中实现
    return { ...TextStyleExtractor.DEFAULT_COLOR };
  }

  /**
   * 转换文本对齐方式
   *
   * 将 ag-psd 的对齐类型和形状类型转换为内部对齐格式。
   * 当前为桩实现，将在后续任务中完善。
   *
   * @param _justification - PSD 段落对齐类型，可能为 undefined
   * @param _shapeType - PSD 文本形状类型（'point' | 'box'），可能为 undefined
   * @returns 转换后的对齐方式对象
   */
  private convertAlignment(
    _justification: Justification | undefined,
    _shapeType: 'point' | 'box' | undefined
  ): TextStyles['alignment'] {
    // 将在任务 5 中实现
    return { ...TextStyleExtractor.DEFAULT_ALIGNMENT };
  }
}
