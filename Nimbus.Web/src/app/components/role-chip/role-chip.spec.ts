import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RoleChip } from './role-chip';

@Component({ imports: [RoleChip], template: `<Nimbus-role-chip>pilot</Nimbus-role-chip>` })
class Host {}

describe('RoleChip', () => {
  it('projects the role label', async () => {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).querySelector('.role-chip')?.textContent).toContain('pilot');
  });
});
