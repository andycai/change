// src/parser/asset-exporter.ts
import { mkdir } from 'fs/promises';
import { existsSync } from 'fs';
import { join } from 'path';
import { Layer } from './layer-tree';

export class AssetExporter {
  /**
   * Export a single layer as a PNG file.
   *
   * Only `image` and `shape` layer types are supported.
   *
   * @throws {Error} Always throws "Not implemented" -- pixel extraction
   *   and sharp export will be added in task 4.
   */
  async export(layer: Layer, outputDir: string): Promise<string> {
    if (layer.type !== 'image' && layer.type !== 'shape') {
      throw new Error(`Cannot export layer type: ${layer.type}`);
    }

    // Ensure output directory exists
    if (!existsSync(outputDir)) {
      await mkdir(outputDir, { recursive: true });
    }

    // Generate filename: <sanitized-name>_<layer-id>.png
    const sanitizedName = this.sanitizeFileName(layer.name);
    const fileName = `${sanitizedName}_${layer.id}.png`;
    const outputPath = join(outputDir, fileName);

    throw new Error(
      `Not implemented: pixel extraction and sharp export for layer "${layer.id}". ` +
        `Intended output path: ${outputPath}. ` +
        'This will be implemented in task 4.',
    );
  }

  /**
   * Sanitize a filename by replacing special characters and limiting length.
   */
  public sanitizeFileName(name: string): string {
    return name
      .replace(/[<>:"/\\|?*]/g, '_') // Replace special characters
      .replace(/\s+/g, '_')           // Replace whitespace
      .substring(0, 50);              // Limit length
  }
}
