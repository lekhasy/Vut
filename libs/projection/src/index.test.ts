import { describe, it, expect } from 'bun:test';
import { PROJECTION_LIB_VERSION, identity } from './index';

describe('@velucid/projection', () => {
  it('exports a version string', () => {
    expect(PROJECTION_LIB_VERSION).toMatch(/^\d+\.\d+\.\d+$/);
  });

  it('identity is a no-op projection', () => {
    const state = { a: 1, b: 'x' };
    expect(identity(state)).toBe(state);
  });
});
