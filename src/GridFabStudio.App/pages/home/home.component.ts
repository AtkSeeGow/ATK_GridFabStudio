import { Component } from '@angular/core';
import { PageController } from '../../shared/page-controller';

@Component({ selector: 'gridfab-home-page', templateUrl: './home.component.html' })
export class HomePageComponent extends PageController {
  constructor() { super(); this.initializePage('home'); }
}
