import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, input, output } from '@angular/core';

const FOCUSABLE =
  'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

let nextId = 0;

@Component({
  selector: 'Nimbus-modal',
  imports: [],
  templateUrl: './modal.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './modal.css',
  host: { '(document:keydown.escape)': 'onEscape()' },
})
export class Modal {
  isOpen = input(false);
  title = input.required<string>();
  description = input<string>('');
  /** Gets `role="alertdialog"` instead of `dialog`, for confirmations that need an answer. */
  alert = input(false);
  /** On narrow screens the dialog slides up from the bottom as a sheet (used by form dialogs). */
  sheet = input(false);
  closed = output<void>();

  protected readonly titleId = `modal-title-${nextId}`;
  protected readonly descriptionId = `modal-desc-${nextId++}`;

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private opener: HTMLElement | null = null;

  constructor() {
    // Focus moves into the dialog when it opens and returns to whatever opened it when it closes.
    effect(() => {
      if (this.isOpen()) {
        this.opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        queueMicrotask(() => {
          const root = this.host.nativeElement.querySelector<HTMLElement>('.modal');
          const target =
            root?.querySelector<HTMLElement>('[data-autofocus] button, [data-autofocus]') ??
            root?.querySelector<HTMLElement>(FOCUSABLE) ??
            root;
          target?.focus();
        });
      } else if (this.opener) {
        this.opener.focus();
        this.opener = null;
      }
    });
  }

  requestClose(): void {
    this.closed.emit();
  }

  onEscape(): void {
    if (this.isOpen()) {
      this.requestClose();
    }
  }

  /** Keeps Tab / Shift+Tab inside the dialog while it is open. */
  onKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Tab') {
      return;
    }
    const root = event.currentTarget as HTMLElement;
    const items = Array.from(root.querySelectorAll<HTMLElement>(FOCUSABLE));
    if (items.length === 0) {
      event.preventDefault();
      return;
    }
    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement;
    if (event.shiftKey && (active === first || active === root)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.requestClose();
    }
  }

  onBackdropSpace(event: Event): void {
    // Only for the backdrop itself: Space on a button inside must still activate that button.
    if (event.target !== event.currentTarget) {
      return;
    }
    event.preventDefault();
    this.requestClose();
  }

  onBackdropEnter(event: Event): void {
    if (event.target === event.currentTarget) {
      this.requestClose();
    }
  }
}
