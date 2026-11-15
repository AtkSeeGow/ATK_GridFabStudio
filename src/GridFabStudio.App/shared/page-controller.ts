import { makerWorldCases, makerWorldSpecials, specialFilters } from "../data/catalog-data";
import type { Estimate, Inquiry, CaseInquiry, MakerWorldCaseCard, SpecialCategory, MakerWorldSpecialCard, Page, RenderKind, CaseFamily, CasePreviewState } from "./models";
import { Directive, EventEmitter, Output, ElementRef, OnDestroy, ViewChild, computed, signal } from '@angular/core';
import * as THREE from 'three';
import { createPreview } from "./model-preview";
import { STLLoader } from 'three/addons/loaders/STLLoader.js';

@Directive()
export class PageController implements OnDestroy {
  @ViewChild('viewport', { static: true }) private viewport!: ElementRef<HTMLDivElement>;
  @ViewChild('motionSection') set motionSection(section: ElementRef<HTMLElement> | undefined) {
    this.motionObserver?.disconnect();
    if (!section) return;
    this.motionObserver = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) {
        this.motionVisible.set(true);
        this.motionObserver?.disconnect();
      }
    }, { threshold: 0.25 });
    this.motionObserver.observe(section.nativeElement);
  }

  readonly busy = signal(false);
  readonly status = signal('');
  readonly renderMessage = signal('');
  readonly renderKind = signal<RenderKind>('');
  readonly download = signal('');
  readonly dimensions = signal('');
  readonly hasPreview = signal(false);
  readonly mode = signal<Page>('home');
  readonly baseplateSizeMode = signal<'grid' | 'mm'>('grid');
  readonly binHolePattern = signal<'none' | 'magnets' | 'screws'>('screws');
  readonly caseFamily = signal<CaseFamily>('modular');
  readonly caseHeight = signal(6);
  readonly caseHandle = signal('front');
  readonly caseInterior = signal('starter');
  readonly caseMaterial = signal('PETG HF');
  readonly caseColor = signal('graphite');
  readonly caseStacking = signal(true);
  readonly caseDrawer = signal(true);
  readonly caseWallMount = signal(false);
  readonly caseLabels = signal(true);
  readonly caseSeal = signal(false);
  readonly caseMagnets = signal(false);
  readonly casePreviewState = signal<CasePreviewState>('closed');
  readonly caseInquiryId = signal('');
  readonly caseCells = Array.from({ length: 20 });
  readonly makerWorldCases = makerWorldCases;
  readonly caseCatalogIndex = signal(0);
  readonly featuredCase = computed(() => this.makerWorldCases[this.caseCatalogIndex()] ?? this.makerWorldCases[0]);
  readonly specialCategory = signal<SpecialCategory>('all');
  readonly specialFilters = specialFilters;
  readonly makerWorldSpecials = makerWorldSpecials;
  readonly specialIndex = signal(0);
  readonly visibleSpecials = computed(() => {
    const category = this.specialCategory();
    return category === 'all' ? this.makerWorldSpecials : this.makerWorldSpecials.filter((item) => item.category === category);
  });
  readonly featuredSpecial = computed(() => this.visibleSpecials()[this.specialIndex()] ?? this.visibleSpecials()[0] ?? this.makerWorldSpecials[0]);
  readonly motionVisible = signal(false);
  readonly reducedMotion = typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  private motionObserver?: IntersectionObserver;
  readonly estimate = signal<Estimate | null>(null);
  readonly inquiryId = signal('');
  readonly inquiries = signal<Inquiry[]>([]);
  readonly caseInquiries = signal<CaseInquiry[]>([]);
  private selectedQuote?: FormData;
  private adminKey = '';
  private request?: AbortController;
  private disposePreview?: () => void;
  private specialTimer?: number;
  private caseCatalogTimer?: number;

  playShowcase(event: Event) {
    if (this.reducedMotion) return;
    const video = event.currentTarget as HTMLVideoElement;
    video.muted = true;
    void video.play().catch(() => undefined);
  }

  setSpecialCategory(category: SpecialCategory) {
    this.specialCategory.set(category);
    this.specialIndex.set(0);
    this.startSpecialRotation();
  }

  showSpecial(index: number) {
    this.specialIndex.set(index);
    this.startSpecialRotation();
  }

  stepSpecial(direction: number) {
    const count = this.visibleSpecials().length;
    if (!count) return;
    this.specialIndex.update((index) => (index + direction + count) % count);
    this.startSpecialRotation();
  }

  private startSpecialRotation() {
    this.stopSpecialRotation();
    if (this.reducedMotion || this.mode() !== 'specialized') return;
    this.specialTimer = window.setInterval(() => {
      const count = this.visibleSpecials().length;
      if (count > 1) this.specialIndex.update((index) => (index + 1) % count);
    }, 4500);
  }

  private stopSpecialRotation() {
    if (this.specialTimer === undefined) return;
    window.clearInterval(this.specialTimer);
    this.specialTimer = undefined;
  }

  showCaseCatalog(index: number) {
    this.caseCatalogIndex.set(index);
    this.startCaseCatalogRotation();
  }

  stepCaseCatalog(direction: number) {
    const count = this.makerWorldCases.length;
    this.caseCatalogIndex.update((index) => (index + direction + count) % count);
    this.startCaseCatalogRotation();
  }

  private startCaseCatalogRotation() {
    this.stopCaseCatalogRotation();
    if (this.reducedMotion || this.mode() !== 'case') return;
    this.caseCatalogTimer = window.setInterval(() => {
      this.caseCatalogIndex.update((index) => (index + 1) % this.makerWorldCases.length);
    }, 4500);
  }

  private stopCaseCatalogRotation() {
    if (this.caseCatalogTimer === undefined) return;
    window.clearInterval(this.caseCatalogTimer);
    this.caseCatalogTimer = undefined;
  }

  @Output() readonly navigate = new EventEmitter<Page>();

  select(mode: Page) { this.navigate.emit(mode); }

  initializePage(mode: Page) {
    if (mode === this.mode()) return;
    this.stopSpecialRotation();
    this.stopCaseCatalogRotation();
    this.clearResult();
    this.status.set('');
    this.resetQuote();
    this.inquiries.set([]);
    this.caseInquiries.set([]);
    this.adminKey = '';
    this.motionVisible.set(false);
    if (mode === 'case') {
      this.casePreviewState.set('closed');
      this.caseCatalogIndex.set(0);
    }
    this.mode.set(mode);
    if (mode === 'specialized') {
      this.specialIndex.set(0);
      this.startSpecialRotation();
    }
    if (mode === 'case') this.startCaseCatalogRotation();
    window.scrollTo(0, 0);
  }

  setBaseplateSizeMode(event: Event) {
    this.baseplateSizeMode.set((event.target as HTMLSelectElement).value === 'mm' ? 'mm' : 'grid');
    this.resetBaseplateResult();
  }

  resetBaseplateResult() {
    this.clearResult();
    this.status.set('');
  }

  setBinHolePattern(event: Event) {
    const value = (event.target as HTMLSelectElement).value;
    this.binHolePattern.set(value === 'none' ? 'none' : value === 'magnets' ? 'magnets' : 'screws');
    this.resetBinResult();
  }

  resetBinResult() {
    this.clearResult();
    this.status.set('');
  }

  setCaseFamily(family: CaseFamily) {
    this.caseFamily.set(family);
    if (family === 'rugged' && ![6, 12].includes(this.caseHeight())) this.caseHeight.set(6);
    this.caseDrawer.set(family === 'modular');
    this.caseWallMount.set(false);
    this.caseSeal.set(false);
    this.casePreviewState.set('closed');
    this.caseInquiryId.set('');
    this.status.set('');
  }

  setCaseHeight(event: Event) { this.caseHeight.set(Number((event.target as HTMLSelectElement).value)); this.caseInquiryId.set(''); }
  setCaseHandle(event: Event) { this.caseHandle.set((event.target as HTMLSelectElement).value); this.caseInquiryId.set(''); }
  setCaseInterior(event: Event) { this.caseInterior.set((event.target as HTMLSelectElement).value); this.caseInquiryId.set(''); }
  setCaseMaterial(event: Event) { this.caseMaterial.set((event.target as HTMLSelectElement).value); this.caseInquiryId.set(''); }
  setCaseColor(event: Event) { this.caseColor.set((event.target as HTMLSelectElement).value); this.caseInquiryId.set(''); }

  toggleCaseOption(option: 'stacking' | 'drawer' | 'wallMount' | 'labels' | 'seal' | 'magnets', event: Event) {
    const checked = (event.target as HTMLInputElement).checked;
    const target = {
      stacking: this.caseStacking, drawer: this.caseDrawer, wallMount: this.caseWallMount,
      labels: this.caseLabels, seal: this.caseSeal, magnets: this.caseMagnets
    }[option];
    target.set(checked);
    this.caseInquiryId.set('');
  }

  caseFamilyLabel() { return this.caseFamily() === 'rugged' ? '堅固攜帶箱' : '模組擴充箱'; }
  caseInteriorLabel() {
    return { starter: '基本盒組', custom: '客製配置', owned: '沿用既有盒', case: '僅外箱' }[this.caseInterior()] ?? '基本盒組';
  }
  caseHardwareLabel() { return this.caseFamily() === 'rugged' ? 'M3 螺絲組' : '9 支 3 × 30 mm 鋼銷'; }
  caseColorValue() {
    return { graphite: '#35414d', navy: '#245275', orange: '#b85b27', sand: '#77766f' }[this.caseColor()] ?? '#35414d';
  }
  casePreviewLabel() { return `${this.caseFamilyLabel()}，5 × 4 格，${this.caseHeight()}U，${this.caseHandleLabel()}`; }
  caseHandleLabel() {
    return { front: '前方提把', top: '上方提把', both: '雙提把', none: '無提把' }[this.caseHandle()] ?? '前方提把';
  }
  caseFeatureSummary() {
    const features = [this.caseStacking() && '堆疊鎖定', this.caseLabels() && '標籤', this.caseMagnets() && '磁鐵孔',
      this.caseFamily() === 'rugged' && this.caseSeal() && '密封圈',
      this.caseFamily() === 'modular' && this.caseDrawer() && '抽屜滑軌',
      this.caseFamily() === 'modular' && this.caseWallMount() && '壁掛板'].filter(Boolean);
    return features.length ? features.join('、') : '基本配置';
  }
  caseCompatibilityNote() {
    if (this.caseFamily() === 'modular' && this.caseHeight() === 4)
      return 'Compact 4U 使用部分專用五金與滑軌，不能假設和標準高度全部共用。';
    return `${this.caseMaterial()} 建議配置；正式公差、列印方向與五金會在報價前確認。`;
  }
  caseSourceUrl() {
    return this.caseFamily() === 'rugged'
      ? 'https://makerworld.com/zh-tw/models/2504163-gridfinity-case-system#profileId-2828010'
      : 'https://makerworld.com/zh-tw/models/2359984-gridfinity-storage-box-v2-5x4#profileId-2582008';
  }

  async submitCaseInquiry(event: Event, form: HTMLFormElement) {
    event.preventDefault();
    if (this.busy() || this.caseInquiryId() || !form.reportValidity()) return;
    const data = new FormData(form);
    this.busy.set(true);
    this.status.set('正在送出外箱配置。');
    try {
      const response = await this.checkedFetch('/api/quotes/case-inquiries', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({
          email: String(data.get('email') ?? ''), quantity: Number(data.get('quantity')),
          family: this.caseFamily(), height: this.caseHeight(), handle: this.caseHandle(),
          interior: this.caseInterior(), material: this.caseMaterial(), color: this.caseColor(),
          stacking: this.caseStacking(), drawer: this.caseFamily() === 'modular' && this.caseDrawer(),
          wallMount: this.caseFamily() === 'modular' && this.caseWallMount(), labels: this.caseLabels(),
          seal: this.caseFamily() === 'rugged' && this.caseSeal(), magnets: this.caseMagnets()
        })
      });
      const result = await response.json();
      this.caseInquiryId.set(result.id);
      this.status.set(`已收到外箱配置，詢價編號 ${result.id}`);
    } catch (error) {
      this.status.set(error instanceof Error ? error.message : '無法送出外箱配置。');
    } finally { this.busy.set(false); }
  }

  resetQuote() {
    this.clearResult();
    this.estimate.set(null);
    this.selectedQuote = undefined;
    this.inquiryId.set('');
    this.status.set('');
  }

  formatSize(model: Estimate['model']) {
    return [model.x, model.y, model.z].map(value => value.toFixed(1)).join(' × ');
  }

  formatGrams(value: number) {
    return value.toFixed(value >= 100 ? 0 : 1) + ' g';
  }

  formatPrintTime(seconds: number | null) {
    if (seconds === null || seconds === undefined) return '未提供';
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.ceil((seconds % 3600) / 60);
    return hours ? `${hours} 小時 ${minutes} 分` : `${minutes} 分`;
  }

  async estimateFile(event: Event, form: HTMLFormElement) {
    event.preventDefault();
    this.resetQuote();
    const data = new FormData(form);
    const file = data.get('file');
    if (!(file instanceof File) || file.size === 0 || file.size > 10 * 1024 * 1024) {
      this.status.set('請選擇一個不超過 10 MB 的 STL 檔案。');
      return;
    }
    await this.run('正在分析 STL。', async () => {
      const response = await this.checkedFetch('/api/quotes/estimate', { method: 'POST', body: data });
      const estimate: Estimate = await response.json();
      this.estimate.set(estimate);
      this.selectedQuote = data;
      this.dimensions.set(this.formatSize(estimate.model));
      const geometry = new STLLoader().parse(await file.arrayBuffer());
      try {
        const size = estimate.model;
        this.showModel(geometry, new THREE.Vector3(size.x, size.y, size.z));
        this.status.set('');
      } catch {
        geometry.dispose();
        this.status.set('無法顯示 3D 預覽，仍可核對外尺寸並詢價。');
      }
    });
  }

  async submitCustomInquiry(event: Event, form: HTMLFormElement) {
    event.preventDefault();
    if (this.busy() || !form.reportValidity()) return;
    const data = new FormData(form);
    await this.run('正在送出客製需求。', async () => {
      const response = await this.checkedFetch('/api/quotes/custom-inquiries', { method: 'POST', body: data });
      const { id } = await response.json();
      form.reset();
      this.status.set('已收到客製需求，詢價編號 ' + id);
    });
  }

  async submitInquiry(event: Event, form: HTMLFormElement) {
    event.preventDefault();
    if (!this.selectedQuote || this.inquiryId()) return;
    const data = new FormData(form);
    this.selectedQuote.forEach((value, key) => data.set(key, value));
    data.set('confirmMillimeters', 'true');
    await this.run('正在送出詢價。', async () => {
      const response = await this.checkedFetch('/api/quotes/inquiries', { method: 'POST', body: data });
      const { id } = await response.json();
      this.inquiryId.set(id);
      this.status.set('已收到詢價，編號 ' + id);
    });
  }

  async loadInquiries(event: Event, key: string) {
    event.preventDefault();
    this.inquiries.set([]);
    this.caseInquiries.set([]);
    this.adminKey = '';
    await this.run('正在讀取詢價。', async () => {
      const headers = { 'X-Quote-Admin-Key': key };
      const [modelResponse, caseResponse] = await Promise.all([
        this.checkedFetch('/api/quotes/admin/inquiries', { headers }),
        this.checkedFetch('/api/quotes/admin/case-inquiries', { headers })
      ]);
      this.inquiries.set(await modelResponse.json());
      this.caseInquiries.set(await caseResponse.json());
      this.adminKey = key;
      this.status.set(this.inquiries().length || this.caseInquiries().length ? '已載入詢價。' : '目前沒有詢價。');
    });
  }

  async confirmPrice(event: Event, id: string, amount: string) {
    event.preventDefault();
    await this.run('正在儲存。', async () => {
      const response = await this.adminFetch(id + '/price?amount=' + encodeURIComponent(amount), 'PUT');
      const item: Inquiry = await response.json();
      this.inquiries.update(items => items.map(existing => existing.id === id ? item : existing));
      this.status.set('正式價格已儲存。');
    });
  }

  async confirmCasePrice(event: Event, id: string, amount: string) {
    event.preventDefault();
    await this.run('正在儲存。', async () => {
      const response = await this.checkedFetch('/api/quotes/admin/case-inquiries/' + id + '/price?amount=' + encodeURIComponent(amount), {
        method: 'PUT', headers: { 'X-Quote-Admin-Key': this.adminKey }
      });
      const item: CaseInquiry = await response.json();
      this.caseInquiries.update(items => items.map(existing => existing.id === id ? item : existing));
      this.status.set('正式價格已儲存。');
    });
  }

  async downloadModel(id: string) {
    await this.run('正在下載。', async () => {
      const response = await this.adminFetch(id + '/model', 'GET');
      const url = URL.createObjectURL(await response.blob());
      const link = document.createElement('a');
      link.href = url;
      link.download = 'inquiry-' + id + '.stl';
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      this.status.set('已下載原始 STL。');
    });
  }

  private adminFetch(path: string, method: string) {
    return this.checkedFetch('/api/quotes/admin/inquiries/' + path, {
      method, headers: { 'X-Quote-Admin-Key': this.adminKey }
    });
  }

  private async checkedFetch(url: string, options: RequestInit) {
    const apiBase = new URL(document.baseURI).pathname === '/GridFabStudio.App/'
      ? '/GridFabStudio.Api' : '';
    const response = await fetch(apiBase.replace(/\/$/, '') + url, options);
    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.message || error.detail || `請求失敗（HTTP ${response.status}）。`);
    }
    return response;
  }

  private async run(message: string, operation: () => Promise<void>) {
    if (this.busy()) return;
    this.busy.set(true);
    this.status.set(message);
    try {
      await operation();
    } catch (error) {
      this.status.set(error instanceof Error ? error.message : '請求失敗，請重試。');
    } finally {
      this.busy.set(false);
    }
  }

  async generateBaseplate(event: Event, form: HTMLFormElement) {
    event.preventDefault();
    if (this.busy() || !form.reportValidity()) return;
    const query = new URLSearchParams();
    new FormData(form).forEach((value, key) => query.set(key, String(value)));
    const junctionHoles = form.elements.namedItem('junctionHoles') as HTMLInputElement;
    query.set('junctionHoles', String(junctionHoles.checked));
    this.clearResult();
    this.busy.set(true);
    this.renderKind.set('baseplate');
    this.renderMessage.set('正在建立底板格位與磁鐵孔');
    this.status.set('正在產生底板。');
    const request = this.request = new AbortController();

    try {
      const response = await this.checkedFetch('/api/models/baseplate?' + query, { signal: request.signal });
      const bytes = await response.arrayBuffer();
      if (request.signal.aborted) return;
      const geometry = new STLLoader().parse(bytes);
      geometry.computeBoundingBox();
      const size = geometry.boundingBox!.getSize(new THREE.Vector3());
      this.dimensions.set(size.toArray().map(value => value.toFixed(1)).join(' × '));
      this.download.set(URL.createObjectURL(new Blob([bytes], { type: 'model/stl' })));

      try {
        this.showModel(geometry, size);
        this.status.set('');
      } catch {
        geometry.dispose();
        this.status.set('目前無法顯示 3D 預覽，仍可下載 STL。');
      }
    } catch (error) {
      if (!request.signal.aborted)
        this.status.set(error instanceof Error ? error.message : '無法產生底板，請重試。');
    } finally {
      this.busy.set(false);
      this.renderMessage.set('');
      this.renderKind.set('');
    }
  }

  async generate(event: Event, form: HTMLFormElement) {
    event.preventDefault();
    if (this.busy() || !form.reportValidity()) return;
    const query = new URLSearchParams();
    new FormData(form).forEach((value, key) => query.set(key, String(value)));
    this.clearResult();
    this.busy.set(true);
    this.renderKind.set('bin');
    this.renderMessage.set('正在建立收納盒模型');
    this.status.set('正在產生模型。');
    const request = this.request = new AbortController();

    try {
      const response = await this.checkedFetch('/api/models/basic-bin?' + query, { signal: request.signal });
      const bytes = await response.arrayBuffer();
      if (request.signal.aborted) return;
      const geometry = new STLLoader().parse(bytes);
      geometry.computeBoundingBox();
      const size = geometry.boundingBox!.getSize(new THREE.Vector3());
      this.dimensions.set(size.toArray().map(value => value.toFixed(1)).join(' × '));
      this.download.set(URL.createObjectURL(new Blob([bytes], { type: 'model/stl' })));

      try {
        this.showModel(geometry, size);
        this.status.set('');
      } catch {
        geometry.dispose();
        this.status.set('目前無法顯示 3D 預覽，仍可下載 STL。');
      }
    } catch (error) {
      if (!request.signal.aborted)
        this.status.set(error instanceof Error ? error.message : '無法產生模型，請重試。');
    } finally {
      this.busy.set(false);
      this.renderMessage.set('');
      this.renderKind.set('');
    }
  }

  private showModel(geometry: THREE.BufferGeometry, size: THREE.Vector3) {
    geometry.center().rotateX(-Math.PI / 2);
    const material = new THREE.MeshStandardMaterial({ color: '#6f9db8', roughness: 0.64 });
    try {
      this.showScene(new THREE.Mesh(geometry, material), size, () => {
        geometry.dispose();
        material.dispose();
      });
    } catch (error) {
      material.dispose();
      throw error;
    }
  }

  private showScene(model: THREE.Object3D, size: THREE.Vector3, disposeModel: () => void, assembly = false) {
    const preview = createPreview(this.viewport.nativeElement, model, size, disposeModel, assembly);
    this.disposePreview = preview.dispose;
    this.hasPreview.set(true);
    return preview.render;
  }

  private clearResult() {
    this.disposePreview?.();
    this.disposePreview = undefined;
    if (this.download()) URL.revokeObjectURL(this.download());
    this.download.set('');
    this.dimensions.set('');
    this.hasPreview.set(false);
  }

  ngOnDestroy() {
    this.motionObserver?.disconnect();
    this.request?.abort();
    this.stopSpecialRotation();
    this.stopCaseCatalogRotation();
    this.clearResult();
  }
}
