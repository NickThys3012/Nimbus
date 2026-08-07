import { ComponentFixture, TestBed } from '@angular/core/testing';

import { UserByMail } from './user-by-mail';

describe('UserByMail', () => {
  let component: UserByMail;
  let fixture: ComponentFixture<UserByMail>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserByMail],
    }).compileComponents();

    fixture = TestBed.createComponent(UserByMail);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
