import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { IconButton } from './icon-button';

describe('IconButton', () => {
  let component: IconButton;
  let fixture: ComponentFixture<IconButton>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IconButton],
    }).compileComponents();

    fixture = TestBed.createComponent(IconButton);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('icon', 'ti-sun');
    fixture.componentRef.setInput('ariaLabel', 'Theme switch');
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('emits pressed when clicked', () => {
    const pressedSpy = vi.fn();
    component.pressed.subscribe(pressedSpy);

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    button.click();

    expect(pressedSpy).toHaveBeenCalled();
  });

  it('does not emit pressed when disabled', () => {
    const pressedSpy = vi.fn();
    component.pressed.subscribe(pressedSpy);
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    button.click();

    expect(pressedSpy).not.toHaveBeenCalled();
  });
});


