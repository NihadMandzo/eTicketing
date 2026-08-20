import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { FooterComponent } from '../footer/footer.component';
import { TopNavbarComponent } from '../top-navbar/top-navbar.component';

/** App shell: fixed top bar, routed page, shared footer. */
@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, TopNavbarComponent, FooterComponent],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.css'
})
export class LayoutComponent {}
