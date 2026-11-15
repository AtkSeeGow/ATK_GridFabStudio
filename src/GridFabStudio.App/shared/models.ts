export interface Estimate {
  model: { x: number; y: number; z: number; triangles: number; volumeCm3: number | null };
  quantity: number;
  amount: number | null;
  reviewReason: string | null;
  material: string;
  filamentGrams: number | null;
  printTimeSeconds: number | null;
  ratePerGram: number | null;
  slicerProfile: string | null;
}

export interface Inquiry {
  id: string;
  createdAt: string;
  email: string;
  fileName: string;
  estimate: Estimate;
  note: string | null;
  confirmedAmount: number | null;
}

export interface CaseInquiry {
  id: string;
  createdAt: string;
  email: string;
  configuration: {
    family: 'rugged' | 'modular'; height: number; handle: string; interior: string;
    material: string; color: string; quantity: number; stacking: boolean;
    drawer: boolean; wallMount: boolean; labels: boolean; seal: boolean; magnets: boolean;
  };
  confirmedAmount: number | null;
}

export interface MakerWorldCaseCard {
  title: string;
  badge: string;
  meta: string;
  description: string;
  url: string;
  image: string;
}

export type SpecialCategory = 'all' | 'tools' | 'precision' | 'electronics' | 'supplies';

export interface MakerWorldSpecialCard extends MakerWorldCaseCard {
  category: Exclude<SpecialCategory, 'all'>;
  author: string;
}

export type Page = 'home' | 'start' | 'baseplate' | 'design' | 'specialized' | 'case' | 'quote' | 'admin' | 'gridfinity';
export type RenderKind = '' | 'baseplate' | 'bin';
export type CaseFamily = 'rugged' | 'modular';
export type CasePreviewState = 'closed' | 'open' | 'stacked' | 'mounted';
