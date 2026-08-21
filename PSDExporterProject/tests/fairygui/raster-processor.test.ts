import {
  applyRasterMask,
  detectSolidColor,
  trimRaster,
  resolveScale9,
} from '../../src/fairygui/raster-processor';

describe('FairyGUI raster processing', () => {
  test('trims transparent pixels and returns coordinate compensation', () => {
    const rgba = new Uint8ClampedArray(4 * 3 * 3);
    rgba[(1 * 3 + 1) * 4 + 3] = 255;

    const result = trimRaster({ layerId: 'x', width: 3, height: 3, rgba });

    expect(result).toMatchObject({ offsetX: 1, offsetY: 1, width: 1, height: 1 });
    expect(result.rgba).toEqual(new Uint8ClampedArray([0, 0, 0, 255]));
  });

  test('keeps original raster when trimmed scale9 would be invalid', () => {
    const result = resolveScale9(
      { left: 3, top: 3, right: 3, bottom: 3 },
      { offsetX: 4, offsetY: 4, width: 4, height: 4, originalWidth: 8, originalHeight: 8 },
    );
    expect(result.trimAllowed).toBe(false);
    expect(result.grid).toEqual({ x: 3, y: 3, width: 2, height: 2 });
  });

  test('applies a grayscale raster mask to source alpha', () => {
    const result = applyRasterMask({
      layerId: 'x',
      width: 2,
      height: 1,
      rgba: new Uint8ClampedArray([
        255, 0, 0, 255,
        255, 0, 0, 255,
      ]),
      mask: {
        x: 10,
        y: 20,
        width: 2,
        height: 1,
        rgba: new Uint8ClampedArray([
          255, 255, 255, 255,
          0, 0, 0, 255,
        ]),
      },
    }, { x: 10, y: 20, width: 2, height: 1 });

    expect(result.rgba[3]).toBe(255);
    expect(result.rgba[7]).toBe(0);
  });

  test('detects a uniform non-transparent fill color', () => {
    const result = detectSolidColor({
      layerId: 'x', width: 2, height: 1,
      rgba: new Uint8ClampedArray([
        20, 40, 60, 255,
        20, 40, 60, 255,
      ]),
    });

    expect(result).toEqual({ r: 20, g: 40, b: 60, a: 255 });
  });

  test('rejects a visibly non-uniform fill color', () => {
    const result = detectSolidColor({
      layerId: 'x', width: 2, height: 1,
      rgba: new Uint8ClampedArray([
        20, 40, 60, 255,
        80, 40, 60, 255,
      ]),
    });

    expect(result).toBeUndefined();
  });
});
