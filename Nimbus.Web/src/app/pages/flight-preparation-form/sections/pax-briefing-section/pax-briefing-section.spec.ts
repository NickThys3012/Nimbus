import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';

import { PaxBriefingSection } from './pax-briefing-section';
import { createFlightPreparationForm } from '../../../../core/flight-preparations/flight-preparation-form';
import { PAX_BRIEFING_TEMPLATE_HTML, PAX_BRIEFING_TEMPLATE_VERSION } from '../../../../core/flight-preparations/pax-briefing-template';

@Component({ imports: [PaxBriefingSection], template: `<Nimbus-pax-briefing-section [group]="form.controls.paxBriefing" />` })
class Host {
  form = createFlightPreparationForm(TestBed.inject(NonNullableFormBuilder));
}

describe('PaxBriefingSection', () => {
  async function setup(initial?: { text: string; version?: string; custom?: boolean }) {
    const fixture = TestBed.createComponent(Host);
    if (initial) {
      fixture.componentInstance.form.controls.paxBriefing.setValue({
        paxBriefingText: initial.text,
        paxBriefingVersion: initial.version ?? PAX_BRIEFING_TEMPLATE_VERSION,
        paxBriefingIsCustom: initial.custom ?? false,
      });
    }
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const q = (id: string) => el.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
    const form = fixture.componentInstance.form;
    return { fixture, el, q, form, group: form.controls.paxBriefing };
  }

  /** What the editor does on a user edit: it writes the new HTML into the bound control. */
  async function typeInEditor(ctx: Awaited<ReturnType<typeof setup>>, html: string) {
    ctx.group.controls.paxBriefingText.setValue(html);
    ctx.group.controls.paxBriefingText.markAsDirty();
    ctx.fixture.detectChanges();
    await ctx.fixture.whenStable();
    ctx.fixture.detectChanges();
  }

  it('shows the standard briefing as standard, with the reset action disabled', async () => {
    const { q } = await setup({ text: PAX_BRIEFING_TEMPLATE_HTML });

    expect(q('pax-briefing-standard-badge')?.textContent).toContain(`v${PAX_BRIEFING_TEMPLATE_VERSION}`);
    expect(q('pax-briefing-custom-badge')).toBeNull();
    expect(q('pax-briefing-reset')?.querySelector('button')?.disabled).toBe(true);
  });

  it('renders the editor and a preview of the briefing', async () => {
    const { q, el } = await setup({ text: PAX_BRIEFING_TEMPLATE_HTML });

    expect(el.querySelector('quill-editor')).not.toBeNull();
    expect(q('pax-briefing-preview')?.textContent).toContain('Inflation');
    expect(q('pax-briefing-preview')?.textContent).toContain('After the flight');
  });

  it('marks the briefing customized as soon as it differs from the template, with no tick to forget', async () => {
    const ctx = await setup({ text: PAX_BRIEFING_TEMPLATE_HTML });

    await typeInEditor(ctx, `${PAX_BRIEFING_TEMPLATE_HTML}<p>Extra: bring a jacket.</p>`);

    expect(ctx.group.controls.paxBriefingIsCustom.value).toBe(true);
    expect(ctx.q('pax-briefing-custom-badge')).not.toBeNull();
    expect(ctx.q('pax-briefing-preview')?.textContent).toContain('bring a jacket');
    expect(ctx.q('pax-briefing-reset')?.querySelector('button')?.disabled).toBe(false);
  });

  it('is no longer customized once the text is back to the template', async () => {
    const ctx = await setup({ text: '<p>mine</p>', custom: true });
    expect(ctx.q('pax-briefing-custom-badge')).not.toBeNull();

    await typeInEditor(ctx, PAX_BRIEFING_TEMPLATE_HTML);

    expect(ctx.group.controls.paxBriefingIsCustom.value).toBe(false);
    expect(ctx.q('pax-briefing-standard-badge')).not.toBeNull();
  });

  it('resets to the template without touching any other section', async () => {
    const ctx = await setup({ text: '<p>my own briefing</p>', version: 'old', custom: true });
    ctx.form.controls.general.patchValue({ flightDate: '2026-08-08', flightTime: '18:45' });
    ctx.form.controls.meteo.patchValue({ windSpeedKt: 12, metar: 'EBBR 081650Z' });
    ctx.form.controls.loadCalculation.patchValue({ envelopeWeightKg: 118 });
    const othersBefore = JSON.stringify({ ...ctx.form.getRawValue(), paxBriefing: null });

    (ctx.q('pax-briefing-reset')!.querySelector('button') as HTMLButtonElement).click();
    ctx.fixture.detectChanges();
    await ctx.fixture.whenStable();
    ctx.fixture.detectChanges();

    expect(ctx.group.getRawValue()).toEqual({
      paxBriefingText: PAX_BRIEFING_TEMPLATE_HTML,
      paxBriefingVersion: PAX_BRIEFING_TEMPLATE_VERSION,
      paxBriefingIsCustom: false,
    });
    expect(JSON.stringify({ ...ctx.form.getRawValue(), paxBriefing: null })).toBe(othersBefore);
    expect(ctx.group.dirty).toBe(true);
    expect(ctx.q('pax-briefing-standard-badge')).not.toBeNull();
  });
});
