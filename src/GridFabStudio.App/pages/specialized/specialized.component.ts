import { Component } from '@angular/core';
import { PageController } from '../../shared/page-controller';

@Component({ selector: 'gridfab-specialized-page', templateUrl: './specialized.component.html' })
export class SpecializedPageComponent extends PageController {
  constructor() { super(); this.initializePage('specialized'); }
}
