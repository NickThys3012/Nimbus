import { Component, signal, WritableSignal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { MobileMenu } from './mobile-menu';
import { NavItem } from '../../models/nav-item';

@Component({
  standalone: true,
  template: '',
})
class DummyComponent {}

describe('MobileMenu', () => {
  let component: MobileMenu;
  let fixture: ComponentFixture<MobileMenu>;
  let router: Router;
  let menuOpen: WritableSignal<boolean>;

  async function renderComponent(navItems: NavItem[] = []) {
    menuOpen = signal(true);
    fixture.componentRef.instance.menuOpen = menuOpen;
    fixture.componentRef.setInput('navItems', navItems);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    vi.restoreAllMocks();

    await TestBed.configureTestingModule({
      imports: [MobileMenu],
      providers: [
        provideRouter([
          { path: 'flights', component: DummyComponent },
          { path: 'login', component: DummyComponent },
          { path: 'login/:tab', component: DummyComponent },
        ]),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(MobileMenu);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
  });

  it('should create', async () => {
    await renderComponent();

    expect(component).toBeTruthy();
  });

  it('closes the menu when a mobile nav link is clicked', async () => {
    await renderComponent([new NavItem('Flights', 'ti-list', '/flights', true)]);

    const link = fixture.nativeElement.querySelector('.sheet-links a') as HTMLAnchorElement;
    link.click();

    expect(menuOpen()).toBe(false);
  });

  it('closes the menu and navigates to login from the mobile CTA', async () => {
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    await renderComponent();

    const signInButton = fixture.nativeElement.querySelector('#btn-sign-in') as HTMLButtonElement;
    signInButton.click();

    expect(menuOpen()).toBe(false);
    expect(navigateSpy).toHaveBeenCalledWith(['/login']);
  });

  it('closes the menu and navigates to register from the mobile CTA', async () => {
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    await renderComponent();

    const requestAccountButton = fixture.nativeElement.querySelector('#btn-request-account') as HTMLButtonElement;
    requestAccountButton.click();

    expect(menuOpen()).toBe(false);
    expect(navigateSpy).toHaveBeenCalledWith(['/login', 'register']);
  });
});
