import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Avatar } from './avatar';

describe('Avatar', () => {
  let component: Avatar;
  let fixture: ComponentFixture<Avatar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Avatar],
    }).compileComponents();

    fixture = TestBed.createComponent(Avatar);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('opens the menu when clicking the avatar button', () => {
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('#av-btn');
    const menu: HTMLElement = fixture.nativeElement.querySelector('#av-menu');

    expect(menu.hidden).toBe(true);

    button.click();
    fixture.detectChanges();

    expect(menu.hidden).toBe(false);
    expect(button.getAttribute('aria-expanded')).toBe('true');
  });

  it('closes the menu when clicking outside', () => {
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('#av-btn');
    const menu: HTMLElement = fixture.nativeElement.querySelector('#av-menu');

    button.click();
    fixture.detectChanges();
    expect(menu.hidden).toBe(false);

    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    fixture.detectChanges();

    expect(menu.hidden).toBe(true);
    expect(button.getAttribute('aria-expanded')).toBe('false');
  });
});
