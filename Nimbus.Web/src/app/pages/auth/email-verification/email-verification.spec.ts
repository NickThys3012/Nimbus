import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import EmailVerification from './email-verification';

describe('EmailVerification', () => {
  async function createComponent(status: 'success' | 'error') {
    await TestBed.configureTestingModule({
      imports: [EmailVerification],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap({ status }),
            },
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(EmailVerification);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('should create', async () => {
    const fixture = await createComponent('success');
    const component = fixture.componentInstance;
    expect(component).toBeTruthy();
  });

  it('renders success message when status=success', async () => {
    const fixture: ComponentFixture<EmailVerification> = await createComponent('success');
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('#verification-success')).not.toBeNull();
    expect(element.querySelector('#verification-error')).toBeNull();
    expect(element.querySelector('#resend-verification-link')).toBeNull();
  });

  it('renders error message and resend CTA when status=error', async () => {
    const fixture: ComponentFixture<EmailVerification> = await createComponent('error');
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('#verification-success')).toBeNull();
    expect(element.querySelector('#verification-error')).not.toBeNull();
    expect(element.querySelector('#resend-verification-link')).not.toBeNull();
  });
});

