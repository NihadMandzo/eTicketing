import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { ALL_CITIES, CITY_LABELS, City } from '../../core/models/catalog.models';

/**
 * City/location filter dropdown for the products listing page's 3-filter toolbar. Backed by the
 * fixed City enum (mirrors eTicketing.Contracts.Persistence.City) rather than a dedicated
 * /cities endpoint — the list is small (8 BiH cities) and effectively static, so a hardcoded
 * frontend array is a better tradeoff than an extra network round-trip and cache.
 */
@Component({
  selector: 'app-location-select',
  templateUrl: './location-select.component.html',
  styleUrl: './location-select.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LocationSelectComponent {
  readonly selectedCity = input<City | null>(null);
  readonly citySelected = output<City | null>();

  readonly cities = ALL_CITIES;
  readonly labels = CITY_LABELS;

  onChange(value: string): void {
    this.citySelected.emit(value === '' ? null : (value as City));
  }
}
