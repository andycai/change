import { hello } from '../src/index';

describe('Project Setup Smoke Test', () => {
  it('should return hello message', () => {
    expect(hello()).toBe('Hello, PSD Exporter!');
  });

  it('should support TypeScript features', () => {
    const result: string = hello();
    expect(typeof result).toBe('string');
  });
});
