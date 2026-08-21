// src/parser/psd-parser.ts
import { readPsd, initializeCanvas, Psd, LayerTextData, LayerEffectsInfo, PixelData } from 'ag-psd';
import { createCanvas } from 'canvas';
import { readFile } from 'fs/promises';
import { Layer, LayerTree, PsdMetadata, LayerType, Rect } from './layer-tree';
import { AssetExporter } from './asset-exporter';
import { TextStyleExtractor } from './text-style-extractor';
import { ParsedPsdDocument, RasterMaskSource, RasterSource } from './psd-document';

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
  id?: number;
  name?: string;
  left?: number;
  top?: number;
  right?: number;
  bottom?: number;
  width?: number;
  height?: number;
  hidden?: boolean;
  opacity?: number;
  blendMode?: string;
  clipping?: boolean;
  text?: unknown;
  imageData?: PixelData;
  canvas?: unknown;
  vectorMask?: unknown;
  effects?: LayerEffectsInfo;
  mask?: {
    left?: number;
    top?: number;
    right?: number;
    bottom?: number;
    imageData?: PixelData;
  };
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
    const document = await this.parseDocument(psdPath);

    if (assetsDir) {
      await this.exportAssets(document, assetsDir);
    }

    return document.tree;
  }

  async parseDocument(psdPath: string): Promise<ParsedPsdDocument> {
    const buffer = await readFile(psdPath);

    let psd: Psd;
    try {
      psd = readPsd(buffer, {
        useImageData: true,
        skipCompositeImageData: true,
        skipThumbnail: true,
      });
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

    const rasterSources = new Map<string, RasterSource>();
    const root = this.convertLayer(psd as unknown as PsdNode, 'root', rasterSources);
    return {
      tree: {
        root,
        metadata,
      },
      rasterSources,
    };
  }

  async exportAssets(document: ParsedPsdDocument, assetsDir: string): Promise<void> {
    await this.exportLayerAssets(document.tree.root, document.rasterSources, assetsDir);
  }

  /**
   * Recursively convert an ag-psd layer (or Psd root) into a Layer structure.
   *
   * The Psd root has `width`/`height` but no `left`/`top`/`right`/`bottom`,
   * while child Layer nodes have `left`/`top`/`right`/`bottom`.
   */
  private convertLayer(
    node: PsdNode,
    layerId: string,
    rasterSources: Map<string, RasterSource>,
  ): Layer {
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
      sourceId: node.id,
      name: node.name || 'Unnamed',
      type: this.determineLayerType(node),
      bounds,
      visible: node.hidden !== true,
      opacity:
        node.opacity !== undefined ? node.opacity : 1.0,
      maskType: node.vectorMask ? 'vector' : node.mask?.imageData ? 'raster' : undefined,
      blendMode: node.blendMode,
      clipping: node.clipping,
    };

    // If this is a text layer, extract text styles (including effects)
    if (node.text) {
      const textData = node.text as LayerTextData;
      layer.textStyles = this.textStyleExtractor.extract(textData, node.effects);
      if (typeof textData.text === 'string') {
        layer.text = textData.text;
      }
    }

    if (node.imageData && this.isValidPixelData(node.imageData)) {
      rasterSources.set(layerId, {
        layerId,
        photoshopLayerId: node.id,
        width: node.imageData.width,
        height: node.imageData.height,
        rgba: this.normalizePixelData(node.imageData),
        mask: this.createMaskSource(node.mask),
      });
    }

    // Process child layers
    if (node.children && node.children.length > 0) {
      layer.children = node.children.map(
        (child, index) => this.convertLayer(child, `${layerId}_${index}`, rasterSources),
      );
    }

    return layer;
  }

  private async exportLayerAssets(
    layer: Layer,
    rasterSources: Map<string, RasterSource>,
    assetsDir: string,
  ): Promise<void> {
    const rasterSource = rasterSources.get(layer.id);
    if (rasterSource && (layer.type === 'image' || layer.type === 'shape')) {
      try {
        layer.assetPath = await this.assetExporter.exportRaster(layer, rasterSource, assetsDir);
      } catch (error) {
        console.error(`[AssetExport] failed for layer "${layer.id}" (${layer.name}): ${error}`);
      }
    }

    for (const child of layer.children ?? []) {
      await this.exportLayerAssets(child, rasterSources, assetsDir);
    }
  }

  private isValidPixelData(pixelData: PixelData): boolean {
    return Number.isInteger(pixelData.width)
      && pixelData.width > 0
      && Number.isInteger(pixelData.height)
      && pixelData.height > 0
      && ArrayBuffer.isView(pixelData.data)
      && pixelData.data.BYTES_PER_ELEMENT === 1
      && pixelData.data.byteLength >= pixelData.width * pixelData.height * 4;
  }

  private createMaskSource(mask: PsdNode['mask']): RasterMaskSource | undefined {
    if (!mask?.imageData || !this.isValidPixelData(mask.imageData)) {
      return undefined;
    }

    return {
      x: mask.left ?? 0,
      y: mask.top ?? 0,
      width: mask.imageData.width,
      height: mask.imageData.height,
      rgba: this.normalizePixelData(mask.imageData),
    };
  }

  private normalizePixelData(pixelData: PixelData): Uint8ClampedArray {
    if (pixelData.data instanceof Uint8ClampedArray) {
      return pixelData.data;
    }
    return Uint8ClampedArray.from(pixelData.data as ArrayLike<number>);
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
