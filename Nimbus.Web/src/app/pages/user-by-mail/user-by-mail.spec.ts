import { ComponentFixture, TestBed } from '@angular/core/testing';

import UserByMail from './user-by-mail';
import { OpenAPI } from '../../core/api-client';
import { BaseHttpRequest } from '../../core/api-client/core/BaseHttpRequest';
import { AngularHttpRequest } from '../../core/api-client/core/AngularHttpRequest';
import { provideHttpClient } from '@angular/common/http';

describe('UserByMail', () => {
  let component: UserByMail;
  let fixture: ComponentFixture<UserByMail>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserByMail],
      providers: [
        provideHttpClient(),
        { provide: OpenAPI, useValue: OpenAPI },
        { provide: BaseHttpRequest, useClass: AngularHttpRequest },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserByMail);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
