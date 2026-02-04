import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-bottom-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './bottom-navbar.component.html',
  styleUrl: './bottom-navbar.component.css'
})
export class BottomNavbarComponent {
  
}
