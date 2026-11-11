window.inventory200Sample = {
  "note": "200格材质理想图，五区长宽引用当前正式代码；物品为隔离样例，不是玩家存档。",
  "cell": 36,
  "capacity": 200,
  "rows": [
    {
      "key": "mist",
      "path": "res://resources/entities/items/雾铃花.tres",
      "name": "雾铃花",
      "icon": "res://textures/items/inventory_density_20261029/mat_mist_bell_flower.png",
      "width": 2,
      "height": 4,
      "x": 1,
      "y": 1
    },
    {
      "key": "healing",
      "path": "res://resources/entities/items/alchemy/huichun_1.tres",
      "name": "回春丹·良效",
      "icon": "res://textures/items/inventory_density_20261029/pill_container_empty_porcelain.png",
      "width": 1,
      "height": 2,
      "x": 4,
      "y": 1
    },
    {
      "key": "copper",
      "path": "res://resources/entities/items/铜髓矿.tres",
      "name": "铜髓矿",
      "icon": "res://textures/items/inventory_density_20261029/mat_tong_sui_ore.png",
      "width": 3,
      "height": 3,
      "x": 7,
      "y": 1
    },
    {
      "key": "bitter",
      "path": "res://resources/entities/items/苦苔根.tres",
      "name": "苦苔根",
      "icon": "res://textures/items/inventory_density_20261029/bitter_moss_root_2x4.png",
      "width": 2,
      "height": 4,
      "x": 1,
      "y": 8
    },
    {
      "key": "scale",
      "path": "res://resources/entities/items/赤鳞蜕.tres",
      "name": "赤鳞蜕",
      "icon": "res://textures/items/inventory_density_20261029/mat_red_scale_shed.png",
      "width": 2,
      "height": 3,
      "x": 5,
      "y": 8
    },
    {
      "key": "bone",
      "path": "res://resources/entities/items/兽骨.tres",
      "name": "兽骨",
      "icon": "res://textures/items/inventory_density_20261029/mat_beast_bone.png",
      "width": 2,
      "height": 5,
      "x": 8,
      "y": 8
    }
  ],
  "regions": [
    {
      "id": "chest",
      "name": "胸前",
      "width": 8,
      "height": 5,
      "x": 898,
      "y": 312,
      "items": [
        {
          "key": "mist",
          "x": 0,
          "y": 0
        },
        {
          "key": "healing",
          "x": 2,
          "y": 0
        },
        {
          "key": "copper",
          "x": 3,
          "y": 0
        }
      ]
    },
    {
      "id": "sleeve-left",
      "name": "左袖",
      "width": 4,
      "height": 5,
      "x": 606,
      "y": 536,
      "items": [
        {
          "key": "bitter",
          "x": 0,
          "y": 0
        }
      ]
    },
    {
      "id": "sleeve-right",
      "name": "右袖",
      "width": 4,
      "height": 5,
      "x": 1298,
      "y": 536,
      "items": [
        {
          "key": "scale",
          "x": 0,
          "y": 0
        }
      ]
    },
    {
      "id": "waist",
      "name": "腰间",
      "width": 8,
      "height": 5,
      "x": 898,
      "y": 756,
      "items": [
        {
          "key": "bone",
          "x": 0,
          "y": 0,
          "rotated": true
        }
      ]
    },
    {
      "id": "back",
      "name": "背囊",
      "width": 8,
      "height": 10,
      "x": 1526,
      "y": 432,
      "items": []
    }
  ]
};
