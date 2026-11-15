import { Component } from '@angular/core';
import { PageController } from '../../shared/page-controller';

@Component({ selector: 'gridfab-case-page', templateUrl: './case.component.html' })
export class CasePageComponent extends PageController {
  constructor() { super(); this.initializePage('case'); }
}
