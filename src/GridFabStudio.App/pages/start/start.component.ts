import { Component } from '@angular/core';
import { PageController } from '../../shared/page-controller';

@Component({ selector: 'gridfab-start-page', templateUrl: './start.component.html' })
export class PlanningPageComponent extends PageController {
  constructor() { super(); this.initializePage('start'); }
}
