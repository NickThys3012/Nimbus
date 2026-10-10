import { ComponentFixture, TestBed } from '@angular/core/testing';
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

  it('exposes the aria-label for icon-only buttons and honours disabled', async () => {
    fixture.componentRef.setInput('ariaLabel', 'Blokkeer');
    fixture.componentRef.setInput('disabled', true);
    await fixture.whenStable();
    const btn = (fixture.nativeElement as HTMLElement).querySelector('button');
    expect(btn?.getAttribute('aria-label')).toBe('Blokkeer');
    expect(btn?.disabled).toBe(true);
  });
});
