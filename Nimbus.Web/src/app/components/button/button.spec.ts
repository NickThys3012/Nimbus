import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { Button } from './button';

describe('Button', () => {
  let component: Button;
  let fixture: ComponentFixture<Button>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Button],
    }).compileComponents();

    fixture = TestBed.createComponent(Button);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('id', 'test-btn');
    fixture.componentRef.setInput('type', 'button');
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('renders a button by default, with the requested variant and size', async () => {
    fixture.componentRef.setInput('variant', 'approve');
    fixture.componentRef.setInput('size', 'sm');
    await fixture.whenStable();
    const btn = (fixture.nativeElement as HTMLElement).querySelector('button');
    expect(btn?.classList).toContain('btn-approve');
    expect(btn?.classList).toContain('btn-sm');
    expect((fixture.nativeElement as HTMLElement).querySelector('a')).toBeNull();
  });

  it('renders an anchor when href is set', async () => {
    fixture.componentRef.setInput('href', 'mailto:beheer@nimbus.be');
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('a')?.getAttribute('href')).toBe('mailto:beheer@nimbus.be');
    expect(el.querySelector('button')).toBeNull();
  });

  it('disables an anchor: no href, aria-disabled, not focusable, click is swallowed', async () => {
    fixture.componentRef.setInput('href', 'mailto:beheer@nimbus.be');
    fixture.componentRef.setInput('disabled', true);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const link = el.querySelector('a') as HTMLAnchorElement;
    expect(link.hasAttribute('href')).toBe(false);
    expect(link.getAttribute('aria-disabled')).toBe('true');
    expect(link.getAttribute('tabindex')).toBe('-1');
    expect(link.classList).toContain('btn-disabled');

    const hostClick = vi.fn();
    el.addEventListener('click', hostClick);
    const event = new MouseEvent('click', { bubbles: true, cancelable: true });
    link.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
    expect(hostClick).not.toHaveBeenCalled();
  });

  it('an enabled anchor keeps href and is focusable', async () => {
    fixture.componentRef.setInput('href', 'mailto:beheer@nimbus.be');
    await fixture.whenStable();
    const link = (fixture.nativeElement as HTMLElement).querySelector('a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('mailto:beheer@nimbus.be');
    expect(link.hasAttribute('aria-disabled')).toBe(false);
    expect(link.hasAttribute('tabindex')).toBe(false);
  });

  it('exposes the aria-label for icon-only buttons and honours disabled', async () => {
    fixture.componentRef.setInput('ariaLabel', 'Block');
    fixture.componentRef.setInput('disabled', true);
    await fixture.whenStable();
    const btn = (fixture.nativeElement as HTMLElement).querySelector('button');
    expect(btn?.getAttribute('aria-label')).toBe('Block');
    expect(btn?.disabled).toBe(true);
  });
});
