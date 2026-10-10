import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { StatusPill } from './status-pill';

@Component({
  imports: [StatusPill],
  template: `<Nimbus-status-pill tone="warn">in afwachting</Nimbus-status-pill>`,
})
class Host {}

describe('StatusPill', () => {
  it('projects its label and applies the tone', async () => {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const pill = (fixture.nativeElement as HTMLElement).querySelector('.pill');
    expect(pill?.textContent).toContain('in afwachting');
    expect(pill?.classList).toContain('warn');
  });
});
