window.ALCHEMY_FIXTURE = {
  "default": {
    "attainment": 36,
    "totalSpirit": 24,
    "otherSpirit": 0,
    "energy": 100
  },
  "materials": [
    {
      "id": "mat_qingmu_core",
      "name": "青木芯",
      "icon": "../../../textures/items/inventory_density_20261029/mat_qingmu_core.png",
      "itemPath": "resources/entities/items/青木芯.tres",
      "craftPath": "resources/material_craft/青木芯材性.tres",
      "element": "木",
      "texture": "致密",
      "polarity": 0,
      "grade": 7,
      "utilities": {
        "通": 2,
        "生": 3
      }
    },
    {
      "id": "mat_mountain_silkworm_thread",
      "name": "山蚕长丝",
      "icon": "../../../textures/items/inventory_density_20261029/mat_mountain_silkworm_thread.png",
      "itemPath": "resources/entities/items/山蚕长丝.tres",
      "craftPath": "resources/material_craft/山蚕长丝材性.tres",
      "element": "木",
      "texture": "纤维",
      "polarity": 0,
      "grade": 3,
      "utilities": {
        "固": 1,
        "藏": 1
      }
    },
    {
      "id": "mat_spirit_spring_dew",
      "name": "灵泉露",
      "icon": "../../../textures/items/inventory_density_20261029/mat_spirit_spring_dew.png",
      "itemPath": "resources/entities/items/灵泉露.tres",
      "craftPath": "resources/material_craft/灵泉露材性.tres",
      "element": "水",
      "texture": "液体",
      "polarity": 0,
      "grade": 7,
      "utilities": {
        "聚": 3,
        "化": 2
      }
    },
    {
      "id": "mat_streampattern_algae_fiber",
      "name": "溪纹藻丝",
      "icon": "../../../textures/items/inventory_density_20261029/mat_streampattern_algae_fiber.png",
      "itemPath": "resources/entities/items/溪纹藻丝.tres",
      "craftPath": "resources/material_craft/溪纹藻丝材性.tres",
      "element": "水",
      "texture": "纤维",
      "polarity": 0,
      "grade": 4,
      "utilities": {
        "通": 2
      }
    },
    {
      "id": "mat_qingmarrow_sprout",
      "name": "青髓芽",
      "icon": "../../../textures/items/inventory_density_20261029/mat_qingmarrow_sprout.png",
      "itemPath": "resources/entities/items/青髓芽.tres",
      "craftPath": "resources/material_craft/青髓芽材性.tres",
      "element": "木",
      "texture": "纤维",
      "polarity": 0,
      "grade": 3,
      "utilities": {
        "生": 1,
        "聚": 1
      }
    },
    {
      "id": "mat_spirit_stone",
      "name": "灵石",
      "icon": "../../../textures/items/inventory_density_20261029/spirit_stone_1x1.png",
      "itemPath": "resources/entities/items/灵石.tres",
      "craftPath": "resources/material_craft/灵石材性.tres",
      "element": "土",
      "texture": "致密",
      "polarity": 0,
      "grade": 3,
      "utilities": {
        "聚": 1,
        "藏": 1,
        "通": 1
      }
    },
    {
      "id": "mat_return_spring_pearl",
      "name": "回泉珠",
      "icon": "../../../textures/items/inventory_density_20261029/mat_return_spring_pearl.png",
      "itemPath": "resources/entities/items/回泉珠.tres",
      "craftPath": "resources/material_craft/回泉珠材性.tres",
      "element": "水",
      "texture": "液体",
      "polarity": 0,
      "grade": 5,
      "utilities": {
        "聚": 2,
        "化": 1
      }
    },
    {
      "id": "mat_dew_pine_resin",
      "name": "凝露松脂",
      "icon": "../../../textures/items/inventory_density_20261029/mat_dew_pine_resin.png",
      "itemPath": "resources/entities/items/凝露松脂.tres",
      "craftPath": "resources/material_craft/凝露松脂材性.tres",
      "element": "木",
      "texture": "黏胶",
      "polarity": 1,
      "grade": 2,
      "utilities": {
        "固": 1
      }
    },
    {
      "id": "mat_qinglan_vine_core",
      "name": "青岚藤心",
      "icon": "../../../textures/items/inventory_density_20261029/mat_qinglan_vine_core.png",
      "itemPath": "resources/entities/items/青岚藤心.tres",
      "craftPath": "resources/material_craft/青岚藤心材性.tres",
      "element": "木",
      "texture": "纤维",
      "polarity": 0,
      "grade": 2,
      "utilities": {
        "生": 1
      }
    },
    {
      "id": "mat_clear_chime_jade_shard",
      "name": "清音玉片",
      "icon": "../../../textures/items/inventory_density_20261029/mat_clear_chime_jade_shard.png",
      "itemPath": "resources/entities/items/清音玉片.tres",
      "craftPath": "resources/material_craft/清音玉片材性.tres",
      "element": "水",
      "texture": "致密",
      "polarity": 0,
      "grade": 3,
      "utilities": {
        "明": 1,
        "通": 1
      }
    },
    {
      "id": "mat_redsun_flower_seed",
      "name": "朱阳花籽",
      "icon": "../../../textures/items/inventory_density_20261029/mat_redsun_flower_seed.png",
      "itemPath": "resources/entities/items/朱阳花籽.tres",
      "craftPath": "resources/material_craft/朱阳花籽材性.tres",
      "element": "火",
      "texture": "纤维",
      "polarity": 1,
      "grade": 2,
      "utilities": {
        "引": 1
      }
    }
  ],
  "recipes": [
    {
      "id": "qingmu",
      "name": "青木清露方",
      "description": "木性君药与水性辅药配伍，佐化转残、使通相合。",
      "slots": {
        "jun": [
          "青木芯",
          2
        ],
        "chen": [
          "山蚕长丝",
          1
        ],
        "zuo1": [
          "灵泉露",
          2
        ],
        "shi1": [
          "溪纹藻丝",
          2
        ]
      }
    },
    {
      "id": "sprout",
      "name": "青芽多味方",
      "description": "青髓芽作君，臣位固形、佐位化残、使位通藏，尝试以分工和材料互补搭配。",
      "slots": {
        "jun": [
          "青髓芽",
          2
        ],
        "chen": [
          "凝露松脂",
          1
        ],
        "zuo1": [
          "回泉珠",
          2
        ],
        "shi1": [
          "灵石",
          2
        ]
      }
    },
    {
      "id": "vine",
      "name": "藤心通路方",
      "description": "青岚藤心作君，溪纹藻丝由使位通路，佐位稳形与识残。",
      "slots": {
        "jun": [
          "青岚藤心",
          3
        ],
        "chen": [
          "朱阳花籽",
          1
        ],
        "zuo1": [
          "清音玉片",
          1
        ],
        "zuo2": [
          "山蚕长丝",
          1
        ],
        "shi1": [
          "溪纹藻丝",
          2
        ]
      }
    }
  ],
  "sources": [
    {
      "path": "resources/entities/items/青木芯.tres",
      "sha256": "30a149d5c20a6a0892ce9fddc014c0bf411930ab0be9289d04fb6136bb8f3cb2"
    },
    {
      "path": "resources/material_craft/青木芯材性.tres",
      "sha256": "cbb3a080116cba1aa6d5063bc0147f455d10e8709de82c3f951f041e968b5020"
    },
    {
      "path": "resources/entities/items/山蚕长丝.tres",
      "sha256": "94fdb9dcd1c351f0a48849064decc0532501a98e0863409ea2164e775f75ad4a"
    },
    {
      "path": "resources/material_craft/山蚕长丝材性.tres",
      "sha256": "a8938457f8dca3fec086b3a5e3330d1fbe348a3c7690ead65b6c4db52ebb4f3a"
    },
    {
      "path": "resources/entities/items/灵泉露.tres",
      "sha256": "485de09b83042b1fe186711028cf3792348283b4bd0a6fccecf22e2aaa7d5be0"
    },
    {
      "path": "resources/material_craft/灵泉露材性.tres",
      "sha256": "4fff496483d7f2fe7946654d79060a82891c09f4edaa0885b6889fa6285cc897"
    },
    {
      "path": "resources/entities/items/溪纹藻丝.tres",
      "sha256": "6429a0217243acd7c7970af8ab0c314fa1426e5fc44eb1d54c001a18a530ff27"
    },
    {
      "path": "resources/material_craft/溪纹藻丝材性.tres",
      "sha256": "838cb6a7fbf0a3c43faa1adaa76674f6f75152ed7b5af6e97dfa6a7e67d79f0e"
    },
    {
      "path": "resources/entities/items/青髓芽.tres",
      "sha256": "3e95100af0096693e2d7d1bc77af2c846e13d8fefcd7717d050bb2dab480c925"
    },
    {
      "path": "resources/material_craft/青髓芽材性.tres",
      "sha256": "d6470bce9bf6ff82e58526128911145a7dc0deb5b70968e02e351a3796cdd582"
    },
    {
      "path": "resources/entities/items/灵石.tres",
      "sha256": "dd24b57401f690305e3b663e778271543b3e2d15cce8efc619bc1a58ed2e3470"
    },
    {
      "path": "resources/material_craft/灵石材性.tres",
      "sha256": "5359abc4a928dd871afedc282fa8b2a371cf440abaef93bc8cf59621bf7bccf5"
    },
    {
      "path": "resources/entities/items/回泉珠.tres",
      "sha256": "72c1e5dd1d1c270f2f0ec4df88252fe7112c268f52b12b25a09246ca27960a5f"
    },
    {
      "path": "resources/material_craft/回泉珠材性.tres",
      "sha256": "c6acad771c1a254ac60679e76e66b418f299db41f4dba05f7874b8a626d1ed23"
    },
    {
      "path": "resources/entities/items/凝露松脂.tres",
      "sha256": "9e1267ee171ae9c00d74bc533dd2ebabe86ef82adf19e70dda6f6108b76542ee"
    },
    {
      "path": "resources/material_craft/凝露松脂材性.tres",
      "sha256": "2afd900a4ed9fa4d25f4aff2de7f1c9e0100f8f6cf18cda89d2534dafc92a2dc"
    },
    {
      "path": "resources/entities/items/青岚藤心.tres",
      "sha256": "373c0a37c9698956793bf68587f8dfb923a0c88107567d87c04972250b172bf5"
    },
    {
      "path": "resources/material_craft/青岚藤心材性.tres",
      "sha256": "5f0b2b97cccb4a4142dabe28687c6cd0ca152b28db65a46cdc06ec138c10a79d"
    },
    {
      "path": "resources/entities/items/清音玉片.tres",
      "sha256": "a510467ac17d6405ae8b8fdedc7face830f84cda02941badec965365dac2d60f"
    },
    {
      "path": "resources/material_craft/清音玉片材性.tres",
      "sha256": "765c826c65d8bc3649ebf2a840c707d2763e1c12f0aa2b8f2f2b5026ca0c0d71"
    },
    {
      "path": "resources/entities/items/朱阳花籽.tres",
      "sha256": "e151e1285167d7dc59056ab1b63708023117b4d001fba29fd599417a52778172"
    },
    {
      "path": "resources/material_craft/朱阳花籽材性.tres",
      "sha256": "ef973f4d768cd07fcc0db9eae090ba1fd19be372f84ec09add410bb2728fbb68"
    },
    {
      "path": "csharp/scene/AlchemyRecipeCatalog.cs",
      "sha256": "36ce23029e195a2ddd20d5f5f902d9109edc6e2af20e06da2790663636e2f204"
    },
    {
      "path": "csharp/scene/AlchemySlotSimulation.cs",
      "sha256": "398f0b63a1d48ab719ce0f2934c463d1ee7b798a0951669801419122addf6136"
    },
    {
      "path": "csharp/scene/AlchemyProcessConfig.cs",
      "sha256": "c2d2c2b80322f206d6f2619af1115240b721c57244a56897a1ea328d3d6e2cb3"
    },
    {
      "path": "csharp/resources/MaterialCraftData.cs",
      "sha256": "1717e58e604b4cec031be65c2326b6644506692ac509a33351a4b8cbc80271ea"
    },
    {
      "path": "csharp/scene/AlchemySlotBrewPreviewScene.cs",
      "sha256": "c9154eabefb2546fb4f743f91cceff00d49661cb15aa6ecdf73a5e677ef600ab"
    },
    {
      "path": "csharp/scene/AlchemyFurnaceView.cs",
      "sha256": "ec700b75602dee5fc2b774058bd9e1ae6daa4e143ce4c3f80adf93c3bf3ba3fa"
    }
  ],
  "scope": "独立UI夹具；材料与三份样方来自现行资源，默认数值来自独立试演常量，不代表玩家库存或已收集状态。未知库存不显示；结果不计算、不发放。"
};
