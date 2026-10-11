import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type AvatarTone = 'default' | 'pending' | 'danger';

@Component({
  selector: 'Nimbus-initials-avatar',
  imports: [],
  templateUrl: './initials-avatar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './initials-avatar.css',
  host: {
    '[style.--avatar-size.px]': 'size()',
  },
})
export class InitialsAvatar {
  initials = input.required<string>();
  tone = input<AvatarTone>('default');
  /** Diameter in px. */
  size = input<number>(34);
}
