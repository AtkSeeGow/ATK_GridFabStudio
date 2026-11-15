import { Component } from '@angular/core';
import { PageController } from '../../shared/page-controller';

@Component({ selector: 'gridfab-design-page', templateUrl: './design.component.html' })
export class BinPageComponent extends PageController {
  constructor() { super(); this.initializePage('design'); }
}
