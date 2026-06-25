// src/parser/asset-exporter.ts
import { mkdir } from 'fs/promises';
import { existsSync } from 'fs';
import { join } from 'path';
import sharp from 'sharp';
import { Layer } from './layer-tree';

export class AssetExporter {
  /**
   * Export a layer's canvas pixel data to a PNG file using sharp.
   * The layer must have a `_canvas` property set by the PSD parser.
   */
  async export(layer: Layer & { _canvas?: unknown }, outputDir: string): Promise<string> {
    // Ensure output directory exists
    if (!existsSync(outputDir)) {
      await mkdir(outputDir, { recursive: true });
    }

    // Generate filename: <sanitized-name>_<layer-id>.png
    const sanitizedName = this.sanitizeFileName(layer.name);
    const fileName = `${sanitizedName}_${layer.id}.png`;
    const outputPath = join(outputDir, fileName);

    const canvas = layer._canvas as { toBuffer?: (format: string) => Buffer; width?: number; height?: number } | null | undefined;

    if (!canvas || typeof canvas.toBuffer !== 'function') {
      throw new Error(`Layer "${layer.id}" has no canvas data to export`);
    }

    const width = canvas.width ?? layer.bounds.width;
    const height = canvas.height ?? layer.bounds.height;

    if (!width || !height) {
      throw new Error(`Layer "${layer.id}" has zero dimensions (${width}x${height})`);
    }

    // Get raw RGBA pixel data from the canvas
    const pngBuffer = canvas.toBuffer('image/png');

    await (sharp as unknown as (input: Buffer) => { toFile: (path: string) => Promise<unknown> })(pngBuffer).toFile(outputPath);

    return outputPath;
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
