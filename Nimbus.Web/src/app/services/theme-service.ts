import { effect, Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ThemeService {
  readonly isDarkMode = signal<boolean>(localStorage.getItem('darkMode') === 'true');

  constructor() {
    effect(() => {
      const dark = this.isDarkMode();
      localStorage.setItem('darkMode', String(dark));
      document.documentElement.classList.toggle('lt', !dark);
    });
  }

  toggle() {
    this.isDarkMode.update((val) => !val);
  }
}
