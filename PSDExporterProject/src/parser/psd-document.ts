import { LayerTree } from './layer-tree';

export interface RasterMaskSource {
  x: number;
  y: number;
  width: number;
  height: number;
  rgba: Uint8ClampedArray;
}

export interface RasterSource {
  layerId: string;
  photoshopLayerId?: number;
  width: number;
  height: number;
  rgba: Uint8ClampedArray;
  mask?: RasterMaskSource;
}

export interface ParsedPsdDocument {
  tree: LayerTree;
  rasterSources: Map<string, RasterSource>;
}
