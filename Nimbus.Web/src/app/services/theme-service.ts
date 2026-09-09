import { effect, inject, Injectable, InjectionToken, signal } from '@angular/core';

export const LOCAL_STORAGE = new InjectionToken<Storage | null>('LocalStorage', {
  providedIn: 'root',
  factory: () => (typeof localStorage !== 'undefined' ? localStorage : null),
});

@Injectable({
  providedIn: 'root',
})
export class ThemeService {
  private readonly storage = inject(LOCAL_STORAGE);
  readonly isDarkMode = signal<boolean>(this.storage?.getItem('darkMode') === 'true');

  constructor() {
    effect(() => {
      const dark = this.isDarkMode();
      this.storage?.setItem('darkMode', String(dark));
      if (typeof document !== 'undefined') {
        document.documentElement.classList.toggle('lt', !dark);
      }
    });
  }

  toggle() {
    this.isDarkMode.update((val) => !val);
  }

  setDarkMode(): void {
    this.isDarkMode.set(true);
  }

  setLightMode(): void {
    this.isDarkMode.set(false);
  }

  // Backwards-compat for existing templates; remove once call sites are updated.
  SetDarkMode(): void {
    this.setDarkMode();
  }

  SetLightMode(): void {
    this.setLightMode();
  }
}
