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

  it('wires aria-controls and a roving tabindex', async () => {
    const el = (await setup()).nativeElement as HTMLElement;
    expect(el.querySelector('#tab-a')?.getAttribute('aria-controls')).toBe('panel-a');
    expect(el.querySelector('#tab-b')?.getAttribute('aria-controls')).toBe('panel-b');
    expect(el.querySelector('#tab-a')?.getAttribute('tabindex')).toBe('0');
    expect(el.querySelector('#tab-b')?.getAttribute('tabindex')).toBe('-1');
    expect(el.querySelector('[role="tablist"]')?.getAttribute('aria-label')).toBe('Sections');
  });

  it('supports Arrow keys (with wrap), Home and End', async () => {
    const fixture = await setup();
    const el = fixture.nativeElement as HTMLElement;
    const press = async (key: string) => {
      el.querySelector('[role="tablist"]')?.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
      await fixture.whenStable();
    };
    await press('ArrowRight');
    expect(fixture.componentInstance.active()).toBe('b');
    expect(document.activeElement).toBe(el.querySelector('#tab-b') ?? null);
    await press('ArrowRight');
    expect(fixture.componentInstance.active()).toBe('a');
    await press('ArrowLeft');
    expect(fixture.componentInstance.active()).toBe('b');
    await press('Home');
    expect(fixture.componentInstance.active()).toBe('a');
    await press('End');
    expect(fixture.componentInstance.active()).toBe('b');
    await press('x');
    expect(fixture.componentInstance.active()).toBe('b');
  });
});
