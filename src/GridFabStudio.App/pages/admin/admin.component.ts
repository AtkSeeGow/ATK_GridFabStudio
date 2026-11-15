import { Component } from '@angular/core';
import { PageController } from '../../shared/page-controller';

@Component({ selector: 'gridfab-admin-page', templateUrl: './admin.component.html' })
export class AdminPageComponent extends PageController {
  constructor() { super(); this.initializePage('admin'); }
}
