import { Component, ElementRef, HostListener, inject } from '@angular/core';
import { ThemeService } from '../../../../services/theme-service';
import { AuthStore } from '../../../../core/auth/auth.store';
import { RouterLink } from '@angular/router';

@Component({
  imports: [RouterLink],
  selector: 'Nimbus-avatar',
  styleUrl: './avatar.css',
  templateUrl: './avatar.html',
})
export class Avatar {
  readonly themeService = inject(ThemeService);
  readonly authStore = inject(AuthStore);
  protected isMenuOpen = false;
  private readonly host = inject(ElementRef<HTMLElement>);

  protected toggleMenu(): void {
    this.isMenuOpen = !this.isMenuOpen;
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    const target = event.target as Node | null;
    if (target && !this.host.nativeElement.contains(target)) {
      this.isMenuOpen = false;
    }
  }
}
