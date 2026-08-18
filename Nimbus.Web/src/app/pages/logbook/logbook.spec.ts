import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Logbook } from './logbook';

describe('Logbook', () => {
  let component: Logbook;
  let fixture: ComponentFixture<Logbook>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Logbook],
    }).compileComponents();

    fixture = TestBed.createComponent(Logbook);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
