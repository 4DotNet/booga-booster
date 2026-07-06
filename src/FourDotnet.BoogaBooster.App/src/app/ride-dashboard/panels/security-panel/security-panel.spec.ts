import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LoadBalanceState, SecurityState } from '../../models/ride.models';
import { SecurityPanel } from './security-panel';

describe('SecurityPanel', () => {
  function render(
    passengers: number,
    securityState: SecurityState,
    totalKg: number,
    loadBalance: LoadBalanceState = 'safe',
  ): { host: HTMLElement; fixture: ComponentFixture<SecurityPanel> } {
    const fixture = TestBed.createComponent(SecurityPanel);
    fixture.componentRef.setInput('passengers', passengers);
    fixture.componentRef.setInput('securityState', securityState);
    fixture.componentRef.setInput('totalKg', totalKg);
    fixture.componentRef.setInput('loadBalance', loadBalance);
    fixture.detectChanges();
    return { host: fixture.nativeElement as HTMLElement, fixture };
  }

  it('renders the streamed boarded-passenger count and singular/plural wording', () => {
    const { host } = render(1, 'secured', 70);
    expect(host.textContent).toContain('1 passenger —');

    const { host: hostMany } = render(5, 'secured', 350);
    expect(hostMany.textContent).toContain('5 passengers —');
  });

  it('renders the streamed total weight in kilos', () => {
    const { host } = render(3, 'secured', 210);
    expect(host.textContent).toContain('210 kilos');
  });

  it('reflects an occupied-but-unsecured boarding state as unsafe, not colour alone', () => {
    const { host } = render(2, 'unsecured', 140);

    const loadRow = host.querySelector('.row[data-state="unsafe"]');
    expect(loadRow).not.toBeNull();
    expect(loadRow?.textContent).toContain('⚠');
    expect(loadRow?.textContent).toContain('unsafe');
  });

  it('reflects every occupied seat secured as safe once restraints close', () => {
    const { host } = render(2, 'secured', 140);

    const loadRow = host.querySelector('.row[data-state="safe"]');
    expect(loadRow).not.toBeNull();
    expect(loadRow?.textContent).toContain('✓');
  });

  it('updates the count, weight and security state as boarding progresses across frames', () => {
    const { fixture, host } = render(0, 'secured', 0);
    expect(host.textContent).toContain('0 passengers —');
    expect(host.textContent).toContain('0 kilos');

    // First passenger boards but has not yet secured the restraint.
    fixture.componentRef.setInput('passengers', 1);
    fixture.componentRef.setInput('securityState', 'unsecured');
    fixture.componentRef.setInput('totalKg', 70);
    fixture.detectChanges();
    expect(host.textContent).toContain('1 passenger —');
    expect(host.textContent).toContain('70 kilos');
    expect(host.querySelector('.row[data-state="unsafe"]')).not.toBeNull();

    // Restraint secures on a later frame.
    fixture.componentRef.setInput('securityState', 'secured');
    fixture.detectChanges();
    expect(host.querySelector('.row[data-state="safe"]')).not.toBeNull();
  });

  it('reflects the weight balance roll-up independently of security', () => {
    const { host } = render(4, 'secured', 280, 'unsafe');

    const weightRow = Array.from(host.querySelectorAll('.row')).find((row) =>
      row.textContent?.includes('Weight'),
    );
    expect(weightRow?.getAttribute('data-state')).toBe('unsafe');
    expect(weightRow?.textContent).toContain('⚠');
  });
});
