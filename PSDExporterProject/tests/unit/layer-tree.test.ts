import { Layer, LayerTree, Rect } from '../../src/parser/layer-tree';

describe('LayerTree Data Structures', () => {
  test('should create a valid Layer', () => {
    const bounds: Rect = { x: 0, y: 0, width: 100, height: 100 };
    const layer: Layer = {
      id: 'layer_001',
      name: 'test_layer',
      type: 'group',
      bounds,
      visible: true,
      opacity: 1.0,
      children: [],
    };

    expect(layer.id).toBe('layer_001');
    expect(layer.type).toBe('group');
    expect(layer.bounds.width).toBe(100);
  });

  test('should create a valid LayerTree', () => {
    const root: Layer = {
      id: 'root',
      name: 'Root',
      type: 'group',
      bounds: { x: 0, y: 0, width: 1920, height: 1080 },
      visible: true,
      opacity: 1.0,
      children: [],
    };

    const tree: LayerTree = {
      root,
      metadata: {
        psdPath: '/path/to/test.psd',
        canvasSize: { x: 0, y: 0, width: 1920, height: 1080 },
        timestamp: '2026-06-24T10:00:00Z',
      },
    };

    expect(tree.root.name).toBe('Root');
    expect(tree.metadata.canvasSize.width).toBe(1920);
  });
});
