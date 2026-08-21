import { RasterSource } from '../parser/psd-document';
import { Rect } from '../parser/layer-tree';

export interface TrimmedRaster {
  rgba: Uint8ClampedArray;
  offsetX: number;
  offsetY: number;
  width: number;
  height: number;
  originalWidth: number;
  originalHeight: number;
  empty: boolean;
}

export interface Scale9Margins {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

export interface Scale9Result {
  trimAllowed: boolean;
  grid: { x: number; y: number; width: number; height: number };
}

export interface SolidColor {
  r: number;
  g: number;
  b: number;
  a: number;
}

export function applyRasterMask(source: RasterSource, layerBounds: Rect): RasterSource {
  if (!source.mask) return source;

  const rgba = new Uint8ClampedArray(source.rgba);
  const mask = source.mask;
  for (let y = 0; y < source.height; y++) {
    for (let x = 0; x < source.width; x++) {
      const documentX = layerBounds.x + x;
      const documentY = layerBounds.y + y;
      const maskX = documentX - mask.x;
      const maskY = documentY - mask.y;
      const sourceIndex = (y * source.width + x) * 4;
      if (maskX < 0 || maskY < 0 || maskX >= mask.width || maskY >= mask.height) {
        rgba[sourceIndex + 3] = 0;
        continue;
      }
      const maskIndex = (maskY * mask.width + maskX) * 4;
      const maskValue = Math.round(
        (mask.rgba[maskIndex] + mask.rgba[maskIndex + 1] + mask.rgba[maskIndex + 2]) / 3,
      );
      const maskAlpha = mask.rgba[maskIndex + 3] / 255;
      rgba[sourceIndex + 3] = Math.round(rgba[sourceIndex + 3] * (maskValue / 255) * maskAlpha);
    }
  }
  return { ...source, rgba };
}

export function detectSolidColor(source: RasterSource, tolerance = 2): SolidColor | undefined {
  let sample: SolidColor | undefined;
  for (let index = 0; index < source.rgba.length; index += 4) {
    const alpha = source.rgba[index + 3];
    if (alpha === 0) continue;
    if (!sample) {
      sample = {
        r: source.rgba[index],
        g: source.rgba[index + 1],
        b: source.rgba[index + 2],
        a: alpha,
      };
      continue;
    }
    if (
      Math.abs(source.rgba[index] - sample.r) > tolerance
      || Math.abs(source.rgba[index + 1] - sample.g) > tolerance
      || Math.abs(source.rgba[index + 2] - sample.b) > tolerance
      || Math.abs(alpha - sample.a) > tolerance
    ) {
      return undefined;
    }
  }
  return sample;
}

export function trimRaster(source: RasterSource): TrimmedRaster {
  let minX = source.width;
  let minY = source.height;
  let maxX = -1;
  let maxY = -1;

  for (let y = 0; y < source.height; y++) {
    for (let x = 0; x < source.width; x++) {
      if (source.rgba[(y * source.width + x) * 4 + 3] === 0) continue;
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
  }

  if (maxX < 0) {
    return {
      rgba: new Uint8ClampedArray(),
      offsetX: 0,
      offsetY: 0,
      width: 0,
      height: 0,
      originalWidth: source.width,
      originalHeight: source.height,
      empty: true,
    };
  }

  const width = maxX - minX + 1;
  const height = maxY - minY + 1;
  const rgba = new Uint8ClampedArray(width * height * 4);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const sourceIndex = ((minY + y) * source.width + minX + x) * 4;
      const targetIndex = (y * width + x) * 4;
      rgba.set(source.rgba.subarray(sourceIndex, sourceIndex + 4), targetIndex);
    }
  }

  return {
    rgba,
    offsetX: minX,
    offsetY: minY,
    width,
    height,
    originalWidth: source.width,
    originalHeight: source.height,
    empty: false,
  };
}

export function resolveScale9(
  margins: Scale9Margins,
  trimmed: Pick<TrimmedRaster, 'offsetX' | 'offsetY' | 'width' | 'height' | 'originalWidth' | 'originalHeight'>,
): Scale9Result {
  const originalGrid = {
    x: margins.left,
    y: margins.top,
    width: trimmed.originalWidth - margins.left - margins.right,
    height: trimmed.originalHeight - margins.top - margins.bottom,
  };
  const translated = {
    x: margins.left - trimmed.offsetX,
    y: margins.top - trimmed.offsetY,
    width: originalGrid.width,
    height: originalGrid.height,
  };
  const valid = translated.x >= 0
    && translated.y >= 0
    && translated.width > 0
    && translated.height > 0
    && translated.x + translated.width <= trimmed.width
    && translated.y + translated.height <= trimmed.height;

  return { trimAllowed: valid, grid: valid ? translated : originalGrid };
}
