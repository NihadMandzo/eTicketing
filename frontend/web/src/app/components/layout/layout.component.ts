import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { FooterComponent } from '../footer/footer.component';
import { TopNavbarComponent } from '../top-navbar/top-navbar.component';

/** App shell: fixed top bar, routed page, shared footer. Never a bootstrap root itself — `main.ts`
 * bootstraps `App`, and this is only ever reached via the Router — so it carries no state of its
 * own that OnPush could go stale on; TopNavbarComponent/FooterComponent below it manage their own
 * change detection independently. */
@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, TopNavbarComponent, FooterComponent],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LayoutComponent {}
