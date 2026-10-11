import { TestBed } from '@angular/core/testing';

import { DraftService } from './draft.service';

describe('DraftService', () => {
  let service: DraftService;

  beforeEach(() => {
    sessionStorage.clear();
    service = TestBed.inject(DraftService);
  });

  it('round-trips a draft and clears it', () => {
    service.save('flight', { name: 'Alps', pax: 2 });
    expect(service.load('flight')).toEqual({ name: 'Alps', pax: 2 });
    service.clear('flight');
    expect(service.load('flight')).toBeNull();
  });

  it('returns null for corrupt data', () => {
    sessionStorage.setItem('nimbus.draft.flight', '{not json');
    expect(service.load('flight')).toBeNull();
  });
});
