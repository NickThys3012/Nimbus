import { defaultPaxBriefing, PAX_BRIEFING_TEMPLATE_HTML, PAX_BRIEFING_TEMPLATE_VERSION } from './pax-briefing-template';

describe('standard passenger briefing', () => {
  it('covers inflation, in-flight conduct, landing position and post-flight instructions', () => {
    const headings = [...PAX_BRIEFING_TEMPLATE_HTML.matchAll(/<h3>(.*?)<\/h3>/g)].map((m) => m[1]);
    expect(headings).toEqual(['Inflation', 'In flight', 'Landing', 'After the flight']);
  });

  it('uses only formatting the API keeps when it sanitises a briefing', () => {
    const tags = new Set([...PAX_BRIEFING_TEMPLATE_HTML.matchAll(/<\/?([a-z0-9]+)/g)].map((m) => m[1]));
    expect([...tags].sort()).toEqual(['h3', 'li', 'strong', 'ul']);
    expect(PAX_BRIEFING_TEMPLATE_HTML).not.toMatch(/<[a-z0-9]+ [^>]*>/); // no attributes
  });

  it('starts a flight on the standard wording, not customized', () => {
    expect(defaultPaxBriefing()).toEqual({
      paxBriefingText: PAX_BRIEFING_TEMPLATE_HTML,
      paxBriefingVersion: PAX_BRIEFING_TEMPLATE_VERSION,
      paxBriefingIsCustom: false,
    });
  });
});
