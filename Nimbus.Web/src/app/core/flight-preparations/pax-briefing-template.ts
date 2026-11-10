/** Version stamp of the standard briefing; stored with a flight so it is clear which wording it started from. */
export const PAX_BRIEFING_TEMPLATE_VERSION = '2026-11';

/**
 * The standard passenger safety briefing a new flight starts with: inflation, in-flight conduct,
 * landing position and post-flight instructions. Plain editor formatting only (headings, lists,
 * bold), the same set the API keeps when it sanitises a stored briefing.
 */
export const PAX_BRIEFING_TEMPLATE_HTML = [
  '<h3>Inflation</h3>',
  '<ul>',
  '<li>Stay well clear of the envelope, the fan and the burner until the crew tells you to come closer.</li>',
  '<li>Do not climb into the basket until the pilot invites you.</li>',
  '<li>Wear closed, sturdy shoes and keep loose items, hats and bags secured.</li>',
  '</ul>',
  '<h3>In flight</h3>',
  '<ul>',
  '<li>Stay inside the basket at all times and keep your hands and head inside it.</li>',
  '<li>Never touch the burner, the cables or the instruments. The burner is loud and gives off heat.</li>',
  '<li>Hold on when the pilot asks you to, and do not throw anything out of the basket.</li>',
  '<li>Tell the pilot at once if you feel unwell or unsure.</li>',
  '</ul>',
  '<h3>Landing</h3>',
  '<ul>',
  '<li>When the pilot calls the landing, <strong>face the direction of travel</strong>, feet together, knees slightly bent.</li>',
  '<li>Hold the handles firmly with both hands and stay down until the basket has stopped moving.</li>',
  '<li>The basket may bounce, drag or tip over. This is normal; do not try to stand up or get out until the pilot says so.</li>',
  '</ul>',
  '<h3>After the flight</h3>',
  '<ul>',
  '<li>Leave the basket only when the pilot tells you to, and stay clear of the envelope.</li>',
  '<li>Help pack the balloon if you like, and respect the landowner and their crops and animals.</li>',
  '<li>Take all your belongings and leave no litter behind.</li>',
  '</ul>',
].join('');

/** The briefing fields a new flight starts with. */
export function defaultPaxBriefing(): { paxBriefingText: string; paxBriefingVersion: string; paxBriefingIsCustom: boolean } {
  return { paxBriefingText: PAX_BRIEFING_TEMPLATE_HTML, paxBriefingVersion: PAX_BRIEFING_TEMPLATE_VERSION, paxBriefingIsCustom: false };
}
