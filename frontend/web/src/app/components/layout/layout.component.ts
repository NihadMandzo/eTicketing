import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TopNavbarComponent } from '../top-navbar/top-navbar.component';
import { BottomNavbarComponent } from '../bottom-navbar/bottom-navbar.component';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, TopNavbarComponent, BottomNavbarComponent],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.css'
})
export class LayoutComponent {
  
}
