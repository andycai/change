// src/parser/psd-parser.ts
import { readPsd, initializeCanvas, Psd, Layer as PsdLayer, LayerTextData, LayerEffectsInfo } from 'ag-psd';
import { createCanvas } from 'canvas';
import { readFile } from 'fs/promises';
import { Layer, LayerTree, PsdMetadata, LayerType, Rect } from './layer-tree';
import { AssetExporter } from './asset-exporter';
import { TextStyleExtractor } from './text-style-extractor';

// Initialize canvas for ag-psd to use when decoding image data.
// ag-psd's initializeCanvas expects a (width, height) => HTMLCanvasElement-like factory;
// the `canvas` package's createCanvas satisfies that shape at runtime in Node.
initializeCanvas(createCanvas as unknown as Parameters<typeof initializeCanvas>[0]);

/**
 * Internal shape that both ag-psd's `Psd` and `Layer` types satisfy.
 *
 * `Psd` provides `width`/`height` (canvas bounds) but no `left`/`top`/…/`opacity`.
 * `Layer` provides `left`/`top`/`right`/`bottom`/`hidden`/`opacity` but no `width`/`height`.
 * Both extend `LayerAdditionalInfo` (name, text, imageData, children).
 * ag-psd also attaches a `canvas` (node-canvas Canvas) with the rasterized
 * pixel data for image/shape layers when `initializeCanvas` has been set up.
 */
interface PsdNode {
  name?: string;
  left?: number;
  top?: number;
  right?: number;
  bottom?: number;
  width?: number;
  height?: number;
  hidden?: boolean;
  opacity?: number;
  text?: unknown;
  imageData?: unknown;
  canvas?: unknown;
  effects?: LayerEffectsInfo;
  children?: PsdNode[];
}

export class PsdParser {
  private assetExporter = new AssetExporter();
  private textStyleExtractor = new TextStyleExtractor();

  /**
   * Parse a PSD file and return a LayerTree.
   *
   * @param psdPath Path to the source .psd file.
   * @param assetsDir Optional directory to export rasterized image/shape
   *   layers to as PNG files. When provided, each exportable layer's
   *   `assetPath` is populated with the written file path.
   */
  async parse(psdPath: string, assetsDir?: string): Promise<LayerTree> {
    const buffer = await readFile(psdPath);

    let psd: Psd;
    try {
      psd = readPsd(buffer);
    } catch (cause) {
      throw new Error(
        `Failed to parse PSD file: ${psdPath}`,
        { cause },
      );
    }

    if (!psd) {
      throw new Error(`Failed to parse PSD file: ${psdPath}`);
    }

    const metadata: PsdMetadata = {
      psdPath,
      canvasSize: {
        x: 0,
        y: 0,
        width: psd.width || 0,
        height: psd.height || 0,
      },
      timestamp: new Date().toISOString(),
    };

    const root = await this.convertLayer(psd as unknown as PsdNode, 'root', assetsDir);

    return {
      root,
      metadata,
    };
  }

  /**
   * Recursively convert an ag-psd layer (or Psd root) into a Layer structure.
   *
   * The Psd root has `width`/`height` but no `left`/`top`/`right`/`bottom`,
   * while child Layer nodes have `left`/`top`/`right`/`bottom`.
   */
  private async convertLayer(node: PsdNode, layerId: string, assetsDir?: string): Promise<Layer> {
    const bounds: Rect = {
      x: node.left || 0,
      y: node.top || 0,
      width:
        (node.right ?? node.width ?? 0) - (node.left || 0),
      height:
        (node.bottom ?? node.height ?? 0) - (node.top || 0),
    };

    const layer: Layer = {
      id: layerId,
      name: node.name || 'Unnamed',
      type: this.determineLayerType(node),
      bounds,
      visible: node.hidden !== true,
      opacity:
        node.opacity !== undefined ? node.opacity : 1.0,
    };

    // If this is a text layer, extract text styles (including effects)
    if (node.text) {
      layer.textStyles = this.textStyleExtractor.extract(
        node.text as LayerTextData,
        node.effects,
      );
    }

    // Export rasterized pixel data for image/shape layers when requested
    if (assetsDir && (layer.type === 'image' || layer.type === 'shape') && node.canvas) {
      try {
        const exportable = layer as Layer & { _canvas?: unknown };
        exportable._canvas = node.canvas;
        layer.assetPath = await this.assetExporter.export(exportable, assetsDir);
      } catch (err) {
        // Leave assetPath unset if export fails (e.g. zero-size or no canvas)
        console.error(`[AssetExport] failed for layer "${layer.id}" (${layer.name}): ${err}`);
      }
    }

    // Process child layers
    if (node.children && node.children.length > 0) {
      layer.children = await Promise.all(
        node.children.map(
          (child, index) =>
            this.convertLayer(child, `${layerId}_${index}`, assetsDir),
        ),
      );
    }

    return layer;
  }

  /**
   * Determine the type of a layer based on its properties.
   */
  private determineLayerType(node: PsdNode): LayerType {
    if (node.children && node.children.length > 0) {
      return 'group';
    }
    if (node.text) {
      return 'text';
    }
    if (node.imageData) {
      return 'image';
    }
    return 'shape';
  }
}
