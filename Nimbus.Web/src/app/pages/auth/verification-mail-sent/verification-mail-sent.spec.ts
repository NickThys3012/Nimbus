import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { convertToParamMap, ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';
import { vi } from 'vitest';

import VerificationMailSent from './verification-mail-sent';
import { AuthStore } from '../../../core/auth/auth.store';

describe('VerificationMailSent', () => {
  let component: VerificationMailSent;
  let fixture: ComponentFixture<VerificationMailSent>;

  beforeEach(async () => {
    const authStoreMock = {
      error: signal<string | null>(null).asReadonly(),
      isLoading: signal(false).asReadonly(),
      requestNewVerificationEmail: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [VerificationMailSent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: { queryParamMap: of(convertToParamMap({})) },
        },
        { provide: AuthStore, useValue: authStoreMock },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(VerificationMailSent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
