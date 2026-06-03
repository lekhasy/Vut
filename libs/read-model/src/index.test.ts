import { describe, it, expect } from 'bun:test';
import { READ_MODEL_LIB_VERSION } from './index';

describe('@velucid/read-model', () => {
  it('exports a version string', () => {
    expect(READ_MODEL_LIB_VERSION).toMatch(/^\d+\.\d+\.\d+$/);
  });
});
