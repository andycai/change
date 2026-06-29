import type { LayerTextData } from 'ag-psd';
import type { TextStyles, RGBA } from './layer-tree';

export class TextStyleExtractor {
  /**
   * 从 ag-psd 的 LayerTextData 提取文本样式
   */
  extract(textData: LayerTextData | undefined): TextStyles | null {
    if (!textData || !textData.text) {
      return this.getDefaultTextStyles();
    }

    return {
      fontSize: this.convertFontSize(textData.style?.fontSize || 12),
      color: this.convertColor(textData.style?.fillColor),
      fontName: textData.style?.font?.name || 'Arial',
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

  private getDefaultTextStyles(): TextStyles {
    return {
      fontSize: 12,
      color: { r: 0, g: 0, b: 0, a: 1 },
      fontName: 'Arial',
      fontStyle: { bold: false, italic: false },
      alignment: { horizontal: 'left', vertical: 'top' }
    };
  }

  private convertFontSize(psdFontSize: number): number {
    // 初始缩放因子 1.0，通过实际测试确定
    const scaleFactor = 1.0;
    return psdFontSize * scaleFactor;
  }

  private convertColor(_color: any): RGBA {
    // 将在任务 5 中实现
    return { r: 0, g: 0, b: 0, a: 1 };
  }

  private convertAlignment(
    _justification: any,
    _shapeType: any
  ): TextStyles['alignment'] {
    // 将在任务 5 中实现
    return { horizontal: 'left', vertical: 'top' };
  }
}
