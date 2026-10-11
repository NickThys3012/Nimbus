import { ComponentFixture, TestBed } from '@angular/core/testing';
import { importProvidersFrom } from '@angular/core';
import { provideRouter } from '@angular/router';
import { Register } from './register';
import { NimbusApiClient } from '../../../../../core/api-client';

describe('Register', () => {
  let component: Register;
  let fixture: ComponentFixture<Register>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Register],
      providers: [
        provideRouter([]),
        importProvidersFrom(NimbusApiClient),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Register);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('explains that approval is required, without promising an approval email', () => {
    const note = (fixture.nativeElement as HTMLElement).querySelector('#pending-note')?.textContent ?? '';
    expect(note).toContain('approved by an administrator');
    expect(note).not.toMatch(/receive an email/i);
    expect(note).not.toMatch(/awaiting approval/i); // unapproved accounts cannot sign in (#16)
  });
});
