"use strict";
window.PLANTING_SCENE_FIXTURE={
  note:"仅用于界面审阅。所有田地共享同批基础条件；一号田以独立的改造后情境展示月露苔蕴养。环境、阶段和长势不是正式物种参数或材料效果，不与加入材料自动换算，不写真实角色及存档。",
  fields:[
    {id:"field-1",name:"一号田",target:"moon",crop:{id:"moon",stage:"蕴养"},substrates:[],visual:"spirit-moss",environmentDemo:{temperature:"凉润",lingqi:"偏低",moisture:"湿润",light:"遮阴",ground:"阴湿岩面"},sceneDescription:"苔叶渐展，露意凝于叶尖。",phaseSteps:["定植","生根","蕴养","可采"],phaseIndex:2,injuryDemo:"未见明显损伤",environmentSource:"改造后培育情境示意，非初始地块优势或材料结算"},
    {id:"field-2",name:"二号田",target:"bell",crop:{id:"bell",stage:"幼苗"},substrates:[],visual:"seedling",environmentDemo:null,sceneDescription:"幼苗初立，尚待细察其性。",phaseSteps:["幼苗"],phaseIndex:0,injuryDemo:null,environmentSource:"共同基础条件；雾铃花完整需求仍未配置"},
    {id:"field-3",name:"三号田",target:"moon",crop:null,substrates:[],visual:"fallow",environmentDemo:null,sceneDescription:"原土未改，静待栽植。",phaseSteps:[],phaseIndex:-1,injuryDemo:null,environmentSource:"共同基础条件"}
  ]
};
