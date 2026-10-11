import { safeReturnUrl } from './return-url';

describe('safeReturnUrl', () => {
  it('keeps in-app paths', () => expect(safeReturnUrl('/flights?x=1')).toBe('/flights?x=1'));
  it.each([null, undefined, '', 'https://evil.example', '//evil.example', '/\\evil.example'])(
    'falls back for %s',
    (v) => expect(safeReturnUrl(v)).toBe('/home'),
  );
});
