import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EmptyState } from './empty-state';

@Component({
  imports: [EmptyState],
  template: `<Nimbus-empty-state icon="ti-clipboard-check">Niets te doen.</Nimbus-empty-state>`,
})
class Host {}

describe('EmptyState', () => {
  it('renders the icon and message', async () => {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Niets te doen.');
    expect(el.querySelector('i.ti-clipboard-check')).toBeTruthy();
  });
});
