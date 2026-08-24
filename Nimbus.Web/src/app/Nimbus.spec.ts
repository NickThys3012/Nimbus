import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Nimbus } from './Nimbus';
import { LOCAL_STORAGE } from './services/theme-service';

const store: Record<string, string> = {};
const mockStorage = {
  getItem: (key: string) => store[key] ?? null,
  setItem: (key: string, value: string) => {
    store[key] = value;
  },
  removeItem: (key: string) => {
    delete store[key];
  },
  clear: () => {
    Object.keys(store).forEach((key) => delete store[key]);
  },
  length: 0,
  key: () => null,
} as unknown as Storage;

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Nimbus],
      providers: [provideRouter([]), { provide: LOCAL_STORAGE, useValue: mockStorage }],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(Nimbus);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the router outlet', async () => {
    const fixture = TestBed.createComponent(Nimbus);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });
});
