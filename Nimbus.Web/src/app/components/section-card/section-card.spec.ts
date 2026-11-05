import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SectionCard } from './section-card';

describe('SectionCard', () => {
  let fixture: ComponentFixture<SectionCard>;
  let component: SectionCard;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [SectionCard] });
    fixture = TestBed.createComponent(SectionCard);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('sectionId', 'General');
    fixture.componentRef.setInput('order', 1);
    fixture.componentRef.setInput('title', 'General');
    fixture.detectChanges();
  });

  it('starts collapsed by default and does not render the body', () => {
    expect(component.expanded()).toBe(false);
    expect(fixture.nativeElement.querySelector('.card-body')).toBeNull();
  });

  it('toggles expanded state, showing and hiding the body', () => {
    fixture.nativeElement.querySelector('.card-head').click();
    fixture.detectChanges();
    expect(component.expanded()).toBe(true);
    expect(fixture.nativeElement.querySelector('.card-body')).not.toBeNull();

    fixture.nativeElement.querySelector('.card-head').click();
    fixture.detectChanges();
    expect(component.expanded()).toBe(false);
    expect(fixture.nativeElement.querySelector('.card-body')).toBeNull();
  });

  it('exposes the completion status as a dot class', () => {
    fixture.componentRef.setInput('status', 'complete');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.dot.complete')).not.toBeNull();

    fixture.componentRef.setInput('status', 'invalid');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.dot.invalid')).not.toBeNull();
  });

  it('pads the section number to two digits', () => {
    fixture.componentRef.setInput('order', 9);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sec-num').textContent.trim()).toBe('09');
  });

  it('sets data-section on the host for focus targeting', () => {
    expect((fixture.nativeElement as HTMLElement).getAttribute('data-section')).toBe('General');
  });

  it('renders no badge by default', () => {
    expect(fixture.nativeElement.querySelector('[data-testid="section-badge"]')).toBeNull();
  });

  it('renders an "EDITED" badge when badge input is "edited"', () => {
    fixture.componentRef.setInput('badge', 'edited');
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('[data-testid="section-badge"]');
    expect(badge).not.toBeNull();
    expect(badge.classList.contains('from-draft')).toBe(false);
    expect(badge.textContent.trim()).toContain('EDITED');
  });

  it('renders a "FROM DRAFT" badge when badge input is "from-draft"', () => {
    fixture.componentRef.setInput('badge', 'from-draft');
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('[data-testid="section-badge"]');
    expect(badge).not.toBeNull();
    expect(badge.classList.contains('from-draft')).toBe(true);
    expect(badge.textContent.trim()).toContain('FROM DRAFT');
  });
});
