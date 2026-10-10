import { TestBed } from '@angular/core/testing';
import { CountBadge } from './count-badge';

describe('CountBadge', () => {
  it('shows the count and tone', async () => {
    await TestBed.configureTestingModule({ imports: [CountBadge] }).compileComponents();
    const fixture = TestBed.createComponent(CountBadge);
    fixture.componentRef.setInput('count', 3);
    fixture.componentRef.setInput('tone', 'warn');
    await fixture.whenStable();
    const badge = (fixture.nativeElement as HTMLElement).querySelector('.badge');
    expect(badge?.textContent?.trim()).toBe('3');
    expect(badge?.classList).toContain('warn');
  });
});
