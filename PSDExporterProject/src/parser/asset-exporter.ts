// src/parser/asset-exporter.ts
import { mkdirSync, existsSync } from 'fs';
import { join } from 'path';
import { Layer } from './layer-tree';

export class AssetExporter {
  /**
   * Export a single layer as a PNG file.
   *
   * Only `image` and `shape` layer types are supported.
   * Returns the output file path on success.
   */
  async export(layer: Layer, outputDir: string): Promise<string> {
    if (layer.type !== 'image' && layer.type !== 'shape') {
      throw new Error(`Cannot export layer type: ${layer.type}`);
    }

    // Ensure output directory exists
    if (!existsSync(outputDir)) {
      mkdirSync(outputDir, { recursive: true });
    }

    // Generate filename: <sanitized-name>_<layer-id>.png
    const sanitizedName = this.sanitizeFileName(layer.name);
    const fileName = `${sanitizedName}_${layer.id}.png`;
    const outputPath = join(outputDir, fileName);

    // TODO: Extract pixel data from the layer via ag-psd and export with sharp.
    // This will be implemented in task 4 when tests are written.
    return outputPath;
  }

  /**
   * Sanitize a filename by replacing special characters and limiting length.
   */
  private sanitizeFileName(name: string): string {
    return name
      .replace(/[<>:"/\\|?*]/g, '_') // Replace special characters
      .replace(/\s+/g, '_')           // Replace whitespace
      .substring(0, 50);              // Limit length
  }
}
