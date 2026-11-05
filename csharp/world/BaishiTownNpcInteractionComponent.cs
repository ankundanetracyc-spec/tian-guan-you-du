using System.Collections.Generic;

public sealed class BaishiTownNpcInteractionComponent : NpcInteractionActionComponent
{
    public override string ComponentId => "baishi.local_questions";
    public override IEnumerable<NpcInteractionAction> GetActions(MainScene owner, WorldNpcData target) =>
        owner.get_baishi_location_questions(target);
}
