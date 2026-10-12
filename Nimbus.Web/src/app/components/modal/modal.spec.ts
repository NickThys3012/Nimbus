import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Modal } from './modal';

@Component({
  imports: [Modal],
  template: `
    <button id="opener" type="button">open</button>
    <Nimbus-modal [isOpen]="open()" (closed)="closes = closes + 1" title="Test modal" description="Hello" [alert]="true">
      <div modal-body><input id="field" /></div>
      <div modal-actions>
        <button id="a" type="button">A</button>
        <button id="b" type="button">B</button>
      </div>
    </Nimbus-modal>
  `,
})
class Host {
  open = signal(false);
  closes = 0;
}

describe('Modal', () => {
  let component: Modal;
  let fixture: ComponentFixture<Modal>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Modal, Host],
    }).compileComponents();

    fixture = TestBed.createComponent(Modal);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('title', 'Test modal');
    fixture.componentRef.setInput('isOpen', true);
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('accessibility', () => {
    let host: ComponentFixture<Host>;
    let el: HTMLElement;

    beforeEach(async () => {
      host = TestBed.createComponent(Host);
      document.body.appendChild(host.nativeElement);
      el = host.nativeElement;
      await host.whenStable();
      (el.querySelector('#opener') as HTMLElement).focus();
      host.componentInstance.open.set(true);
      await host.whenStable();
      await Promise.resolve();
    });

    afterEach(() => host.nativeElement.remove());

    it('is a labelled modal alertdialog and focuses inside on open', () => {
      const dialog = el.querySelector('[role="alertdialog"]') as HTMLElement;
      expect(dialog.getAttribute('aria-modal')).toBe('true');
      expect(el.querySelector(`#${dialog.getAttribute('aria-labelledby')}`)?.textContent).toContain('Test modal');
      expect(dialog.contains(document.activeElement)).toBe(true);
    });

    it('traps Tab inside the dialog', () => {
      const last = el.querySelector('#b') as HTMLElement;
      last.focus();
      const event = new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true });
      last.dispatchEvent(event);
      expect(event.defaultPrevented).toBe(true);
      expect(document.activeElement).toBe(el.querySelector('#field'));
    });

    it('closes on Escape and gives focus back to the opener', async () => {
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
      expect(host.componentInstance.closes).toBe(1);
      host.componentInstance.open.set(false);
      await host.whenStable();
      expect(document.activeElement).toBe(el.querySelector('#opener'));
    });

    it('does not close when Enter or Space activates a button inside', () => {
      const btn = el.querySelector('#a') as HTMLElement;
      btn.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
      btn.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true }));
      expect(host.componentInstance.closes).toBe(0);
    });
  });
});
