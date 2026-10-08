using Godot;
using Godot.Collections;

public partial class NpcTravelManager : Node
{
    [Signal] public delegate void npc_arrivedEventHandler(string npc_id, string node_id);
    [Signal] public delegate void npc_departedEventHandler(string npc_id, string from_id, string to_id);
    [Signal] public delegate void travelers_changedEventHandler();
    public partial class NpcState : RefCounted
    {
        public string npc_id, npc_name, realm = "", current_node_id, target_node_id = "";
        public float travel_progress, stay_remaining_days, travel_total_days;
        public NpcState(string id, string name, string start, string rlm = "") { npc_id=id; npc_name=name; current_node_id=start; realm=rlm; stay_remaining_days=(float)GD.RandRange(1.0,5.0); }
    }
    private WorldNodeMapData? _map_data;
    private readonly System.Collections.Generic.Dictionary<string, NpcState> _npcs = new();
    public void set_map_data(WorldNodeMapData data) => _map_data = data;
    public void place_npc(string id, string name, string node, string realm = "") { if (_npcs.TryGetValue(id,out var st)){st.current_node_id=node;st.target_node_id="";st.travel_progress=0;st.stay_remaining_days=GD.RandRange(1,5);} else _npcs[id]=new(id,name,node,realm); EmitSignal(SignalName.travelers_changed); }
    public void on_day_tick(float days=1) { if(_map_data is null)return; foreach(var st in _npcs.Values) if(st.target_node_id.Length>0){if(st.travel_total_days>0 && (st.travel_progress+=days/st.travel_total_days)>=1){st.current_node_id=st.target_node_id;st.target_node_id="";st.travel_progress=0;st.stay_remaining_days=GD.RandRange(2,7);EmitSignal(SignalName.npc_arrived,st.npc_id,st.current_node_id);}}else{st.stay_remaining_days-=days;if(st.stay_remaining_days<=0)pick_destination(st);} EmitSignal(SignalName.travelers_changed); }
    private void pick_destination(NpcState st) { var neighbors=_map_data!.get_neighbors(st.current_node_id); if(neighbors.Count==0){st.stay_remaining_days=GD.RandRange(1,3);return;} st.target_node_id=neighbors[(int)(GD.Randi()%(uint)neighbors.Count)];st.travel_progress=0;st.travel_total_days=GD.RandRange(1,4);EmitSignal(SignalName.npc_departed,st.npc_id,st.current_node_id,st.target_node_id); }
    public int get_node_npc_count(string node) => _npcs.Values.Count(s=>s.current_node_id==node&&s.target_node_id.Length==0);
    public Godot.Collections.Array get_all_states(){Godot.Collections.Array a=[];foreach(var s in _npcs.Values)a.Add(s);return a;}
    public Godot.Collections.Array get_traveling_npcs(){Godot.Collections.Array a=[];foreach(var s in _npcs.Values)if(s.target_node_id.Length>0)a.Add(s);return a;}
    public Godot.Collections.Array get_npcs_at_node(string node){Godot.Collections.Array a=[];foreach(var s in _npcs.Values)if(s.current_node_id==node&&s.target_node_id.Length==0)a.Add(s);return a;}
}
