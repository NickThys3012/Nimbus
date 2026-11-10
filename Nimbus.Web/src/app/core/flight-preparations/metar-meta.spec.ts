import { metarMeta } from './metar-meta';

describe('metarMeta', () => {
  it('reads the station and the observation time from a METAR', () => {
    expect(metarMeta('EBAW 100850Z 08003KT 0400 FG BKN001 01/01 Q1013')).toBe('EBAW · 08:50Z');
    expect(metarMeta('EBBR 172140Z 24006KT 9999 FEW035 19/11 Q1018 NOSIG')).toBe('EBBR · 21:40Z');
  });

  it('copes with a leading METAR or SPECI word, COR, AUTO and a lower-case station', () => {
    expect(metarMeta('METAR EBAW 100850Z 08003KT')).toBe('EBAW · 08:50Z');
    expect(metarMeta('SPECI EBBR 100855Z 09004KT')).toBe('EBBR · 08:55Z');
    expect(metarMeta('METAR COR EBBR 100850Z 09004KT')).toBe('EBBR · 08:50Z');
    expect(metarMeta('EDDF 100850Z AUTO 02005KT')).toBe('EDDF · 08:50Z');
    expect(metarMeta('ebaw 100850Z 08003KT')).toBe('EBAW · 08:50Z');
  });

  it('keeps the multi-line and padded text from a paste working', () => {
    expect(metarMeta('  \n METAR EBAW\n 100850Z 08003KT')).toBe('EBAW · 08:50Z');
  });

  it('is null without a valid time group, because that is what makes text a METAR', () => {
    expect(metarMeta('EBAW 08003KT 0400 FG')).toBeNull();
    expect(metarMeta('EBAW 999999Z 08003KT')).toBeNull();
    expect(metarMeta('EBAW 102560Z 08003KT')).toBeNull();
    expect(metarMeta('WIND from the north, looks fine')).toBeNull();
  });

  it.each([null, undefined, '', '   ', 'wind from the north, looks fine', 'abc 100850Z', 'METAR'])('is null when %j is not a METAR', (text) => {
    expect(metarMeta(text)).toBeNull();
  });
});
