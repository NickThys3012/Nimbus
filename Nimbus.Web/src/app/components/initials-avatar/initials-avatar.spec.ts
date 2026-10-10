import { ComponentFixture, TestBed } from '@angular/core/testing';
import { InitialsAvatar } from './initials-avatar';

describe('InitialsAvatar', () => {
  let fixture: ComponentFixture<InitialsAvatar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [InitialsAvatar] }).compileComponents();
    fixture = TestBed.createComponent(InitialsAvatar);
    fixture.componentRef.setInput('initials', 'NT');
    await fixture.whenStable();
  });

  it('renders the initials', () => {
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('NT');
  });

  it('applies the tone class and size', async () => {
    fixture.componentRef.setInput('tone', 'danger');
    fixture.componentRef.setInput('size', 32);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.avatar')?.classList).toContain('danger');
    expect(el.style.getPropertyValue('--avatar-size')).toBe('32px');
  });
});
