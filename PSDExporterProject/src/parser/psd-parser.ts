// src/parser/psd-parser.ts
import { readPsd, Psd } from 'ag-psd';
import { readFileSync } from 'fs';
import { Layer, LayerTree, PsdMetadata, LayerType, Rect } from './layer-tree';

export class PsdParser {
  /**
   * Parse a PSD file and return a LayerTree.
   */
  async parse(psdPath: string): Promise<LayerTree> {
    const buffer = readFileSync(psdPath);
    const psd: Psd = readPsd(buffer);

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

    const root = this.convertLayer(psd, 'root');

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
  private convertLayer(psdLayer: any, layerId: string): Layer {
    const bounds: Rect = {
      x: psdLayer.left || 0,
      y: psdLayer.top || 0,
      width:
        (psdLayer.right ?? psdLayer.width ?? 0) - (psdLayer.left || 0),
      height:
        (psdLayer.bottom ?? psdLayer.height ?? 0) - (psdLayer.top || 0),
    };

    const layer: Layer = {
      id: layerId,
      name: psdLayer.name || 'Unnamed',
      type: this.determineLayerType(psdLayer),
      bounds,
      visible: psdLayer.hidden !== true,
      opacity:
        psdLayer.opacity !== undefined ? psdLayer.opacity / 255 : 1.0,
    };

    // Process child layers
    if (psdLayer.children && psdLayer.children.length > 0) {
      layer.children = psdLayer.children.map(
        (child: any, index: number) =>
          this.convertLayer(child, `${layerId}_${index}`),
      );
    }

    return layer;
  }

  /**
   * Determine the type of a layer based on its properties.
   */
  private determineLayerType(psdLayer: any): LayerType {
    if (psdLayer.children && psdLayer.children.length > 0) {
      return 'group';
    }
    if (psdLayer.text) {
      return 'text';
    }
    if (psdLayer.imageData) {
      return 'image';
    }
    return 'shape';
  }
}
