import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VerificationMailSent } from './verification-mail-sent';

describe('VerificationMailSent', () => {
  let component: VerificationMailSent;
  let fixture: ComponentFixture<VerificationMailSent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VerificationMailSent],
    }).compileComponents();

    fixture = TestBed.createComponent(VerificationMailSent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
