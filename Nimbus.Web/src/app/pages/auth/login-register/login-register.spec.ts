import { ComponentFixture, TestBed } from '@angular/core/testing';
import { importProvidersFrom } from '@angular/core';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { vi } from 'vitest';

import LoginRegister  from './login-register';
import { NimbusApiClient } from '../../../core/api-client';

describe('LoginRegister', () => {
  let component: LoginRegister;
  let fixture: ComponentFixture<LoginRegister>;
  let router: Router;
  let paramMapSubject: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  beforeEach(async () => {
    vi.restoreAllMocks();
    paramMapSubject = new BehaviorSubject(convertToParamMap({}));

    await TestBed.configureTestingModule({
      imports: [LoginRegister],
      providers: [
        provideRouter([]),
        importProvidersFrom(NimbusApiClient),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: paramMapSubject.asObservable(),
          },
        },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(LoginRegister);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should default to the login tab when no route param is provided', () => {
    expect(component.activeTab()).toBe('login');
    expect(component.showLogin()).toBe(true);
  });

  it('should open the register tab when the route param is register', async () => {
    paramMapSubject.next(convertToParamMap({ tab: 'register' }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.activeTab()).toBe('register');
    expect(component.showLogin()).toBe(false);
  });

  it('should fall back to login when the route param is not supported', async () => {
    paramMapSubject.next(convertToParamMap({ tab: 'anything-else' }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.activeTab()).toBe('login');
    expect(component.showLogin()).toBe(true);
  });

  it('should update the route when selecting the register tab', () => {
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    component.selectTab('register');

    expect(component.activeTab()).toBe('register');
    expect(navigateSpy).toHaveBeenCalledWith(['/login', 'register']);
  });
});
