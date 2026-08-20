import { toPublishStatus, toTicketingMode } from '../models/catalog.models';
import { toTicketStatus } from '../models/purchase.models';
import { coerceEnum } from './api-enum.util';

/**
 * These cover the exact regression that made the product-details purchase
 * card render empty: the API sends `"ticketingMode": 0`, the templates
 * `@switch` on `'SingleOccurrence'`, and TypeScript's type annotation does
 * nothing about it at runtime.
 */
describe('coerceEnum', () => {
  const NAMES = ['Alpha', 'Beta', 'Gamma'] as const;

  it('maps an integer ordinal onto the name at that position', () => {
    expect(coerceEnum(0, NAMES, 'Alpha')).toBe('Alpha');
    expect(coerceEnum(1, NAMES, 'Alpha')).toBe('Beta');
    expect(coerceEnum(2, NAMES, 'Alpha')).toBe('Gamma');
  });

  it('maps a numeric string ordinal the same way', () => {
    expect(coerceEnum('1', NAMES, 'Alpha')).toBe('Beta');
  });

  it('passes a known name straight through', () => {
    expect(coerceEnum('Gamma', NAMES, 'Alpha')).toBe('Gamma');
  });

  it('falls back for an out-of-range ordinal', () => {
    expect(coerceEnum(9, NAMES, 'Alpha')).toBe('Alpha');
    expect(coerceEnum(-1, NAMES, 'Alpha')).toBe('Alpha');
  });

  it('falls back for an unknown name, null, undefined and empty string', () => {
    expect(coerceEnum('Delta', NAMES, 'Alpha')).toBe('Alpha');
    expect(coerceEnum(null, NAMES, 'Alpha')).toBe('Alpha');
    expect(coerceEnum(undefined, NAMES, 'Alpha')).toBe('Alpha');
    expect(coerceEnum('', NAMES, 'Alpha')).toBe('Alpha');
  });
});

describe('toTicketingMode', () => {
  it('matches the backend TicketingMode ordinals', () => {
    expect(toTicketingMode(0)).toBe('SingleOccurrence');
    expect(toTicketingMode(1)).toBe('DailyEntry');
    expect(toTicketingMode(2)).toBe('RecurringReservation');
  });

  it('accepts names too, in case the backend adds JsonStringEnumConverter', () => {
    expect(toTicketingMode('RecurringReservation')).toBe('RecurringReservation');
  });

  it('falls back to SingleOccurrence on anything unrecognised', () => {
    expect(toTicketingMode(99)).toBe('SingleOccurrence');
    expect(toTicketingMode(null)).toBe('SingleOccurrence');
  });
});

describe('toPublishStatus', () => {
  it('matches the backend PublishStatus ordinals', () => {
    expect(toPublishStatus(0)).toBe('Draft');
    expect(toPublishStatus(1)).toBe('Published');
  });

  it('falls back to Published, since public GETs only ever return published rows', () => {
    expect(toPublishStatus(undefined)).toBe('Published');
  });
});

describe('toTicketStatus', () => {
  it('matches the backend TicketStatus ordinals', () => {
    expect(toTicketStatus(0)).toBe('Processing');
    expect(toTicketStatus(1)).toBe('Confirmed');
    expect(toTicketStatus(2)).toBe('Ready');
    expect(toTicketStatus(3)).toBe('Cancelled');
  });

  it('falls back to Confirmed', () => {
    expect(toTicketStatus('nonsense')).toBe('Confirmed');
  });
});
