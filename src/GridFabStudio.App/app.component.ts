import { Component, signal } from '@angular/core';
import type { Page } from './shared/models';
import { HomePageComponent } from './pages/home/home.component';
import { PlanningPageComponent } from './pages/start/start.component';
import { BaseplatePageComponent } from './pages/baseplate/baseplate.component';
import { BinPageComponent } from './pages/design/design.component';
import { SpecializedPageComponent } from './pages/specialized/specialized.component';
import { CasePageComponent } from './pages/case/case.component';
import { GridfinityPageComponent } from './pages/gridfinity/gridfinity.component';
import { QuotePageComponent } from './pages/quote/quote.component';
import { AdminPageComponent } from './pages/admin/admin.component';

@Component({
  selector: 'gridfab-app',
  imports: [HomePageComponent, PlanningPageComponent, BaseplatePageComponent, BinPageComponent, SpecializedPageComponent, CasePageComponent, GridfinityPageComponent, QuotePageComponent, AdminPageComponent],
  templateUrl: './app.component.html'
})
export class App {
  readonly mode = signal<Page>('home');
  readonly busy = () => false;
  select(page: Page) { this.mode.set(page); window.scrollTo(0,0); }
}
