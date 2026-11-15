import { Component } from '@angular/core';
import { PageController } from '../../shared/page-controller';

@Component({ selector: 'gridfab-baseplate-page', templateUrl: './baseplate.component.html' })
export class BaseplatePageComponent extends PageController {
  constructor() { super(); this.initializePage('baseplate'); }
}
