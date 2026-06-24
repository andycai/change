// src/parser/psd-parser.ts
import { readPsd, Psd, Layer as PsdLayer } from 'ag-psd';
import { readFile } from 'fs/promises';
import { Layer, LayerTree, PsdMetadata, LayerType, Rect } from './layer-tree';

/**
 * Internal shape that both ag-psd's `Psd` and `Layer` types satisfy.
 *
 * `Psd` provides `width`/`height` (canvas bounds) but no `left`/`top`/…/`opacity`.
 * `Layer` provides `left`/`top`/`right`/`bottom`/`hidden`/`opacity` but no `width`/`height`.
 * Both extend `LayerAdditionalInfo` (name, text, imageData, children).
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
  children?: PsdNode[];
}

export class PsdParser {
  /**
   * Parse a PSD file and return a LayerTree.
   */
  async parse(psdPath: string): Promise<LayerTree> {
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

    const root = this.convertLayer(psd as unknown as PsdNode, 'root');

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
  private convertLayer(node: PsdNode, layerId: string): Layer {
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
        node.opacity !== undefined ? node.opacity / 255 : 1.0,
    };

    // Process child layers
    if (node.children && node.children.length > 0) {
      layer.children = node.children.map(
        (child, index) =>
          this.convertLayer(child, `${layerId}_${index}`),
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
