import { createStableId, createStableName, StableIdAllocator } from '../../src/fairygui/identity';

describe('FairyGUI identity', () => {
  test('creates deterministic ids independent of call order', () => {
    expect(createStableId('image', 'page|42|bg')).toBe(createStableId('image', 'page|42|bg'));
    expect(createStableId('image', 'page|42|bg')).not.toBe(createStableId('image', 'page|43|bg'));
  });

  test('creates safe stable names with hash suffix on collisions', () => {
    expect(createStableName('Close / Button', 'page|42')).toMatch(/^Close_Button_[a-z0-9]{6}$/);
  });

  test('resolves truncated hash collisions with reproducible salts', () => {
    const firstCollisions: Array<{ salt: number }> = [];
    const first = new StableIdAllocator(collision => firstCollisions.push(collision));
    const firstIds = Array.from({ length: 30 }, (_, index) => first.allocate('image', `page|${index}`, 1));

    const secondCollisions: Array<{ salt: number }> = [];
    const second = new StableIdAllocator(collision => secondCollisions.push(collision));
    const secondIds = Array.from({ length: 30 }, (_, index) => second.allocate('image', `page|${index}`, 1));

    expect(new Set(firstIds).size).toBe(30);
    expect(secondIds).toEqual(firstIds);
    expect(firstCollisions.some(collision => collision.salt > 0)).toBe(true);
    expect(secondCollisions).toEqual(firstCollisions);
  });

  test('fails clearly instead of retrying forever when the id space is exhausted', () => {
    const allocator = new StableIdAllocator();
    for (let index = 0; index < 36; index++) {
      allocator.reserve(index.toString(36), `manual-${index}`);
    }

    expect(() => allocator.allocate('image', 'page|overflow', 1)).toThrow(
      'Stable ID space exhausted for namespace "image" at length 1 (36 candidates)',
    );
  });
});
