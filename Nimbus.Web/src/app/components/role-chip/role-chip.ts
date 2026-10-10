import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Read-only role label (mono chip). */
@Component({
  selector: 'Nimbus-role-chip',
  imports: [],
  templateUrl: './role-chip.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './role-chip.css',
})
export class RoleChip {}
