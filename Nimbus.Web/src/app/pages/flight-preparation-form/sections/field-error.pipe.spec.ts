import { FieldErrorPipe } from './field-error.pipe';

describe('FieldErrorPipe', () => {
  const pipe = new FieldErrorPipe();

  it('returns null for no errors', () => {
    expect(pipe.transform(null)).toBeNull();
    expect(pipe.transform(undefined)).toBeNull();
  });

  it('maps required/requiredTrue to "Required"', () => {
    expect(pipe.transform({ required: true })).toBe('Required');
    expect(pipe.transform({ requiredTrue: true })).toBe('Required');
  });

  it('maps min to "Must be N or more"', () => {
    expect(pipe.transform({ min: { min: 0, actual: -3 } })).toBe('Must be 0 or more');
  });

  it('maps max to "Must be N or less"', () => {
    expect(pipe.transform({ max: { max: 360, actual: 400 } })).toBe('Must be 360 or less');
  });

  it('falls back to a generic message for unknown validators', () => {
    expect(pipe.transform({ somethingElse: true })).toBe('Invalid value');
  });
});
