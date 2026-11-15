import { Component } from '@angular/core';
import { PageController } from '../../shared/page-controller';

@Component({ selector: 'gridfab-gridfinity-page', templateUrl: './gridfinity.component.html' })
export class GridfinityPageComponent extends PageController {
  constructor() { super(); this.initializePage('gridfinity'); }
}
