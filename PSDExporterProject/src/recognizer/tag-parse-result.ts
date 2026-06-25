/**
 * 标签解析结果
 *
 * 包含解析后的前缀、基础名称和 4-family 分类标签。
 * TagParser 将图层名解析为此中间类型，ComponentRecognizer 再将其映射为 ComponentInfo。
 */
export interface TagParseResult {
  /** 资源引用前缀 (ref 或 refp) */
  prefix?: 'ref' | 'refp';

  /** 图层基础名称（去除标签后的部分） */
  baseName: string;

  /** 4-family 分类标签 */
  families: {
    /** 主组件类型 (bt, img, txt, etc.) */
    main?: string;

    /** 文本后端 (tmp, ugui) */
    textBackend?: string;

    /** 图片类型 (simple, sliced, tiled, filled) */
    imageType?: string;

    /** 角色标签 (bg, press, placeholder, etc.) */
    role?: string;
  };
}
