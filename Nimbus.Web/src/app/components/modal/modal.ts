import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'Nimbus-modal',
  imports: [],
  templateUrl: './modal.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './modal.css',
})
export class Modal {
  isOpen = input(false);
  title = input.required<string>();
  description = input<string>('');
  closed = output<void>();

  requestClose(): void {
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.requestClose();
    }
  }

  onBackdropSpace(event: Event): void {
    event.preventDefault();
    this.requestClose();
  }
}
