import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Glyph } from './glyph';

describe('Glyph', () => {
  let component: Glyph;
  let fixture: ComponentFixture<Glyph>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Glyph],
    }).compileComponents();

    fixture = TestBed.createComponent(Glyph);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('icon', 'ti-star');
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
