import { TestBed } from '@angular/core/testing';

import { RichTextView } from './rich-text-view';

describe('RichTextView', () => {
  async function render(html: string | null | undefined) {
    const fixture = TestBed.createComponent(RichTextView);
    fixture.componentRef.setInput('html', html);
    await fixture.whenStable();
    return fixture.nativeElement.querySelector('[data-testid="rich-text"]') as HTMLElement;
  }

  it('renders the editor formatting', async () => {
    const el = await render('<h3>Landing</h3><p>Hold <strong>on</strong></p><ul><li>one</li></ul>');
    expect(el.querySelector('h3')?.textContent).toBe('Landing');
    expect(el.querySelector('strong')?.textContent).toBe('on');
    expect(el.querySelectorAll('li').length).toBe(1);
  });

  it('never runs a script or an event handler, whatever the stored value holds', async () => {
    const g = globalThis as unknown as { __pwned?: boolean };
    g.__pwned = false;
    const el = await render(
      '<p>safe</p><script>globalThis.__pwned = true</script><img src="x" onerror="globalThis.__pwned = true"><a href="javascript:globalThis.__pwned = true">x</a>',
    );

    expect(el.querySelector('script')).toBeNull();
    expect(el.innerHTML).not.toContain('onerror');
    // Angular defuses an unsafe link by prefixing it, so it can no longer run.
    expect(el.querySelector('a')?.getAttribute('href') ?? '').toMatch(/^unsafe:/);
    expect(el.textContent).toContain('safe');
    expect(g.__pwned).toBe(false);
  });

  it('renders nothing for a missing value', async () => {
    expect((await render(null)).innerHTML).toBe('');
    expect((await render(undefined)).innerHTML).toBe('');
  });
});
