import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Site footer. Lives in the shell (LayoutComponent) rather than on the
 * landing page, so support/contact links are reachable from every screen —
 * they used to exist only on `/`, which meant a buyer sitting on checkout or
 * a product page had no route to Pomoć or Uslovi korištenja at all.
 */
@Component({
  selector: 'app-footer',
  imports: [RouterLink],
  templateUrl: './footer.component.html',
  styleUrl: './footer.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FooterComponent {
  readonly year = new Date().getFullYear();
}
