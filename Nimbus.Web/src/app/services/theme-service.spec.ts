import { TestBed } from '@angular/core/testing';
import { LOCAL_STORAGE, ThemeService } from './theme-service';

describe('ThemeService', () => {
  let service: ThemeService;
  const mockStore: Record<string, string> = {};
  const mockStorage = {
    getItem: (key: string) => mockStore[key] ?? null,
    setItem: (key: string, val: string) => { mockStore[key] = val; },
    removeItem: (key: string) => { delete mockStore[key]; },
    clear: () => { Object.keys(mockStore).forEach(k => delete mockStore[k]); },
  } as Storage;

  beforeEach(() => {
    mockStorage.clear();
    TestBed.configureTestingModule({
      providers: [{ provide: LOCAL_STORAGE, useValue: mockStorage }],
    });
    service = TestBed.inject(ThemeService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should be light mode by default', () => {
    expect(service.isDarkMode()).toBeFalsy();
  });
});
