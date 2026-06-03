import { describe, it, expect } from 'bun:test';
import { EVENTS_LIB_VERSION } from './index';

describe('@velucid/events', () => {
  it('exports a version string', () => {
    expect(typeof EVENTS_LIB_VERSION).toBe('string');
    expect(EVENTS_LIB_VERSION).toMatch(/^\d+\.\d+\.\d+$/);
  });
});
