import { TestBed } from '@angular/core/testing';
import { Tabs } from './tabs';

describe('Tabs', () => {
  async function setup() {
    await TestBed.configureTestingModule({ imports: [Tabs] }).compileComponents();
    const fixture = TestBed.createComponent(Tabs);
    fixture.componentRef.setInput('tabs', [
      { id: 'a', label: 'Eerste', count: 2, countTone: 'warn' },
      { id: 'b', label: 'Tweede' },
    ]);
    fixture.componentRef.setInput('active', 'a');
    await fixture.whenStable();
    return fixture;
  }

  it('renders tabs, marks the active one and shows counts only when given', async () => {
    const el = (await setup()).nativeElement as HTMLElement;
    expect(el.querySelector('#tab-a')?.classList).toContain('on');
    expect(el.querySelector('#tab-b')?.classList).not.toContain('on');
    expect(el.querySelector('#tab-a Nimbus-count-badge')).toBeTruthy();
    expect(el.querySelector('#tab-b Nimbus-count-badge')).toBeNull();
  });

  it('switches the active tab on click', async () => {
    const fixture = await setup();
    ((fixture.nativeElement as HTMLElement).querySelector('#tab-b') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(fixture.componentInstance.active()).toBe('b');
  });
});
