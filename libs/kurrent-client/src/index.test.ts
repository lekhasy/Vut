import { describe, it, expect } from 'bun:test';
import { KURRENT_CLIENT_LIB_VERSION } from './index';

describe('@velucid/kurrent-client', () => {
  it('exports a version string', () => {
    expect(KURRENT_CLIENT_LIB_VERSION).toMatch(/^\d+\.\d+\.\d+$/);
  });
});
