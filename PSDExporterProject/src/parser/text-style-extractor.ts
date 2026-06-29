import type { Color, Justification } from 'ag-psd';
import type { LayerTextData, LayerEffectsInfo } from 'ag-psd';
import type {
  TextStyles,
  RGBA,
  TextEffect,
  StrokeEffect,
  ShadowEffect,
  GradientEffect,
  GlowEffect,
  BevelEffect,
} from './layer-tree';
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

  /** 颜色通道最大值（用于 0-255 到 0-1 的归一化） */
  private static readonly MAX_COLOR_CHANNEL = 255;

  /** CMYK 百分比缩放因子（0-1 小数转 0-100 百分比） */
  private static readonly CMYK_PERCENT_SCALE = 100;

  /** 默认 alpha 值（完全不透明） */
  private static readonly DEFAULT_ALPHA = 1;

  /**
   * 从 ag-psd 的 LayerTextData 提取文本样式
   *
   * 当 textData 缺失或 textData.text 为空时，返回默认文本样式，
   * 确保调用方始终能获得可用的 TextStyles 对象。
   *
   * @param textData - ag-psd 解析出的文本图层数据，可能为 undefined
   * @param effects - ag-psd 解析出的图层效果信息，可能为 undefined
   * @returns 提取到的文本样式，或默认样式（不会返回 null）
   */
  extract(textData: LayerTextData | undefined, effects?: LayerEffectsInfo): TextStyles {
    if (!textData || !textData.text) {
      return this.getDefaultTextStyles();
    }

    const baseStyles: TextStyles = {
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

    if (effects) {
      const extracted = this.extractEffects(effects);
      if (extracted.effects.length > 0) {
        baseStyles.effects = extracted.effects;
      }
      if (extracted.warnings.length > 0) {
        for (const warning of extracted.warnings) {
          console.warn(`[TextStyleExtractor] ${warning}`);
        }
      }
    }

    return baseStyles;
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
   * 将 RGB 分量值归一化到 0-1 区间
   *
   * @param r - 红色分量（0-255）
   * @param g - 绿色分量（0-255）
   * @param b - 蓝色分量（0-255）
   * @param a - alpha 值（0-1），默认为完全不透明
   * @returns 归一化后的 RGBA 颜色对象
   */
  private normalizeRgb(r: number, g: number, b: number, a: number): RGBA {
    return {
      r: r / TextStyleExtractor.MAX_COLOR_CHANNEL,
      g: g / TextStyleExtractor.MAX_COLOR_CHANNEL,
      b: b / TextStyleExtractor.MAX_COLOR_CHANNEL,
      a
    };
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
        return this.normalizeRgb(r, g, b, TextStyleExtractor.DEFAULT_ALPHA);
      }

      // RGBA / RGB 颜色空间：具有 r、g、b 三个键
      if ('r' in color && 'g' in color && 'b' in color) {
        const a = 'a' in color
          ? (color as { a: number }).a / TextStyleExtractor.MAX_COLOR_CHANNEL
          : TextStyleExtractor.DEFAULT_ALPHA;
        return this.normalizeRgb(color.r, color.g, color.b, a);
      }

      // CMYK 颜色空间：具有 c、m、y、k 四个键
      if ('c' in color && 'm' in color && 'y' in color && 'k' in color) {
        const [r, g, b] = convert.cmyk.rgb([
          color.c * TextStyleExtractor.CMYK_PERCENT_SCALE,
          color.m * TextStyleExtractor.CMYK_PERCENT_SCALE,
          color.y * TextStyleExtractor.CMYK_PERCENT_SCALE,
          color.k * TextStyleExtractor.CMYK_PERCENT_SCALE
        ]);
        return this.normalizeRgb(r, g, b, TextStyleExtractor.DEFAULT_ALPHA);
      }

      // 不支持的颜色空间
      console.warn(
        `[TextStyleExtractor] 不支持的颜色空间，已回退为默认颜色：${JSON.stringify(color)}`
      );
      return { ...TextStyleExtractor.DEFAULT_COLOR };
    } catch (error) {
      console.error(
        `[TextStyleExtractor] 颜色转换失败，已回退为默认颜色：${
          error instanceof Error ? error.message : String(error)
        }`
      );
      return { ...TextStyleExtractor.DEFAULT_COLOR };
    }
  }

  /**
   * 提取图层效果
   *
   * 处理 6 种效果类型：stroke、dropShadow、innerShadow、gradientOverlay、
   * outerGlow、bevel。仅提取 enabled === true 的效果。
   *
   * @param effectsInfo - ag-psd 解析出的图层效果信息
   * @returns 提取到的效果列表和警告信息
   */
  private extractEffects(effectsInfo: LayerEffectsInfo): {
    effects: TextEffect[];
    warnings: string[];
  } {
    const effects: TextEffect[] = [];
    const warnings: string[] = [];

    // stroke（数组，取每个 enabled 的项）
    if (effectsInfo.stroke) {
      for (const stroke of effectsInfo.stroke) {
        if (stroke.enabled) {
          effects.push({
            type: 'stroke',
            enabled: true,
            color: this.convertColor(stroke.color),
            width: stroke.size?.value ?? 1,
            position: stroke.position ?? 'outside',
          } as StrokeEffect);
        }
      }
    }

    // dropShadow（数组，取每个 enabled 的项）
    if (effectsInfo.dropShadow) {
      for (const shadow of effectsInfo.dropShadow) {
        if (shadow.enabled) {
          const distance = shadow.distance?.value ?? 0;
          const angle = shadow.angle ?? 0;
          const angleRad = (angle * Math.PI) / 180;
          effects.push({
            type: 'dropShadow',
            enabled: true,
            color: this.convertColor(shadow.color),
            offsetX: distance * Math.cos(angleRad),
            offsetY: -distance * Math.sin(angleRad),
            blur: shadow.size?.value ?? 0,
          } as ShadowEffect);
        }
      }
    }

    // innerShadow（数组，取每个 enabled 的项）
    if (effectsInfo.innerShadow) {
      for (const shadow of effectsInfo.innerShadow) {
        if (shadow.enabled) {
          const distance = shadow.distance?.value ?? 0;
          const angle = shadow.angle ?? 0;
          const angleRad = (angle * Math.PI) / 180;
          effects.push({
            type: 'innerShadow',
            enabled: true,
            color: this.convertColor(shadow.color),
            offsetX: distance * Math.cos(angleRad),
            offsetY: -distance * Math.sin(angleRad),
            blur: shadow.size?.value ?? 0,
          } as ShadowEffect);
        }
      }
    }

    // gradientOverlay（数组，取每个 enabled 的项，经过 degradeGradient 处理）
    if (effectsInfo.gradientOverlay) {
      for (const gradOverlay of effectsInfo.gradientOverlay) {
        if (gradOverlay.enabled) {
          const { gradient: gradEffect, warnings: gradWarnings } =
            this.degradeGradient(gradOverlay);
          effects.push(gradEffect);
          warnings.push(...gradWarnings);
        }
      }
    }

    // outerGlow（单例）
    if (effectsInfo.outerGlow?.enabled) {
      const glow = effectsInfo.outerGlow;
      effects.push({
        type: 'outerGlow',
        enabled: true,
        color: this.convertColor(glow.color),
        size: glow.size?.value ?? 0,
        spread: glow.choke?.value ?? 0,
      } as GlowEffect);
    }

    // bevel（单例）
    if (effectsInfo.bevel?.enabled) {
      const bevel = effectsInfo.bevel;
      effects.push({
        type: 'bevel',
        enabled: true,
        style: this.mapBevelStyle(bevel.style),
        depth: bevel.strength ?? 0,
        size: bevel.size?.value ?? 0,
        angle: bevel.angle ?? 0,
        highlightColor: this.convertColor(bevel.highlightColor),
        shadowColor: this.convertColor(bevel.shadowColor),
      } as BevelEffect);
    }

    return { effects, warnings };
  }

  /**
   * 渐变效果劣化策略
   *
   * 将 PSD 复杂渐变效果简化为 Unity 兼容的线性渐变格式。
   * 劣化规则：
   * - 径向渐变 → 退化到线性竖直方向（angle=90），记录警告
   * - 非标准角度（非 0/90/180/270）→ 吸附到最近的基准方向，记录警告
   * - 超过 2 个色标 → 仅保留首尾色标，记录警告
   * - 所有输出均标记 gradientType='linear'
   *
   * 颜色值从 ag-psd 的 0-255 范围归一化到 0-1。
   *
   * @param gradOverlay - ag-psd 的渐变叠加效果信息
   * @returns 劣化后的渐变效果和警告列表
   */
  private degradeGradient(gradOverlay: any): {
    gradient: GradientEffect;
    warnings: string[];
  } {
    const warnings: string[] = [];
    let degraded = false;
    let angle = gradOverlay.angle ?? 0;

    // 径向渐变 → 退化为线性竖直方向
    if (gradOverlay.type === 'radial') {
      angle = 90;
      degraded = true;
      warnings.push('渐变类型 "radial" 已退化为 linear (angle=90)');
    }

    // 非标准角度 → 吸附到最近的基准方向
    const snapped = this.snapAngleToCardinal(angle);
    if (snapped !== angle) {
      angle = snapped;
      degraded = true;
      warnings.push(`渐变角度已从 ${gradOverlay.angle ?? 0}° 吸附到 ${snapped}°`);
    }

    // 提取色标
    const gradient = gradOverlay.gradient as any;
    let colorStops: any[] = [];
    if (gradient?.colorStops && Array.isArray(gradient.colorStops)) {
      colorStops = gradient.colorStops;
    }

    // 超过 2 个色标 → 仅保留首尾
    let colors: Array<RGBA & { position: number }>;
    if (colorStops.length > 2) {
      degraded = true;
      warnings.push(
        `渐变色标已从 ${colorStops.length} 个简化为 2 个（仅保留首尾）`
      );
      const first = colorStops[0];
      const last = colorStops[colorStops.length - 1];
      colors = [
        {
          ...this.convertColor(first.color),
          position: first.location / TextStyleExtractor.MAX_COLOR_CHANNEL,
        },
        {
          ...this.convertColor(last.color),
          position: last.location / TextStyleExtractor.MAX_COLOR_CHANNEL,
        },
      ];
    } else {
      colors = colorStops.map((stop: any) => ({
        ...this.convertColor(stop.color),
        position: stop.location / TextStyleExtractor.MAX_COLOR_CHANNEL,
      }));
    }

    return {
      gradient: {
        type: 'gradient',
        enabled: true,
        gradientType: 'linear',
        angle,
        colors,
        degraded,
      } as GradientEffect,
      warnings,
    };
  }

  /**
   * 将角度吸附到最近的基准方向（0/90/180/270）
   *
   * 使用象限分段法：每个基准方向覆盖上下 45 度范围，
   * 边界值归属到更大的角度（顺时针方向）。
   *
   * @param angle - 原始角度（度数）
   * @returns 吸附后的角度
   */
  private snapAngleToCardinal(angle: number): number {
    // 归一化到 [0, 360)
    let normalized = angle % 360;
    if (normalized < 0) {
      normalized += 360;
    }

    if (normalized >= 315 || normalized < 45) {
      return 0;
    } else if (normalized >= 45 && normalized < 135) {
      return 90;
    } else if (normalized >= 135 && normalized < 225) {
      return 180;
    } else {
      return 270;
    }
  }

  /**
   * 映射 ag-psd 的 BevelStyle 到内部格式
   *
   * ag-psd 使用带空格的格式（如 'outer bevel'），
   * 内部使用驼峰格式（如 'outerBevel'）。
   *
   * @param style - ag-psd 的斜面样式，可能为 undefined
   * @returns 内部斜面样式格式
   */
  private mapBevelStyle(style: string | undefined): BevelEffect['style'] {
    switch (style) {
      case 'outer bevel':
        return 'outerBevel';
      case 'inner bevel':
        return 'innerBevel';
      case 'emboss':
        return 'emboss';
      case 'pillow emboss':
        return 'pillowEmboss';
      case 'stroke emboss':
        return 'strokeEmboss';
      default:
        return 'innerBevel';
    }
  }

  /**
   * 转换文本对齐方式
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
