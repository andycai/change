import { existsSync } from 'fs';
import * as path from 'path';
import type { Layer } from '../../src/parser/layer-tree';

/**
 * Recursively counts text layers in a layer tree.
 * Traverses all children of group layers and counts leaf nodes
 * whose `type` is `'text'`.
 */
function countTextLayers(layer: Layer): number {
  let count = 0;
  if (layer.type === 'text') {
    count = 1;
  }
  if (layer.children) {
    for (const child of layer.children) {
      count += countTextLayers(child);
    }
  }
  return count;
}

describe('Large text parsing performance', () => {
  // Lookup priority:
  // 1. LARGE_TEXT_PSD_PATH environment variable
  // 2. Default fixture path under tests/fixtures/
  const fixturePath =
    process.env.LARGE_TEXT_PSD_PATH ||
    path.resolve(__dirname, '..', 'fixtures', 'large-text-50-plus.psd');

  const hasFixture = existsSync(fixturePath);

  if (!hasFixture) {
    console.warn(
      '[performance] No large PSD fixture found at ' +
        `${fixturePath}. ` +
        'Set LARGE_TEXT_PSD_PATH env var to point to a PSD with 50+ text layers. ' +
        'Skipping performance tests.',
    );
  }

  // Use dynamic import for PsdParser so that the top-level
  // initializeCanvas(createCanvas(...)) call (which requires the `canvas`
  // native package) is only executed when a fixture is actually available.
  const perfTest = hasFixture ? test : test.skip;

  perfTest(
    'should parse 50+ text layers with effects in under 10 seconds',
    async () => {
      const { PsdParser } = await import('../../src/parser/psd-parser');
      const parser = new PsdParser();

      const start = Date.now();
      const tree = await parser.parse(fixturePath);
      const elapsed = Date.now() - start;

      const textLayerCount = countTextLayers(tree.root);

      console.log(
        `[performance] Parsed ${textLayerCount} text layers ` +
          `in ${elapsed}ms ` +
          `(${(elapsed / Math.max(textLayerCount, 1)).toFixed(1)}ms/layer)`,
      );

      expect(textLayerCount).toBeGreaterThanOrEqual(50);
      expect(elapsed).toBeLessThan(10000);
    },
  );

  perfTest(
    'should have reasonable per-layer overhead',
    async () => {
      const { PsdParser } = await import('../../src/parser/psd-parser');
      const parser = new PsdParser();

      const start = Date.now();
      const tree = await parser.parse(fixturePath);
      const elapsed = Date.now() - start;

      const textLayerCount = countTextLayers(tree.root);

      expect(textLayerCount).toBeGreaterThanOrEqual(50);

      const avgPerLayer = elapsed / textLayerCount;
      console.log(
        `[performance] Average per-layer: ${avgPerLayer.toFixed(1)}ms`,
      );
      expect(avgPerLayer).toBeLessThan(200);
    },
  );
});
