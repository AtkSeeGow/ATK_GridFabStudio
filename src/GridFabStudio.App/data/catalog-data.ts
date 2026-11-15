import type { MakerWorldCaseCard, MakerWorldSpecialCard, SpecialCategory } from "../shared/models";
export const makerWorldCases: MakerWorldCaseCard[] = [
    {
      title: 'Gridfinity Case System', badge: '堅固攜帶', meta: '5 × 4｜Standard／Fat Boy',
      description: '雙鎖扣、加固鉸鏈、密封與完整堆疊設計。', image: 'media/makerworld-case-system.webp',
      url: 'https://makerworld.com/zh-tw/models/2504163-gridfinity-case-system#profileId-2828010'
    },
    {
      title: 'Schubox V2', badge: '模組主力', meta: '5 × 4｜6U／9U／12U／15U',
      description: '具滑軌、壁掛、堆疊鎖定與雙向提把的完整系統。', image: 'media/makerworld-schubox-v2.webp',
      url: 'https://makerworld.com/zh-tw/models/2359984-gridfinity-storage-box-v2-5x4#profileId-2582008'
    },
    {
      title: 'Schubox Slim V2', badge: '薄型工具', meta: '5 × 4｜4U',
      description: '適合刀具、熱端與精密工具的低矮外箱。', image: 'media/makerworld-schubox-slim.webp',
      url: 'https://makerworld.com/zh-tw/models/2467559-schubox-slim-v2-5x4#profileId-2708889'
    },
    {
      title: 'Schubox V2 Large', badge: '大型容量', meta: '7 × 6｜H 系列列印機',
      description: '更大的 Gridfinity 格位，適合設備與多組工具。', image: 'media/makerworld-schubox-large.webp',
      url: 'https://makerworld.com/zh-tw/models/2488518-schubox-v2-large-7x6#profileId-2734255'
    },
    {
      title: 'Schubox V2 5×5', badge: '方形配置', meta: '5 × 5｜6U',
      description: '增加一列格位，適合較深工作台與方形配置。', image: 'media/makerworld-schubox-5x5.webp',
      url: 'https://makerworld.com/zh-tw/models/2472040-shoebox-v2-5x5#profileId-2714271'
    },
    {
      title: 'Schubox V2 Lite', badge: '輕量版本', meta: '5 × 4｜減少耗材',
      description: '保留主要使用方式，降低外箱重量與列印材料。', image: 'media/makerworld-schubox-lite.webp',
      url: 'https://makerworld.com/zh-tw/models/3202304-schubox-v2-5x4-lite#profileId-3623921'
    },
    {
      title: 'Schubox V2 Gridless', badge: '泡棉內襯', meta: '5 × 4 外廓｜無格位',
      description: '移除內部 Gridfinity，適合泡棉與特殊設備內襯。', image: 'media/makerworld-schubox-gridless.webp',
      url: 'https://makerworld.com/zh-tw/models/2399515-schubox-v2-gridless#profileId-2629851'
    },
    {
      title: 'Schubox Open Drawer', badge: '快速取用', meta: '5 × 4｜6U 抽屜',
      description: '常駐工作區的開放式抽屜，不需要每次開蓋。', image: 'media/makerworld-schubox-drawer.webp',
      url: 'https://makerworld.com/zh-tw/models/2399756-schubox-open-drawer-v2-5x4#profileId-2630110'
    },
    {
      title: 'Schubox V2 M3 Mod', badge: '螺絲版本', meta: '5 × 4｜M3 螺栓',
      description: '以 M3 螺栓取代鋼銷，適合不同材料與維修需求。', image: 'media/makerworld-schubox-m3.webp',
      url: 'https://makerworld.com/zh-tw/models/2468017-schubox-v2-5x4-m3-bolt-mod#profileId-2709435'
    }
  ];
export const specialFilters: ReadonlyArray<{ id: SpecialCategory; label: string }> = [
    { id: 'all', label: '全部' },
    { id: 'tools', label: '手工具' },
    { id: 'precision', label: '精密工具' },
    { id: 'electronics', label: '電子設備' },
    { id: 'supplies', label: '耗材' }
  ];
export const makerWorldSpecials: MakerWorldSpecialCard[] = [
    {
      title: 'Bambu Screwdriver Set Holder', badge: '手工具', meta: '3 × 4｜螺絲起子與批頭', author: 'ADYDesigns', category: 'tools',
      description: '讓握柄、長短批頭與配件各自固定，抽屜打開就能立刻取用。', image: 'media/makerworld-special-screwdrivers.jpg',
      url: 'https://makerworld.com/en/models/2799489-bambu-screwdriver-set-gridfinity-holder'
    },
    {
      title: 'Deburring & Trimming Kit Holder', badge: '手工具', meta: '2 × 4｜修邊刀與刀片', author: 'ADYDesigns', category: 'tools',
      description: '整合修邊握柄、替換刀頭與備用刀片空間，避免尖銳配件散落。', image: 'media/makerworld-special-deburring.jpg',
      url: 'https://makerworld.com/en/models/2627298-gridfinity-holder-bambu-deburring-trimming-kit'
    },
    {
      title: 'Needle File Set Holder', badge: '精密工具', meta: '2 × 4｜四件式銼刀', author: 'ADYDesigns', category: 'precision',
      description: '依銼刀輪廓分槽固定，保留手指取放空間，也能快速確認缺少哪一支。', image: 'media/makerworld-special-files.jpg',
      url: 'https://makerworld.com/en/models/2627297-gridfinity-holder-bambu-4-piece-needle-file-set'
    },
    {
      title: 'Hand Drill Kit Holder', badge: '精密工具', meta: '2 × 4｜手鑽與鑽頭', author: 'ADYDesigns', category: 'precision',
      description: '手鑽、夾頭與不同尺寸鑽頭集中排列，適合模型與小型精密加工。', image: 'media/makerworld-special-hand-drill.jpg',
      url: 'https://makerworld.com/en/models/2888200-bambu-hand-drill-kit-gridfinity-holder'
    },
    {
      title: 'ESD Tweezers Holder', badge: '精密工具', meta: '2 × 4｜防靜電鑷子', author: 'ADYDesigns', category: 'precision',
      description: '直頭與彎頭鑷子分位收納，可依使用習慣選擇張開或套帽放置。', image: 'media/makerworld-special-tweezers.jpg',
      url: 'https://makerworld.com/en/models/2841925-bambu-stainless-steel-tweezers-gridfinity-holder'
    },
    {
      title: 'Multimeter & Probe Holder', badge: '電子設備', meta: '3 × 4｜電表與測試棒', author: 'VeriHeppi', category: 'electronics',
      description: '電表與測試線分區放置，線材不再纏繞，量測時可以整組帶走。', image: 'media/makerworld-special-multimeter.jpg',
      url: 'https://makerworld.com/en/models/1963687-gridfinity-3x4-multimeter-holder'
    },
    {
      title: 'SD / CFast / CFexpress Holder', badge: '電子設備', meta: '1 × 3 × 4U｜記憶卡', author: 'Jordi The Media Guy', category: 'electronics',
      description: '集中收納 SD、CFast 與 CFexpress 卡，適合攝影工作桌與器材箱。', image: 'media/makerworld-special-memory-cards.jpg',
      url: 'https://makerworld.com/en/models/3040197-gridfinity-card-holder-sd-cfast-cf-express'
    },
    {
      title: 'Battery Storage Box', badge: '耗材', meta: '5 × 3｜多規格電池', author: 'thczv', category: 'supplies',
      description: '把 AA、AAA、9V 與鈕扣電池集中在可攜外箱，容量一眼就能確認。', image: 'media/makerworld-special-batteries.jpg',
      url: 'https://makerworld.com/en/models/2106253-battery-storage-box-gridfinity'
    }
  ];
