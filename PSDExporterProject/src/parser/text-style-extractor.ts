import type { Color, Justification } from 'ag-psd';
import type { LayerTextData } from 'ag-psd';
import type { TextStyles, RGBA } from './layer-tree';
import convert from 'color-convert';

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
   * 将 ag-psd 的 Color 联合类型转换为内部 RGBA 格式（归一化到 0-1 区间）。
   *
   * 支持的颜色空间：
   * - RGBA / RGB：直接取 r、g、b 分量，除以 255 归一化；
   *   alpha 取自 a 分量除以 255，若未提供则默认为 1
   * - CMYK：通过 color-convert 库转换为 RGB 后归一化，alpha 固定为 1
   * - Lab：通过 color-convert 库转换为 RGB 后归一化，alpha 固定为 1
   *
   * 不支持的颜色空间（FRGB、HSB、Grayscale）将输出警告并返回默认颜色。
   *
   * @param color - ag-psd 中的颜色值，可能为 undefined
   * @returns 转换后的 RGBA 颜色对象（各分量 0-1）
   */
  private convertColor(color: Color | undefined): RGBA {
    if (color === undefined || color === null) {
      return { ...TextStyleExtractor.DEFAULT_COLOR };
    }

    try {
      // Lab 颜色空间：具有 l、a、b 三个键
      // 注意：必须先检测 Lab，因为其 a 键与 RGBA 的 a 键在 in 检查中会产生歧义
      if ('l' in color && 'a' in color && 'b' in color) {
        const [r, g, b] = convert.lab.rgb([color.l, color.a, color.b]);
        return { r: r / 255, g: g / 255, b: b / 255, a: 1 };
      }

      // RGBA / RGB 颜色空间：具有 r、g、b 三个键
      if ('r' in color && 'g' in color && 'b' in color) {
        const a = 'a' in color ? (color as { a: number }).a / 255 : 1;
        return { r: color.r / 255, g: color.g / 255, b: color.b / 255, a };
      }

      // CMYK 颜色空间：具有 c、m、y、k 四个键
      if ('c' in color && 'm' in color && 'y' in color && 'k' in color) {
        const [r, g, b] = convert.cmyk.rgb([
          color.c * 100,
          color.m * 100,
          color.y * 100,
          color.k * 100
        ]);
        return { r: r / 255, g: g / 255, b: b / 255, a: 1 };
      }

      // 不支持的颜色空间
      console.warn(
        `[TextStyleExtractor] 不支持的颜色空间，已回退为默认颜色：${JSON.stringify(color)}`
      );
      return { ...TextStyleExtractor.DEFAULT_COLOR };
    } catch (error) {
      console.error(
        `[TextStyleExtractor] 颜色转换失败，已回退为默认颜色：${error}`
      );
      return { ...TextStyleExtractor.DEFAULT_COLOR };
    }
  }

  /**
   * 转换文本对齐方式
   *
   * 将 ag-psd 的 Justification（段落对齐）和 shapeType（文本形状类型）
   * 映射为内部的水平/垂直对齐格式。
   *
   * 水平映射规则：
   * - 'center' → 'center'
   * - 'right' → 'right'
   * - 'justify-left'/'justify-right'/'justify-center'/'justify-all' → 'justify'
   * - 'left' 或 undefined / 未知值 → 'left'
   *
   * 垂直映射规则：
   * - shapeType 为 'box' 时 → 'middle'（文本框内垂直居中）
   * - 其他情况（'point' 或 undefined）→ 'top'
   *
   * @param justification - PSD 段落对齐类型，可能为 undefined
   * @param shapeType - PSD 文本形状类型（'point' | 'box'），可能为 undefined
   * @returns 转换后的对齐方式对象
   */
  private convertAlignment(
    justification: Justification | undefined,
    shapeType: 'point' | 'box' | undefined
  ): TextStyles['alignment'] {
    let horizontal: TextStyles['alignment']['horizontal'] = 'left';

    if (justification) {
      switch (justification) {
        case 'center':
          horizontal = 'center';
          break;
        case 'right':
          horizontal = 'right';
          break;
        case 'justify-left':
        case 'justify-right':
        case 'justify-center':
        case 'justify-all':
          horizontal = 'justify';
          break;
        case 'left':
        default:
          horizontal = 'left';
          break;
      }
    }

    const vertical: TextStyles['alignment']['vertical'] =
      shapeType === 'box' ? 'middle' : 'top';

    return { horizontal, vertical };
  }
}
