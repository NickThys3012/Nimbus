import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Banner } from './banner';

describe('Banner', () => {
  let component: Banner;
  let fixture: ComponentFixture<Banner>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Banner],
    }).compileComponents();

    fixture = TestBed.createComponent(Banner);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('id', 'test-banner');
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('uses the error palette for type="error"', () => {
    fixture.componentRef.setInput('type', 'error');
    fixture.detectChanges();
    expect(component.backgroundColor()).toBe('var(--rb)');
    expect(component.color()).toBe('var(--r)');
  });
});
