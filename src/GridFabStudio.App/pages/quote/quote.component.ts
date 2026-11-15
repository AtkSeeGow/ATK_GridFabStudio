import { Component } from '@angular/core';
import { PageController } from '../../shared/page-controller';

@Component({ selector: 'gridfab-quote-page', templateUrl: './quote.component.html' })
export class QuotePageComponent extends PageController {
  constructor() { super(); this.initializePage('quote'); }
}
