import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';

import type { FlightImageDto } from '../api-client';
import { FlightImagesApi, type UploadEvent } from './flight-images.api';
import { FlightImagesEditor, MAX_IMAGE_BYTES } from './flight-images.editor';

const image = (id: string, order: number, section = 'Meteo'): FlightImageDto => ({
  id, section, order, contentType: 'image/png', url: `https://f.test/${id}`, thumbnailUrl: `https://f.test/${id}.thumb`, sizeBytes: 2048, uploadedAt: null,
});
const png = (name = 'stuve.png', size = 1000) => new File([new Uint8Array(size)], name, { type: 'image/png' });

describe('FlightImagesEditor (issue #50)', () => {
  let api: { list: ReturnType<typeof vi.fn>; upload: ReturnType<typeof vi.fn>; reorder: ReturnType<typeof vi.fn>; remove: ReturnType<typeof vi.fn> };
  let editor: FlightImagesEditor;

  beforeEach(() => {
    api = {
      list: vi.fn().mockReturnValue(of([image('b', 1), image('a', 0), image('r', 0, 'Route')])),
      upload: vi.fn(),
      reorder: vi.fn().mockReturnValue(of(undefined)),
      remove: vi.fn().mockReturnValue(of(undefined)),
    };
    TestBed.configureTestingModule({ providers: [FlightImagesEditor, { provide: FlightImagesApi, useValue: api }] });
    editor = TestBed.inject(FlightImagesEditor);
  });

  it('loads only its own section, in order', () => {
    editor.bind('f1', 'Meteo');
    expect(editor.images().map((i) => i.id)).toEqual(['a', 'b']);
  });

  it('has nothing to attach to before the flight is saved, and loads nothing', () => {
    editor.bind(null, 'Meteo');
    expect(editor.hasFlight()).toBe(false);
    expect(api.list).not.toHaveBeenCalled();
  });

  it('shows progress while an upload runs, then the stored image in its place', () => {
    const run = new Subject<UploadEvent>();
    api.upload.mockReturnValue(run);
    editor.bind('f1', 'Meteo');

    editor.add([png()]);
    expect(editor.uploads()).toMatchObject([{ fileName: 'stuve.png', state: 'uploading', percent: 0 }]);
    expect(api.upload).toHaveBeenCalledWith('f1', 'Meteo', expect.any(File));

    run.next({ kind: 'progress', percent: 40 });
    expect(editor.uploads()[0].percent).toBe(40);

    run.next({ kind: 'done', image: image('n', 2) });
    expect(editor.uploads()).toEqual([]);
    expect(editor.images().map((i) => i.id)).toEqual(['a', 'b', 'n']);
  });

  it('cancels a running upload: the request is aborted and nothing is added', () => {
    let unsubscribed = false;
    // Observe the teardown of the observable the editor subscribed to: unsubscribing is what aborts the HTTP request.
    api.upload.mockReturnValue({
      subscribe: () => ({ unsubscribe: () => (unsubscribed = true) }),
    });
    editor.bind('f1', 'Meteo');
    editor.add([png()]);

    editor.cancel(editor.uploads()[0].id);

    expect(unsubscribed).toBe(true);
    expect(editor.uploads()).toEqual([]);
    expect(editor.images().map((i) => i.id)).toEqual(['a', 'b']);
  });

  it('keeps a failed upload visible with the server reason, leaves the other images alone, and can retry it', () => {
    api.upload.mockReturnValueOnce(
      throwError(() => new HttpErrorResponse({ status: 422, error: { errors: { file: ['The file is not a valid image.'] } } })),
    );
    editor.bind('f1', 'Meteo');
    editor.add([png()]);

    expect(editor.uploads()).toMatchObject([{ state: 'failed', error: 'The file is not a valid image.' }]);
    expect(editor.images().map((i) => i.id)).toEqual(['a', 'b']);

    api.upload.mockReturnValueOnce(of({ kind: 'done', image: image('n', 2) } as UploadEvent));
    editor.retry(editor.uploads()[0].id);
    expect(editor.uploads()).toEqual([]);
    expect(editor.images().map((i) => i.id)).toEqual(['a', 'b', 'n']);
  });

  it('explains an unreachable server and an unavailable file store', () => {
    api.upload.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 0 })));
    api.upload.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 502 })));
    editor.bind('f1', 'Meteo');
    editor.add([png('a.png'), png('b.png')]);

    expect(editor.uploads()[0].error).toContain('could not be reached');
    expect(editor.uploads()[1].error).toContain('file store is unavailable');
  });

  it('refuses a wrong type, an oversized file and an empty file without calling the API', () => {
    editor.bind('f1', 'Meteo');
    editor.add([
      new File(['x'], 'notes.pdf', { type: 'application/pdf' }),
      new File([new Uint8Array(MAX_IMAGE_BYTES + 1)], 'huge.png', { type: 'image/png' }),
      new File([], 'empty.png', { type: 'image/png' }),
    ]);

    expect(api.upload).not.toHaveBeenCalled();
    expect(editor.uploads().map((u) => u.error)).toEqual([
      'Only JPEG, PNG and WebP images can be attached.',
      'This image is larger than 10 MB.',
      'This file is empty.',
    ]);
  });

  it('moves an image and saves the whole section order', () => {
    editor.bind('f1', 'Meteo');
    editor.move('b', -1);

    expect(api.reorder).toHaveBeenCalledWith('f1', 'Meteo', ['b', 'a']);
    expect(editor.images().map((i) => [i.id, i.order])).toEqual([['b', 0], ['a', 1]]);
  });

  it('does not move past either end', () => {
    editor.bind('f1', 'Meteo');
    editor.move('a', -1);
    editor.move('b', 1);
    expect(api.reorder).not.toHaveBeenCalled();
  });

  it('keeps the old order and says so when saving the order fails', () => {
    api.reorder.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 422, error: { errors: { imageIds: ['Reload and try again.'] } } })));
    editor.bind('f1', 'Meteo');
    editor.move('b', -1);

    expect(editor.images().map((i) => i.id)).toEqual(['a', 'b']);
    expect(editor.problem()).toBe('Reload and try again.');
    expect(editor.busyId()).toBeNull();
  });

  it('removes an image, and keeps it with a message when the removal fails', () => {
    editor.bind('f1', 'Meteo');
    editor.remove('a');
    expect(api.remove).toHaveBeenCalledWith('f1', 'a');
    expect(editor.images().map((i) => i.id)).toEqual(['b']);

    api.remove.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 502 })));
    editor.remove('b');
    expect(editor.images().map((i) => i.id)).toEqual(['b']);
    expect(editor.problem()).toContain('file store is unavailable');
  });
});
