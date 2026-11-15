"use strict";
window.PLANTING_CATALOG = {
  "utilities": [
    {
      "id": "固",
      "meaning": "稳定、承载",
      "plantingIntent": "维持田层或附着载体结构",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "聚",
      "meaning": "汇集、蓄积",
      "plantingIntent": "汇集地块所需的环境灵力",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "通",
      "meaning": "连通、导流",
      "plantingIntent": "连通灵路或疏导水路",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "化",
      "meaning": "转换、炼化",
      "plantingIntent": "处理、转化已投入的培育材料",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "引",
      "meaning": "牵引、导向",
      "plantingIntent": "把已有供给导向目标位置",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "破",
      "meaning": "穿透、瓦解",
      "plantingIntent": "处理已明确定义的结壳或阻塞",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "护",
      "meaning": "保护、隔绝",
      "plantingIntent": "隔绝已定义的外界扰动",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "镇",
      "meaning": "压制、固定",
      "plantingIntent": "抑制已定义的躁动或不稳定因素",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "生",
      "meaning": "生长、修复",
      "plantingIntent": "适生条件下的养根与修复",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "明",
      "meaning": "显化、辨识",
      "plantingIntent": "观测和辨识灵草状态",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "藏",
      "meaning": "封存、收敛",
      "plantingIntent": "封存或缓释已有供给",
      "intentStatus": "候选培育用途，不是已生效规则"
    },
    {
      "id": "行",
      "meaning": "运行、位移",
      "plantingIntent": "维持已定义的循环和输送",
      "intentStatus": "候选培育用途，不是已生效规则"
    }
  ],
  "materials": [
    {
      "id": "mat_zisha_spirit_soil",
      "name": "紫砂灵土",
      "image": "textures/items/inventory_density_20261029/mat_zisha_spirit_soil.png",
      "kind": "矿材",
      "facts": "土 · 颗粒 · 阴",
      "stock": 12,
      "source": "resources/entities/items/紫砂灵土.tres",
      "craftSource": "resources/material_craft/紫砂灵土材性.tres",
      "utilities": [
        {
          "id": "藏",
          "grade": 1
        }
      ]
    },
    {
      "id": "mat_clay_vein_lump",
      "name": "泥脉石团",
      "image": "textures/items/inventory_density_20261029/mat_clay_vein_lump.png",
      "kind": "矿材",
      "facts": "土 · 致密 · 阴",
      "stock": 8,
      "source": "resources/entities/items/泥脉石团.tres",
      "craftSource": "resources/material_craft/泥脉石团材性.tres",
      "utilities": [
        {
          "id": "镇",
          "grade": 1
        }
      ]
    },
    {
      "id": "mat_beast_bone",
      "name": "兽骨",
      "image": "textures/items/inventory_density_20261029/mat_beast_bone.png",
      "kind": "兽材",
      "facts": "土 · 致密 · 阳",
      "stock": 6,
      "source": "resources/entities/items/兽骨.tres",
      "craftSource": "resources/material_craft/兽骨材性.tres",
      "utilities": [
        {
          "id": "固",
          "grade": 1
        },
        {
          "id": "生",
          "grade": 1
        }
      ]
    },
    {
      "id": "mat_geovein_flame_coal",
      "name": "地脉焰炭",
      "image": "textures/items/inventory_density_20261029/mat_geovein_flame_coal.png",
      "kind": "炼器燃料",
      "facts": "火 · 致密 · 阳",
      "stock": 5,
      "source": "resources/entities/items/地脉焰炭.tres",
      "craftSource": "resources/material_craft/地脉焰炭材性.tres",
      "utilities": [
        {
          "id": "聚",
          "grade": 2
        },
        {
          "id": "通",
          "grade": 1
        }
      ]
    }
  ],
  "plants": [
    {
      "id": "moon",
      "name": "月露苔",
      "image": "textures/items/inventory_density_20261029/mat_moon_dew_moss.png",
      "habitat": "阴湿岩面",
      "needs": {
        "temperature": "凉润",
        "lingqi": "充足",
        "moisture": "湿润",
        "light": "遮阴",
        "ground": "阴湿岩面"
      }
    },
    {
      "id": "bell",
      "name": "雾铃花",
      "image": "textures/items/inventory_density_20261029/mat_mist_bell_flower.png",
      "habitat": "瑶圃峰已有培育物种",
      "needs": null
    }
  ],
  "dimensions": [
    {
      "key": "temperature",
      "label": "温度",
      "baseline": "偏暖",
      "issue": "温度偏暖"
    },
    {
      "key": "lingqi",
      "label": "灵力浓度",
      "baseline": "偏低",
      "issue": "环境灵力不足"
    },
    {
      "key": "moisture",
      "label": "水分",
      "baseline": "湿润",
      "issue": "水分不合"
    },
    {
      "key": "light",
      "label": "光照",
      "baseline": "直射",
      "issue": "光照过强"
    },
    {
      "key": "ground",
      "label": "附着条件",
      "baseline": "普通土层",
      "issue": "缺少所需岩面"
    }
  ],
  "scope": "独立UI演示：行囊可用基质列表，拖拽或选择数量直接加入当前田地，不再另存方案；演示可用量随加入记录减少。物品资料与功用品级真实，资格和库存为夹具；不读写真实行囊、角色或存档。具体培育效果未定义，阵法只留独立入口。",
  "demoSubstrateEligibility": {
    "mat_zisha_spirit_soil": true,
    "mat_clay_vein_lump": true,
    "mat_beast_bone": true,
    "mat_geovein_flame_coal": false
  },
  "eligibilityNote": "仅为验证“行囊持有且允许作基质”列表的演示夹具，不是正式可入田白名单。正式资格和来源由物品/种植规则提供，未定义不放行。"
};

