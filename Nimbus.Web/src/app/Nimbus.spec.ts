import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Nimbus } from './Nimbus';
import { LOCAL_STORAGE } from './services/theme-service';
import { AuthStore } from './core/auth/auth.store';

const store: Record<string, string> = {};
const mockStorage = {
  getItem: (key: string) => store[key] ?? null,
  setItem: (key: string, value: string) => {
    store[key] = value;
  },
  removeItem: (key: string) => {
    delete store[key];
  },
  clear: () => {
    Object.keys(store).forEach((key) => delete store[key]);
  },
  length: 0,
  key: () => null,
} as unknown as Storage;

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Nimbus],
      providers: [
        provideRouter([]),
        { provide: LOCAL_STORAGE, useValue: mockStorage },
        {
          provide: AuthStore,
          useValue: {
            isAuthenticated: () => false,
            role: () => null,
            roles: () => [],
            isPendingApproval: () => false,
            isAccessRejected: () => false,
          },
        },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(Nimbus);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('shows the pending approval notice only when the store reports it', async () => {
    const pending = signal(false);
    const rejected = signal(false);
    TestBed.overrideProvider(AuthStore, {
      useValue: {
        isAuthenticated: () => true,
        role: () => 'Pilot',
        roles: () => ['Pilot'],
        isPendingApproval: pending,
        isAccessRejected: rejected,
        requestedAt: () => '2026-07-26T10:00:00Z',
        initials: () => 'PP',
        abbreviatedName: () => 'P. Pilot',
        email: () => 'pilot@example.com',
        logout: () => undefined,
      },
    });
    const fixture = TestBed.createComponent(Nimbus);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#pending-approval-notice')).toBeNull();
    expect(el.querySelector('router-outlet')).toBeTruthy();

    pending.set(true);
    await fixture.whenStable();
    expect(el.querySelector('#pending-approval-notice')).toBeTruthy();
    expect(el.textContent).toContain('Your account is awaiting approval');
    expect(el.querySelector('router-outlet')).toBeNull();

    pending.set(false);
    rejected.set(true);
    await fixture.whenStable();
    expect(el.querySelector('#pending-approval-notice')).toBeNull();
    expect(el.querySelector('#rejected-notice')).toBeTruthy();
    expect(el.textContent).toContain('No access to Nimbus');
    expect(el.querySelector('router-outlet')).toBeNull();
  });

  it('should render the router outlet', async () => {
    const fixture = TestBed.createComponent(Nimbus);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });
});
