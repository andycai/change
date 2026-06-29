import { describe, it, expect } from '@jest/globals';
import { PsdParser } from '../../src/parser/psd-parser';
import * as fs from 'fs';
import * as path from 'path';
import type { Layer } from '../../src/parser/layer-tree';

describe('Performance - Large Text Parsing', () => {
  it('should parse 50+ text layers with effects in under 10 seconds', async () => {
    const largePsdPath = path.join(__dirname, '../fixtures/large-text-sample.psd');

    if (!fs.existsSync(largePsdPath)) {
      console.warn('Large PSD not found, skipping performance test');
      return;
    }

    const parser = new PsdParser();
    const startTime = Date.now();

    const result = await parser.parse(largePsdPath);
    expect(result).toBeTruthy();
    expect(result.root).toBeTruthy();

    const elapsed = Date.now() - startTime;

    // 统计文本图层数量
    const textLayerCount = countTextLayers(result.root);
    expect(textLayerCount).toBeGreaterThan(0);

    console.log('Performance Test Results:');
    console.log(`- Text layers: ${textLayerCount}`);
    console.log(`- Parsing time: ${elapsed}ms`);
    console.log(`- Avg per layer: ${(elapsed / textLayerCount).toFixed(2)}ms`);

    expect(textLayerCount).toBeGreaterThanOrEqual(50);
    expect(elapsed).toBeLessThan(10000); // 10 seconds
  });

  it('should have reasonable per-layer overhead', async () => {
    const largePsdPath = path.join(__dirname, '../fixtures/large-text-sample.psd');

    if (!fs.existsSync(largePsdPath)) {
      console.warn('Large PSD not found, skipping per-layer overhead test');
      return;
    }

    const parser = new PsdParser();
    const startTime = Date.now();
    const result = await parser.parse(largePsdPath);
    expect(result).toBeTruthy();
    expect(result.root).toBeTruthy();

    const elapsed = Date.now() - startTime;

    const textLayerCount = countTextLayers(result.root);
    expect(textLayerCount).toBeGreaterThan(0);

    const avgPerLayer = elapsed / textLayerCount;

    // 期望：每个文本图层处理时间 < 200ms
    expect(avgPerLayer).toBeLessThan(200);
  });
});

function countTextLayers(layer: Layer): number {
  let count = 0;
  if (layer.type === 'text') {
    count++;
  }
  if (layer.children) {
    for (const child of layer.children) {
      count += countTextLayers(child);
    }
  }
  return count;
}
