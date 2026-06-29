import { PsdParser } from '../../src/parser/psd-parser';
import { LayerTree, Layer } from '../../src/parser/layer-tree';
import * as path from 'path';
import * as fs from 'fs';

/**
 * Recursively find all layers that have textStyles defined.
 */
function findTextLayers(layer: Layer): Layer[] {
  const results: Layer[] = [];
  if (layer.textStyles) {
    results.push(layer);
  }
  if (layer.children) {
    for (const child of layer.children) {
      results.push(...findTextLayers(child));
    }
  }
  return results;
}

describe('Text Styles Extraction (Integration)', () => {
  const fixturesDir = path.resolve(__dirname, '..', 'fixtures');
  const textSamplePath = path.join(fixturesDir, 'text-sample.psd');
  const largeTextSamplePath = path.join(fixturesDir, 'large-text-sample.psd');
  const demoPsdPath = path.resolve(__dirname, '..', '..', '..', 'UIArtifacts', 'psd', 'demo.psd');

  describe('Integration: parse a PSD with text layers', () => {
    it('should extract textStyles from text layers in a real PSD', async () => {
      // Prefer a dedicated text-sample fixture, fall back to the project demo PSD
      let psdPath: string;
      if (fs.existsSync(textSamplePath)) {
        psdPath = textSamplePath;
      } else if (fs.existsSync(demoPsdPath)) {
        psdPath = demoPsdPath;
      } else {
        console.warn(
          `[SKIP] No PSD fixture found. ` +
          `Place a PSD with text layers at ${textSamplePath} to enable this test.`,
        );
        return;
      }

      const parser = new PsdParser();
      const result: LayerTree = await parser.parse(psdPath);

      expect(result).toBeDefined();
      expect(result.root).toBeDefined();
      expect(result.metadata).toBeDefined();

      const textLayers = findTextLayers(result.root);
      expect(textLayers.length).toBeGreaterThanOrEqual(1);

      for (const layer of textLayers) {
        const ts = layer.textStyles!;
        expect(ts.fontSize).toBeGreaterThan(0);
        expect(ts.color).toHaveProperty('r');
        expect(ts.fontName).toBeTruthy();
        expect(ts.alignment).toHaveProperty('horizontal');
      }
    });
  });

  describe('Performance: 50+ text objects in < 10 seconds', () => {
    it('should parse a large PSD with 50+ text objects under 10 seconds', async () => {
      if (!fs.existsSync(largeTextSamplePath)) {
        console.warn(
          `[SKIP] Fixture not found: ${largeTextSamplePath}. ` +
          'Place a large PSD with 50+ text layers there to enable this performance test.',
        );
        return;
      }

      const parser = new PsdParser();
      const start = Date.now();
      const result: LayerTree = await parser.parse(largeTextSamplePath);
      const elapsed = Date.now() - start;

      const textLayers = findTextLayers(result.root);
      console.log(
        `[perf] Parsed ${textLayers.length} text layers in ${elapsed} ms`,
      );

      expect(textLayers.length).toBeGreaterThanOrEqual(50);
      expect(elapsed).toBeLessThan(10000);
    });
  });
});
