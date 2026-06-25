import { z } from 'zod';
import * as fs from 'fs';

/** 标签定义结构 */
const TagDefSchema = z.object({
  id: z.string(),
  label: z.string(),
});

/** 配置文件结构 */
const TagConfigSchema = z.object({
  version: z.string(),
  canonicalOrder: z.array(z.string()),
  families: z.object({
    main: z.array(TagDefSchema),
    textBackend: z.array(TagDefSchema),
    imageType: z.array(TagDefSchema),
    role: z.array(TagDefSchema),
  }),
});

export type TagConfig = z.infer<typeof TagConfigSchema>;
export type TagDef = z.infer<typeof TagDefSchema>;

/**
 * 标签配置加载器
 * 负责从 JSON 文件加载并校验标签配置
 */
export class TagConfigLoader {
  /**
   * 从文件路径加载配置
   * @param path 配置文件路径
   * @returns 校验通过的配置对象
   * @throws 文件不存在或格式错误时抛出异常
   */
  static load(path: string): TagConfig {
    if (!fs.existsSync(path)) {
      throw new Error(`Config file not found: ${path}`);
    }

    const content = fs.readFileSync(path, 'utf-8');
    let data: unknown;

    try {
      data = JSON.parse(content);
    } catch (error) {
      throw new Error(
        `Failed to parse config file: ${error instanceof Error ? error.message : String(error)}`
      );
    }

    return TagConfigLoader.validate(data);
  }

  /**
   * 校验配置数据
   * @param data 待校验的数据
   * @returns 校验通过的配置对象
   * @throws 数据格式错误时抛出 zod 异常
   */
  static validate(data: unknown): TagConfig {
    return TagConfigSchema.parse(data);
  }
}
