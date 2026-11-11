"""Reproduce a documented candidate from a native forging export; never opens saves."""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
import math
from collections import defaultdict
from decimal import Decimal, ROUND_DOWN, ROUND_FLOOR, ROUND_HALF_UP
from pathlib import Path
import numpy as np
from scipy.optimize import minimize


def solve(requests, budgets):
    active = sorted([q for q in requests if all(budgets.get(k, 0) / v >= q['minimum'] for k, v in q['costs'].items())], key=lambda q: q['id'])
    removed = []
    while active:
        keys = sorted({k for q in active for k in q['costs']})
        matrix = np.array([[q['costs'].get(k, 0) for q in active] for k in keys], dtype=float)
        limits = np.array([budgets.get(k, 0) for k in keys], dtype=float)
        weights = np.array([q['weight'] for q in active], dtype=float)
        answer = minimize(lambda x: -np.dot(weights, np.log1p(x)), np.zeros(len(active)), jac=lambda x: -weights/(1+x), bounds=[(0, None)]*len(active), constraints=[{'type':'ineq', 'fun':lambda x: limits-matrix@x, 'jac':lambda x: -matrix}], method='SLSQP', options={'ftol':1e-12, 'maxiter':2000})
        if not answer.success or np.min(limits-matrix@answer.x) < -1e-7:
            raise RuntimeError(f'Allocation failed: {answer.message}')
        values = np.maximum(answer.x, 0)
        too_small = [(values[i]/max(q['minimum'],1e-12), q['id'], i) for i,q in enumerate(active) if values[i]+1e-8 < q['minimum']]
        if not too_small:
            return {q['id']:float(values[i]) for i,q in enumerate(active)}, removed
        _, key, index = min(too_small)
        removed.append(key)
        active.pop(index)
    return {}, removed


def ordinary_requests(native, process, parameters, revised=False, star_count=2):
    if revised and 'basic_stat_generation' in parameters:
        return shape_pool_requests(native, process, parameters)
    config = json.loads(native['ParameterSnapshot'])
    atoms = copy.deepcopy(config['Atoms'])
    carriers = copy.deepcopy(config['Carriers'])
    budgets = process['resources_after_bonds_before_effects']
    family = None
    if revised:
        changes = parameters['normal_effect_changes']
        flat = changes['keyword_damage_atom']
        scope = max(flat['scope_cost_floor'], star_count*flat['scope_cost_per_star_cell'])
        flat_atom = {'Id':flat['id'], 'Kind':'integer', 'Coefficient':flat['coefficient']/scope, 'Maximum':0, 'Sources':{'破':1}}
        atoms.append(flat_atom)
        for carrier in carriers:
            if carrier['Id']=='base': carrier['Atoms']=[k for k in carrier['Atoms'] if k!='physical_damage_percent']
            if carrier['Id']=='keyword': carrier['Atoms']=[flat['id']]
        candidates=[]
        for f in changes['binding_families']:
            minimum=max(config['Scalars']['MinimumEffect'],1/f['coefficient'])
            if all(budgets.get(k,0)/v>=minimum for k,v in {'source/'+f['source']:1,'root':1,'structure':1,'通':0.35}.items()):
                candidates.append((budgets['source/'+f['source']]/minimum,f))
        if candidates:
            family=sorted(candidates,key=lambda pair:(-pair[0],pair[1]['id']))[0][1]
        for carrier in carriers:
            if carrier['Id']=='binding':
                carrier['Weight']=changes['binding_weight']
                carrier['Atoms']=[family['atom']] if family else []
    choices=[]
    for carrier in sorted(carriers,key=lambda q:q['Id']):
        if carrier['Special'] and native['Tier']=='凡器': continue
        if carrier['Id']=='summon' and native['Volume']<config['Scalars']['MinimumSummonVolume']: continue
        for atom in sorted(atoms,key=lambda q:q['Id']):
            if atom['Id'] not in carrier['Atoms']: continue
            for source,source_weight in sorted(atom['Sources'].items()):
                if revised and carrier['Id']=='binding' and source!=family['source']: continue
                if process['vectors']['E'].get(source,0)<=0: continue
                costs={'source/'+source:1,'root':1,'structure':1,**carrier['Costs']}
                resource=carrier.get('Resource','')
                resource_weight=1 if not resource else budgets.get(resource,0)/(budgets.get(resource,0)+config['Scalars']['WeightScale'])
                coefficient=family['coefficient'] if revised and carrier['Id']=='binding' else atom['Coefficient']
                minimum=max(config['Scalars']['MinimumEffect'],1/coefficient if atom['Kind']=='integer' else 0)
                choices.append({'id':carrier['Id']+'/'+atom['Id']+'/'+source, 'source':source, 'carrier':carrier['Id'], 'atom':atom['Id'], 'coefficient':coefficient, 'kind':atom['Kind'], 'maximum':atom.get('Maximum',0), 'minimum':minimum, 'raw_weight':carrier['Weight']*source_weight*resource_weight, 'costs':costs})
    totals=defaultdict(float)
    for q in choices: totals[q['source']]+=q['raw_weight']
    for q in choices: q['weight']=q['raw_weight']/totals[q['source']]
    return choices,family


def shape_profile(native, parameters):
    """Select a hard pool from physical ratios, never from a display name."""
    rules = parameters['basic_stat_generation']
    width = float(native['Width'])
    length = float(native['Length'])
    thickness = float(native['Thickness'])
    if not all(math.isfinite(v) and v > 0 for v in (width, length, thickness)):
        raise ValueError('Shape pools require valid actual sword dimensions.')
    lam, tau = length / width, thickness / width
    thresholds = rules['shape_thresholds']
    kind = ('slender' if lam >= thresholds['slender_lambda_min'] else 'broad')
    kind += '_thick' if tau >= thresholds['thick_tau_min'] else '_thin'
    profile_id = f'sword.{kind}.v1'
    pools = rules['shape_pools']
    if profile_id not in pools:
        raise ValueError(f'Missing hard shape pool: {profile_id}; no all-stat fallback.')
    allowed = list(pools[profile_id])
    whitelist = {'max_health', 'physical_defense', 'precision', 'dodge', 'critical', 'crit_resist'}
    if set(rules['allowed_stat_ids']) != whitelist or set(rules['stats']) != whitelist:
        raise ValueError('The candidate only defines the six confirmed int-flat stats.')
    if len(allowed) != len(set(allowed)) or not set(allowed) <= whitelist:
        raise ValueError(f'Invalid hard shape pool: {profile_id}.')
    return {'profile_id': profile_id, 'kind': kind, 'lambda': lam, 'tau': tau,
            'allowed_stat_ids': allowed, 'selection_source': 'actual_dimensions'}


def rotate_cell(cell, quarter_turns):
    if len(cell) != 2 or any(type(value) is not int for value in cell):
        raise ValueError('Layout coordinates must contain two explicit integer values.')
    x, y = cell
    for _ in range(quarter_turns % 4):
        x, y = -y, x
    return [x, y]


def held_sharpness_geometry(parameters, quarter_turns=0):
    """Read an explicit tip anchor and ray; never derive coverage from either footprint."""
    definition = parameters['held_sharpness_range']
    if definition['origin'] != 'blade_tip' or definition['occupies_board_cells'] or definition['changes_attack_range']:
        raise ValueError('The confirmed sword-tip coverage cannot become occupancy or attack reach.')
    turns = (definition['canonical_facing_quarter_turns'] + quarter_turns) % 4
    anchor = rotate_cell(definition['source_anchor'], turns)
    offsets = [rotate_cell(cell, turns) for cell in definition['offsets']]
    if len({tuple(cell) for cell in offsets}) != len(offsets):
        raise ValueError('Coverage offsets cannot repeat a cell.')
    return {
        'definition_version': definition['definition_version'],
        'definition_key': definition['definition_key'], 'status': definition['status'],
        'origin': 'blade_tip', 'facing_quarter_turns': turns,
        'source_anchor': anchor, 'offsets': offsets,
        'range_cells': [[anchor[0] + x, anchor[1] + y] for x, y in offsets],
        'cells': len(offsets), 'rotation': definition['rotation'],
        'target': definition['target'], 'occupies_board_cells': False,
        'changes_attack_range': False, 'derived_from_battle_or_inventory_footprint': False,
    }


def preview_skill_attempt(skill, available_energy, available_mana, conditions_valid=True):
    """One skill's eligibility only; never select a substitute or mutate battle resources."""
    can_execute = (conditions_valid and available_energy >= skill['energy_cost']
                   and available_mana >= skill['mana_cost'])
    return {'skill_id': skill['skill_id'], 'can_execute': can_execute,
            'decision': 'execute_this_skill' if can_execute else 'skip_this_skill',
            'substituted_skill_id': None, 'automatic_extra_attack': False}


def shape_pool_requests(native, process, parameters):
    """Pool filtering precedes per-source preference normalization."""
    profile = shape_profile(native, parameters)
    rules = parameters['basic_stat_generation']
    config = json.loads(native['ParameterSnapshot'])
    budgets = process['resources_after_bonds_before_effects']
    choices = []
    unit_cost = rules['source_root_structure_unit_cost']
    if unit_cost != 1 or rules['extra_process_costs']:
        raise ValueError('V2 base stats require source/root/structure 1 and no extra process costs.')
    for stat_id in sorted(profile['allowed_stat_ids']):
        definition = rules['stats'][stat_id]
        coefficient = definition['coefficient']
        if not math.isfinite(coefficient) or coefficient <= 0:
            raise ValueError(f'Invalid conversion coefficient for {stat_id}.')
        for source, semantic_weight in sorted(definition['sources'].items()):
            if not math.isfinite(semantic_weight) or semantic_weight <= 0:
                raise ValueError(f'Invalid semantic weight for {stat_id}/{source}.')
            if process['vectors']['E'].get(source, 0) <= 0:
                continue
            choices.append({
                'id': f'base/{stat_id}/{source}', 'source': source, 'carrier': 'base',
                'atom': stat_id, 'stat_id': stat_id, 'value_kind': 'int_flat',
                'profile_id': profile['profile_id'], 'coefficient': coefficient,
                'kind': 'integer', 'maximum': 0,
                'minimum': max(config['Scalars']['MinimumEffect'], 1 / coefficient),
                'semantic_weight': semantic_weight,
                'raw_weight': rules['base_weight'] * semantic_weight,
                'costs': {'source/' + source: 1, 'root': 1, 'structure': 1},
            })
    # Only this registered package is open in v2; raw legacy carriers never enter.
    changes = parameters['normal_effect_changes']
    families = changes['binding_families']
    if len(families) != 1 or families[0]['template_id'] != 'artifact.gejian.defuse_flat.v1':
        raise ValueError('V2 only registers the gejian response-defuse package.')
    family = None
    definition = families[0]
    if native['Tier'] != '凡器':
        source = definition['source']
        coefficient = definition['coefficient']
        minimum = max(config['Scalars']['MinimumEffect'], 1 / coefficient)
        costs = {'source/' + source: 1, 'root': 1, 'structure': 1, **definition['costs']}
        if process['vectors']['E'].get(source, 0) > 0 and all(
                budgets.get(key, 0) / cost >= minimum for key, cost in costs.items()):
            resource = definition['preference_resource']
            quantity = budgets.get(resource, 0)
            resource_weight = quantity / (quantity + config['Scalars']['WeightScale'])
            choices.append({
                'id': f"binding/{definition['atom']}/{source}", 'source': source,
                'carrier': 'binding', 'atom': definition['atom'], 'coefficient': coefficient,
                'kind': 'integer', 'maximum': 0, 'minimum': minimum,
                'raw_weight': changes['binding_weight'] * resource_weight, 'costs': costs,
                'template_id': definition['template_id'],
                'template_version': definition['template_version'],
                'operation_id': definition['operation_id'],
            })
            family = copy.deepcopy(definition)
    totals = defaultdict(float)
    for choice in choices:
        totals[choice['source']] += choice['raw_weight']
    for choice in choices:
        choice['weight'] = choice['raw_weight'] / totals[choice['source']]
    return choices, family


def shape_pool_outputs(requests, allocated, parameters):
    """Merge a stat's paid source branches before its one integer truncation."""
    grouped = defaultdict(list)
    for request in requests:
        x = allocated.get(request['id'], 0)
        if request['carrier'] != 'profile' and x > 1e-10:
            grouped[(request['carrier'], request['atom'])].append((request, x))
    outputs = []
    for (carrier, atom), parts in sorted(grouped.items()):
        first = parts[0][0]
        coefficient = first['coefficient']
        assert all(q['coefficient'] == coefficient for q, _ in parts)
        allocations, debits = defaultdict(float), defaultdict(float)
        for request, x in parts:
            allocations[request['source']] += x
            for key, cost in request['costs'].items():
                debits[key] += x * cost
        total = sum(allocations.values())
        raw = total * coefficient
        value = math.floor(raw + 1e-9)
        if value <= 0:
            continue
        output = {
            'id': carrier + '/' + atom, 'carrier': carrier, 'atom': atom,
            'source': next(iter(allocations)) if len(allocations) == 1 else 'multiple_sources',
            'source_allocations': dict(allocations),
            'source_branches': [{'request_id': q['id'], 'source': q['source'],
                                 'semantic_weight': q.get('semantic_weight', 1),
                                 'normalized_weight': q['weight'], 'allocation': x}
                                for q, x in parts],
            'allocation': total, 'coefficient': coefficient, 'value': value,
            'value_kind': 'int_flat', 'unquantized': raw,
            'quantization_loss_allocation': max(0, total - value / coefficient),
            'costs': {key: debit / total for key, debit in debits.items()},
            'budget_debits': dict(debits),
        }
        if carrier == 'base':
            output.update({
                'stat_id': first['stat_id'], 'profile_id': first['profile_id'],
                'activation': 'held_or_controlled_enabled', 'activation_modes': ['held', 'controlled'],
                'requires_enabled': True, 'scope': 'owner_character_global_not_star_area',
                'source_deduplication': 'source_instance_id+stat_id',
                'controlled_action_read': 'owner_final_stat_once_no_duplicate_instance_bonus',
                'godot_runtime_applier_implemented': False,
            })
        else:
            output.update({key: first[key] for key in ('template_id', 'template_version', 'operation_id')})
        outputs.append(output)
    return outputs


def transformation_bundle(native, outputs, family, parameters):
    if family is None:
        return None
    effects = [e for e in outputs if e['carrier'] == 'binding' and e['value'] > 0]
    if not effects:
        return None
    assert len(effects) == 1 and effects[0]['operation_id'] == 'add_response_defuse_flat'
    effect = effects[0]
    source_id = native.get('InstanceId') or None
    return {
        'source_instance_id': source_id,
        'bundle_id': (source_id or 'unbound_trial_source') + '/' + family['template_id'],
        'template_id': family['template_id'], 'template_version': family['template_version'],
        'source_policy': family['source_policy'], 'target_policy': family['target_policy'],
        'matcher': copy.deepcopy(family['matcher']),
        'operations': [{'id': family['operation_id'], 'parameters': {'amount': effect['value']}}],
        'generation': {
            'allocation_X': effect['allocation'], 'source_dimension': family['source'],
            'source_allocations': effect['source_allocations'], 'coefficient': effect['coefficient'],
            'budget_debits': effect['budget_debits'], 'rules_version': parameters['version'],
        },
        'runtime_status': 'independent_candidate_not_wired_to_godot',
        'source_resolution_status': 'recorded_trial_instance_not_player_save' if source_id else 'real_source_required_before_binding',
    }


def quantize(value, places):
    return float(Decimal(str(value)).quantize(Decimal(1).scaleb(-places),rounding=ROUND_DOWN))


def evaluate(stability, attack, soul, sword, occupation):
    d=lambda v: Decimal(str(v))
    stable=d(stability['base'])+d(stability['soul_coefficient'])*d(soul)+d(stability['sword_proficiency_coefficient'])*d(sword)+d(stability['this_item_spirit_occupation_coefficient'])*d(occupation)
    damage=d(attack['base'])+d(attack['soul_coefficient'])*d(soul)+d(attack['sword_proficiency_coefficient'])*d(sword)
    return {'soul':soul,'sword_proficiency':sword,'occupation':occupation,'stability_cap':int(stable.to_integral_value(rounding=ROUND_FLOOR)),'raw_physical_damage':int(damage.to_integral_value(rounding=ROUND_HALF_UP))}


def automatic_name(native, outputs, config):
    order={name:i for i,name in enumerate(config['category_order'])}
    candidates=[]
    for index,entry in enumerate(config['prefixes']):
        matched=[effect for effect in outputs if effect['carrier']==entry['carrier'] and effect['atom']==entry['atom'] and effect['value']>0]
        assert len(entry['label'])==2, 'Name prefixes must contain two Chinese characters.'
        if matched: candidates.append((order[entry['carrier']],index,min(e['id'] for e in matched),entry['label'],entry['carrier']))
    labels=[]
    for _,_,_,label,_ in sorted(candidates):
        if label not in labels: labels.append(label)
        if len(labels)==config['preferred_prefix_groups']: break
    if len(labels)>=config['preferred_prefix_groups'] and len(labels)<config['max_prefix_groups']:
        for _,_,_,label,carrier in sorted(candidates):
            if carrier in config['extra_group_carriers'] and label not in labels:
                labels.append(label)
                break
    # Actual native shape thresholds; no material-name or item-name inference.
    suffix=('厚脊' if native['Thickness']/native['Width']>=0.16 else '薄脊')+'剑'
    return config['separator'].join(labels+[suffix]),labels


def calculate(bundle, p):
    native=bundle['native_export']['snapshot']
    assert native['Category']=='剑' and native['Volume']>0, 'This candidate only accepts formed swords.'
    process=bundle['process_evidence']
    B,E,J=[process['vectors'][k] for k in ('B','E','J')]
    metrics=process['metrics']
    v2 = 'basic_stat_generation' in p
    selected_profile = shape_profile(native, p) if v2 else None
    # Validate the export, including encoded utility grades and each real source.
    sums={name:defaultdict(float) for name in ('B','E','J')}
    for s in process['sources']:
        q=(1-math.exp(-s['quantity']/3))/(1-math.exp(-1/3))
        strongest=max((1.6**(g-1) for g in s['facts']['Utilities'].values()),default=0)
        assert abs(q-s['Q'])<1e-10
        assert abs(8*q*strongest*s['eta']-s['P'])<1e-8
        assert abs(sum(sum(s[name].values()) for name in sums)+s['bridge_spent']+s['loss']-s['P'])<1e-8
        for name in sums:
            for key,value in s[name].items(): sums[name][key]+=value
    for name,values in sums.items():
        for key,value in process['vectors'][name].items(): assert abs(values[key]-value)<1e-8
    for key,value in native['Calculation']['CharacterPotential'].items(): assert abs(E[key]-value)<1e-8
    assert abs(native['RootBudget']-metrics['R'])<1e-8
    original_requests,_=ordinary_requests(native,process,p)
    old_alloc,_=solve(original_requests,process['resources_after_bonds_before_effects'])
    expected={effect['CarrierId']+'/'+effect['AtomId']+'/'+source:value for effect in native['Effects'] for source,value in effect['SourceAllocations'].items()}
    assert set(old_alloc)==set(expected)
    native_error=max(abs(old_alloc[key]-value) for key,value in expected.items())
    assert native_error<2e-5,native_error
    splits={key:[value*ratio for ratio in p['body_split'].get(key,p['body_split']['*'])] for key,value in B.items()}
    for key,parts in splits.items(): assert abs(sum(parts)-B[key])<1e-10
    core,bs,ba=[sum(row[i] for row in splits.values()) for i in range(3)]
    gl=max(1,math.ceil(metrics['length']/p['battle_shape']['length_per_cell']))
    gw=max(1,math.ceil(metrics['width']/p['battle_shape']['width_per_cell']))
    requests,family=ordinary_requests(native,process,p,True,gl*gw)
    budgets=dict(process['resources_after_bonds_before_effects'])
    budgets.update({'body/stability':bs,'body/attack':ba})
    for key,profile in p['profile_demands'].items():
        requests.append({'id':'profile/'+key,'carrier':'profile','atom':key,'minimum':profile['minimum'],'weight':profile['group_priority'],'costs':profile['costs']})
    allocated,removed=solve(requests,budgets)
    used=defaultdict(float)
    outputs=[]
    for q in requests:
        x=allocated.get(q['id'],0)
        for key,cost in q['costs'].items(): used[key]+=x*cost
        if q['carrier']=='profile' or x<=1e-10: continue
        raw=x*q['coefficient']
        capped=min(raw,q['maximum']) if q['maximum'] else raw
        value=math.floor(capped+1e-9) if q['kind']=='integer' else capped
        outputs.append({'id':q['id'],'carrier':q['carrier'],'atom':q['atom'],'source':q['source'],'allocation':x,'coefficient':q['coefficient'],'value':value,'unquantized':raw,'quantization_loss_allocation':max(0,x-value/q['coefficient']),'costs':q['costs']})
        if q['carrier']=='keyword' and q['atom']==p['normal_effect_changes']['keyword_damage_atom']['id']:
            outputs[-1]['eligibility']={key:value for key,value in p['normal_effect_changes']['keyword_damage_atom'].items() if key in ('required_terms','action_owner','target_shape','damage_type','segment_scope','active_mode','requires_star_coverage','affects_controlled_weapon_actions')}
    if v2:
        outputs = shape_pool_outputs(requests, allocated, p)
        base_outputs = [effect for effect in outputs if effect['carrier'] == 'base']
        assert all(effect['stat_id'] in selected_profile['allowed_stat_ids'] for effect in base_outputs)
        assert all(effect['value_kind'] == 'int_flat' and effect['profile_id'] == selected_profile['profile_id'] for effect in base_outputs)
        assert all(effect['carrier'] in ('base', 'binding') for effect in outputs)
        family = family if any(effect['carrier'] == 'binding' for effect in outputs) else None
        for key, definition in p['profile_demands'].items():
            assert next(q for q in requests if q['id'] == 'profile/' + key)['weight'] == definition['group_priority']
    for key,value in used.items(): assert value<=budgets[key]+1e-7,(key,value,budgets[key])
    xs,xa=allocated.get('profile/stability',0),allocated.get('profile/attack',0)
    k,sigma,f=metrics['connection_ratio'],metrics['S'],metrics['finish']
    sv,av=p['stability_value'],p['attack_value']
    Vs=max(sv['minimum'],(sv['base']+sv['sqrt_scale']*math.sqrt(xs))*k*(sv['stability_base_factor']+sv['stability_factor']*sigma))
    Va=max(av['minimum'],(av['base']+av['sqrt_scale']*math.sqrt(xa))*f*k*(av['stability_base_factor']+av['stability_factor']*sigma))
    bt,jt=sum(B.values()),sum(J.values())
    ratio=lambda v,key,total: v.get(key,0)/total if total else 0
    solid,protect,breaking=[ratio(B,key,bt) for key in ('固','护','破')]
    trans,storage=[ratio(J,key,jt) for key in ('通','藏')]
    edge=metrics['edge_material']*f*metrics['shape_coefficient']
    response=edge/(1+edge)
    features={'body_solid_ratio':solid,'body_protect_ratio':protect,'body_break_ratio':breaking,'process_transmission_ratio':trans,'process_storage_ratio':storage,'process_transmission_ratio_times_connection':trans*k,'edge_response':response}
    def bias(row):
        return row.get('constant',0)+sum(value*features[key] for key,value in row.items() if key!='constant')
    sw=[bias(p['stability_bias'][key]) for key in ('base','soul','sword','occupation')]
    aw=[bias(p['attack_bias'][key]) for key in ('base','soul','sword')]
    sp,ap=[[v/sum(values) for v in values] for values in (sw,aw)]
    ref=p['reference_prices']
    sr=[Vs*sp[0],Vs*sp[1]/ref['soul'],Vs*sp[2]/ref['sword_proficiency'],Vs*sp[3]/ref['occupation']]
    ar=[Va*ap[0],Va*ap[1]/ref['soul'],Va*ap[2]/ref['sword_proficiency']]
    sk=['base','soul_coefficient','sword_proficiency_coefficient','this_item_spirit_occupation_coefficient']
    ak=['base','soul_coefficient','sword_proficiency_coefficient']
    stable={key:quantize(value,p['coefficient_decimal_places']) for key,value in zip(sk,sr)}
    attack={key:quantize(value,p['coefficient_decimal_places']) for key,value in zip(ak,ar)}
    attack['damage_type']='physical'
    assert abs(sr[0]+ref['soul']*sr[1]+ref['sword_proficiency']*sr[2]+ref['occupation']*sr[3]-Vs)<1e-10
    assert abs(ar[0]+ref['soul']*ar[1]+ref['sword_proficiency']*ar[2]-Va)<1e-10
    stable_quantized_value=stable['base']+ref['soul']*stable['soul_coefficient']+ref['sword_proficiency']*stable['sword_proficiency_coefficient']+ref['occupation']*stable['this_item_spirit_occupation_coefficient']
    attack_quantized_value=attack['base']+ref['soul']*attack['soul_coefficient']+ref['sword_proficiency']*attack['sword_proficiency_coefficient']
    assert stable_quantized_value<=Vs+1e-9
    assert attack_quantized_value<=Va+1e-9
    # Core physical processing remains distinct from extra profile allocation.
    indirect=0
    for source in process['sources']:
        if source['role'] in ('主体','承接','复合身材'): continue
        for utility,weight in (('破',0.5),('固',0.2)):
            indirect+=source['B'].get(utility,0)*p['body_split'].get(utility,p['body_split']['*'])[0]*weight
    sharp_raw=4*metrics['edge_material']*f*metrics['shape_coefficient']+0.03*indirect
    exclusive=sum(e['allocation'] for e in outputs if e['carrier']=='summon')
    occupation=max(p['spirit']['minimum'],math.ceil(p['spirit']['volume_coefficient']*native['Volume']+p['spirit']['exclusive_control_skill_allocation_coefficient']*exclusive))
    actor_examples=[evaluate(stable,attack,s,w,occupation) for s,w in ((6,6),(10,10),(20,20),(40,40),(80,80),(20,10),(10,20))]
    for e in actor_examples:
        a=evaluate(stable,attack,e['soul']+1,e['sword_proficiency'],occupation)
        b=evaluate(stable,attack,e['soul'],e['sword_proficiency']+1,occupation)
        assert a['stability_cap']>=e['stability_cap'] and b['stability_cap']>=e['stability_cap']
        assert a['raw_physical_damage']>=e['raw_physical_damage'] and b['raw_physical_damage']>=e['raw_physical_damage']
    source_trace=[]
    for source in process['sources']:
        parts={key:sum(value*p['body_split'].get(u,p['body_split']['*'])[i] for u,value in source['B'].items()) for i,key in enumerate(('core','stability','attack'))}
        attributed_s=xs*parts['stability']/bs if bs else 0
        attributed_a=xa*parts['attack']/ba if ba else 0
        e_receipts = {}
        for effect in outputs:
            source_allocations = effect.get('source_allocations', {effect['source']: effect['allocation']})
            receipt = sum(x * source['E'].get(utility, 0) / E[utility]
                          for utility, x in source_allocations.items() if E.get(utility, 0) > 0)
            if receipt > 0:
                e_receipts[effect['id']] = receipt
        bond_receipts={b['bond']:b['actual_debit'].get('source/'+u,0)*amount/E[u] for b in process['bonds'] for u,amount in source['E'].items() if b['actual_debit'].get('source/'+u,0)>0}
        source_trace.append({'role':source['role'],'material':source['material'],'quantity':source['quantity'],'body_pools':parts,'stability_allocation_contribution':attributed_s,'attack_allocation_contribution':attributed_a,'normal_effect_source_contributions':e_receipts,'bond_source_contributions':bond_receipts,'unused_body_stability':parts['stability']-attributed_s,'unused_body_attack':parts['attack']-attributed_a})
    assert abs(sum(s['stability_allocation_contribution'] for s in source_trace)-xs)<1e-9
    assert abs(sum(s['attack_allocation_contribution'] for s in source_trace)-xa)<1e-9
    auto_name,prefixes=automatic_name(native,outputs,p['automatic_naming'])
    custom_name=bundle.get('explicit_custom_name',native['Name'] if bundle['native_export'].get('offer_path') else '')
    origin='explicit_input' if 'explicit_custom_name' in bundle else ('explicit_offer_entity_name' if custom_name else 'automatic')
    naming={'rule_version':p['automatic_naming']['version'],'auto_name':auto_name,'prefix_groups':prefixes,'custom_name':custom_name,'custom_name_origin':origin,'display_name':custom_name or auto_name,'name_does_not_change_stats':True}
    result = {'status':'real_material_input_with_new_candidate_rules_not_live_item','rules_version':p['version'],'native_baseline_reproduction_max_allocation_error':native_error,'input_proficiency':native['Proficiency'],'input_units':sum(s['quantity'] for s in process['sources']),'original_metrics':metrics,'body_split_by_utility':splits,'body_totals':{'core':core,'stability_supply':bs,'attack_supply':ba},'budgets_after_bonds':budgets,'requests':requests,'allocations':allocated,'pruned_candidates':removed,'used_after_bonds':dict(used),'unused_after_bonds':{key:max(0,value-used[key]) for key,value in budgets.items()},'normal_effects':outputs,'binding_family':family,'profile_values':{'Xs':xs,'Xa':xa,'stability_reference_value':Vs,'attack_reference_value':Va,'stability_weights':sp,'attack_weights':ap,'stability_before_quantization':dict(zip(sk,sr)),'attack_before_quantization':dict(zip(ak,ar)),'features':features,'stability_quality_factor':k*(sv['stability_base_factor']+sv['stability_factor']*sigma),'attack_quality_factor':f*k*(av['stability_base_factor']+av['stability_factor']*sigma),'reference_value_quantization_loss':{'stability':Vs-stable_quantized_value,'attack':Va-attack_quantized_value}},'source_trace':source_trace,'naming':naming,'stability_parameters':stable,'default_attack_parameters':attack,'actor_examples':actor_examples,'sharpness':{'core_indirect_support':indirect,'raw':sharp_raw,'value':math.floor(sharp_raw)},'inventory':{'width':bundle['native_export']['offer_inventory_width'],'height':bundle['native_export']['offer_inventory_height'],'rotation_allowed':True},'battle_footprint':{'length':gl,'width':gw,'cells':gl*gw,'rotation_allowed':True},'handheld_stars':{'inward_length':gl,'width':gw,'cells':gl*gw},'spirit_occupation':occupation,'exclusive_control_skill_allocation':exclusive,'default_attack_rules':p['skills'][0] if v2 else p['default_attack'],'bonds':process['bonds'],'crafting_hours':native['CraftingHours'],'crafting_energy':native['CraftingEnergy'],'runtime_modified':False,'player_save_modified':False}
    result['rule_status'] = 'complete_shape_pool_candidate_not_live' if v2 else 'historical_withdrawn_current_rules_invalid'
    if v2:
        result['shape_profile'] = selected_profile
        result.pop('default_attack_rules')
        result['skills'] = copy.deepcopy(p['skills'])
        result['skill_dispatch'] = copy.deepcopy(p['skill_dispatch'])
        result['battle_footprint']['occupied_cell_offsets'] = [[x, y] for y in range(gw) for x in range(gl)]
        result['handheld_stars'] = held_sharpness_geometry(p)
        result['base_stat_rules'] = p['basic_stat_generation']
        result['transformation_bundle'] = transformation_bundle(native, outputs, family, p)
        result['generation_validation'] = {
            'hard_pool_exclusion': True, 'legacy_raw_carriers_excluded': True,
            'profile_priority_not_renormalized': True, 'source_and_common_budgets_conserved': True,
            'native_old_model_reproduced': True, 'godot_runtime_modified': False,
        }
    return result


def numerical_core_sha256(result):
    keys = ('body_split_by_utility', 'body_totals', 'budgets_after_bonds', 'requests',
            'allocations', 'pruned_candidates', 'used_after_bonds', 'unused_after_bonds',
            'normal_effects', 'binding_family', 'profile_values', 'source_trace', 'naming',
            'stability_parameters', 'default_attack_parameters', 'actor_examples', 'sharpness',
            'spirit_occupation', 'exclusive_control_skill_allocation', 'bonds')
    numeric = copy.deepcopy({key: result[key] for key in keys})
    # Eligibility changes do not alter the paid amounts or frozen numerical result.
    for effect in numeric['normal_effects']:
        for field in ('activation', 'activation_modes', 'requires_enabled', 'scope',
                      'source_deduplication', 'controlled_action_read'):
            effect.pop(field, None)
    canonical = json.dumps(numeric, ensure_ascii=False,
                           sort_keys=True, separators=(',', ':'))
    return hashlib.sha256(canonical.encode('utf-8')).hexdigest()


def verify_candidate(bundle, parameters, result, baseline_parameters, baseline_result, previous_candidate_result=None):
    """Exercise the candidate contract and reproduce the archived v1 independently."""
    checks = []

    def check(name, condition, group):
        if not condition:
            raise AssertionError(f'{group}: {name}')
        checks.append({'group': group, 'name': name, 'status': 'passed'})

    check('v2 shape rules required', 'basic_stat_generation' in parameters, 'scope')
    native = bundle['native_export']['snapshot']
    process = bundle['process_evidence']
    expected_pools = {
        'sword.slender_thin.v1': ['critical', 'precision', 'dodge'],
        'sword.slender_thick.v1': ['critical', 'precision', 'crit_resist'],
        'sword.broad_thin.v1': ['precision', 'dodge', 'physical_defense'],
        'sword.broad_thick.v1': ['max_health', 'physical_defense', 'crit_resist'],
    }
    check('four complete profile ids and hard pools',
          parameters['basic_stat_generation']['shape_pools'] == expected_pools, 'shape_pools')

    shared_keys = ('body_split', 'profile_demands', 'stability_value', 'attack_value',
                   'stability_bias', 'attack_bias', 'reference_prices',
                   'coefficient_decimal_places', 'battle_shape', 'spirit')
    for key in shared_keys:
        check('unchanged v1 shared parameters: ' + key,
              parameters[key] == baseline_parameters[key], 'v1_regression')
    reproduced_v1 = calculate(bundle, baseline_parameters)
    historical_fields = ('original_metrics', 'body_split_by_utility', 'body_totals',
                         'budgets_after_bonds', 'requests', 'allocations', 'pruned_candidates',
                         'used_after_bonds', 'unused_after_bonds', 'normal_effects',
                         'binding_family', 'profile_values', 'source_trace', 'naming',
                         'stability_parameters', 'default_attack_parameters', 'actor_examples',
                         'sharpness', 'inventory', 'battle_footprint', 'handheld_stars',
                         'spirit_occupation', 'exclusive_control_skill_allocation',
                         'default_attack_rules', 'bonds', 'crafting_hours', 'crafting_energy')
    for key in historical_fields:
        check('archived numeric/effect result unchanged: ' + key,
              reproduced_v1[key] == baseline_result[key], 'v1_regression')
    check('v1 marked withdrawn without changing archived values',
          reproduced_v1['rule_status'] == 'historical_withdrawn_current_rules_invalid',
          'v1_regression')
    check('native allocator replay within tolerance',
          result['native_baseline_reproduction_max_allocation_error'] < 2e-5, 'v1_regression')

    profile_cases = []
    for kind, lam, tau in (('slender_thin', 12, .159), ('slender_thick', 12, .16),
                           ('broad_thin', 11.999, .159), ('broad_thick', 11.999, .16)):
        profile_id = f'sword.{kind}.v1'
        measured = copy.deepcopy(native)
        measured.update({'Width': 1, 'Length': lam, 'Thickness': tau,
                         'Name': '误导名字', 'Shape': '不参与分类的名字'})
        selected = shape_profile(measured, parameters)
        check('threshold boundary and name independence: ' + profile_id,
              selected['profile_id'] == profile_id and selected['kind'] == kind, 'shape_pools')
        ordinary, _ = ordinary_requests(measured, process, parameters, True)
        check('candidate exclusion before normalization: ' + profile_id,
              all(q['stat_id'] in expected_pools[profile_id]
                  for q in ordinary if q['carrier'] == 'base'), 'shape_pools')
        totals = defaultdict(float)
        for request in ordinary:
            totals[request['source']] += request['weight']
        check('ordinary per-source normalization: ' + profile_id,
              all(abs(value - 1) < 1e-12 for value in totals.values()), 'source_allocation')
        demands = ordinary + [
            {'id': 'profile/' + key, 'carrier': 'profile', 'atom': key,
             'minimum': value['minimum'], 'weight': value['group_priority'], 'costs': value['costs']}
            for key, value in parameters['profile_demands'].items()]
        budgets = dict(process['resources_after_bonds_before_effects'])
        budgets.update({'body/stability': result['body_totals']['stability_supply'],
                        'body/attack': result['body_totals']['attack_supply']})
        allocations, _ = solve(demands, budgets)
        paid = defaultdict(float)
        for request in demands:
            for key, cost in request['costs'].items():
                paid[key] += cost * allocations.get(request['id'], 0)
        output = shape_pool_outputs(demands, allocations, parameters)
        emitted = [e['stat_id'] for e in output if e['carrier'] == 'base']
        check('positive outputs remain in hard pool: ' + profile_id,
              set(emitted) <= set(expected_pools[profile_id]), 'shape_pools')
        check('source/common budgets conserved: ' + profile_id,
              all(value <= budgets[key] + 1e-7 for key, value in paid.items()), 'budgets')
        check('profile priority stays 0.6: ' + profile_id,
              all(q['weight'] == .6 for q in demands if q['carrier'] == 'profile'), 'budgets')
        profile_cases.append({'profile_id': profile_id, 'lambda': lam, 'tau': tau,
                              'allowed_stat_ids': expected_pools[profile_id],
                              'emitted_stat_ids': emitted,
                              'maximum_budget_excess': max([0] + [paid[key] - value for key, value in budgets.items()])})

    current_id = result['shape_profile']['profile_id']
    missing = copy.deepcopy(parameters)
    del missing['basic_stat_generation']['shape_pools'][current_id]
    rejected = False
    try:
        shape_profile(native, missing)
    except ValueError:
        rejected = True
    check('missing pool rejects rather than granting all six', rejected, 'shape_pools')
    empty = copy.deepcopy(parameters)
    empty['basic_stat_generation']['shape_pools'][current_id] = []
    empty_requests, _ = ordinary_requests(native, process, empty, True)
    check('empty pool never falls back to all six',
          not any(q['carrier'] == 'base' for q in empty_requests), 'shape_pools')
    small_stars, _ = ordinary_requests(native, process, parameters, True, 1)
    large_stars, _ = ordinary_requests(native, process, parameters, True, 100)
    check('global held stats do not scale with stars', small_stars == large_stars, 'scope')

    merged_requests = []
    for source in ('通', '破'):
        merged_requests.append({'id': 'base/precision/' + source, 'source': source,
                                'carrier': 'base', 'atom': 'precision', 'stat_id': 'precision',
                                'profile_id': current_id, 'coefficient': 3, 'weight': .5,
                                'costs': {'source/' + source: 1, 'root': 1, 'structure': 1}})
    merged = shape_pool_outputs(merged_requests,
                               {q['id']: 1.6 for q in merged_requests}, parameters)
    check('multi-source stat floors once after merging, not per source',
          len(merged) == 1 and merged[0]['value'] == 9, 'source_allocation')

    base = [e for e in result['normal_effects'] if e['carrier'] == 'base']
    check('Qinghe full profile id', current_id == 'sword.slender_thin.v1', 'qinghe_result')
    check('Qinghe actual six-stat result',
          {e['stat_id']: e['value'] for e in base} == {'critical': 11, 'precision': 6}, 'qinghe_result')
    check('base DTO uses fixed points and one full profile id',
          all(e['value_kind'] == 'int_flat' and e['profile_id'] == current_id for e in base), 'qinghe_result')
    check('base stats apply to owner globally while held or controlled and enabled',
          parameters['basic_stat_generation']['activation'] == 'held_or_controlled_enabled' and
          all(e['activation'] == 'held_or_controlled_enabled' and
              e['scope'] == 'owner_character_global_not_star_area' and
              e['controlled_action_read'] == 'owner_final_stat_once_no_duplicate_instance_bonus'
              for e in base), 'scope')
    check('base contract includes both eligible modes and still requires activation',
          parameters['basic_stat_generation']['activation_modes'] == ['held', 'controlled'] and
          parameters['basic_stat_generation']['requires_enabled'] and
          all(e['activation_modes'] == ['held', 'controlled'] and e['requires_enabled'] for e in base), 'scope')
    check('one instance and stat contributes once rather than once per mode or body cell',
          parameters['basic_stat_generation']['source_deduplication'] == 'source_instance_id+stat_id' and
          all(e['source_deduplication'] == 'source_instance_id+stat_id' for e in base), 'scope')
    check('equipment mode expansion does not expand the transformation held-only policy',
          parameters['normal_effect_changes']['binding_families'][0]['source_policy'] == 'held_active_only' and
          result['transformation_bundle']['source_policy'] == 'held_active_only', 'scope')
    check('ordinary attack is an entry in the same unified skill list',
          result['skills'][0]['skill_id'] == 'weapon.sword.ordinary_attack' and
          result['skill_dispatch']['ordinary_attack_is_a_skill'], 'skill_dispatch')
    check('resource shortage and invalid conditions skip without selecting substitutes',
          result['skill_dispatch']['resource_insufficient'] == 'skip_this_skill_without_replacement' and
          result['skill_dispatch']['other_conditions_invalid'] == 'skip_this_skill_without_replacement' and
          not result['skill_dispatch']['automatic_substitution'] and
          not result['skill_dispatch']['automatic_extra_ordinary_attack'], 'skill_dispatch')
    fixture_skill = {'skill_id': 'fixture:costly_skill', 'energy_cost': 2, 'mana_cost': 3}
    for label, energy, mana, valid, can_execute in (
            ('energy_shortage', 1, 3, True, False), ('mana_shortage', 2, 2, True, False),
            ('condition_invalid', 2, 3, False, False), ('payable', 2, 3, True, True)):
        attempt = preview_skill_attempt(fixture_skill, energy, mana, valid)
        check('unified per-skill preview: ' + label,
              attempt['can_execute'] == can_execute and attempt['skill_id'] == fixture_skill['skill_id']
              and attempt['substituted_skill_id'] is None and not attempt['automatic_extra_attack'], 'skill_dispatch')
    check('multi-skill scanning order remains undecided',
          result['skill_dispatch']['multi_skill_scan_order_status'] == 'not_decided', 'skill_dispatch')
    check('no keyword/trigger/direct/summon raw effects',
          all(e['carrier'] in ('base', 'binding') for e in result['normal_effects']), 'scope')
    for effect in result['normal_effects']:
        traced = sum(s['normal_effect_source_contributions'].get(effect['id'], 0)
                     for s in result['source_trace'])
        check('source receipts conserve ' + effect['id'],
              abs(traced - effect['allocation']) < 1e-8, 'source_allocation')
    for key, budget in result['budgets_after_bonds'].items():
        used = result['used_after_bonds'].get(key, 0)
        unused = result['unused_after_bonds'][key]
        check('Qinghe budget conserved: ' + key,
              used <= budget + 1e-7 and abs(used + unused - budget) < 1e-7, 'budgets')
    check('bond prepayments and priority unchanged', result['bonds'] == baseline_result['bonds'], 'budgets')

    package = result['transformation_bundle']
    binding = next(e for e in result['normal_effects'] if e['carrier'] == 'binding')
    check('registered gejian template and version',
          package['template_id'] == 'artifact.gejian.defuse_flat.v1' and package['template_version'] == 1,
          'transformation')
    check('package amount derived from paid source rather than fixed literal',
          package['operations'] == [{'id': 'add_response_defuse_flat', 'parameters': {'amount': 3}}]
          and 3 == math.floor(.5 * package['generation']['allocation_X'] + 1e-9), 'transformation')
    check('package source and generation receipts match actual output',
          package['source_instance_id'] == native['InstanceId'] and
          package['generation']['budget_debits'] == binding['budget_debits'], 'transformation')
    check('chemical source held-only and one placed target',
          package['source_policy'] == 'held_active_only' and package['target_policy'] == 'one_placed_copy',
          'transformation')

    stable_expected = {'base': 11.3916, 'soul_coefficient': .8741,
                       'sword_proficiency_coefficient': .2719,
                       'this_item_spirit_occupation_coefficient': 2.6992}
    attack_expected = {'base': 3.8905, 'soul_coefficient': .2045,
                       'sword_proficiency_coefficient': .3282, 'damage_type': 'physical'}
    check('Qinghe four stable coefficients', result['stability_parameters'] == stable_expected, 'coefficient_groups')
    check('Qinghe three attack coefficients and physical type',
          result['default_attack_parameters'] == attack_expected, 'coefficient_groups')
    for group, values in (('stability', result['stability_parameters']),
                          ('attack', result['default_attack_parameters'])):
        raw = result['profile_values'][group + '_before_quantization']
        check('four-decimal downward quantization: ' + group,
              all(values[key] == quantize(value, parameters['coefficient_decimal_places'])
                  for key, value in raw.items()), 'coefficient_groups')
    actor = evaluate(result['stability_parameters'], result['default_attack_parameters'], 10, 10, 2)
    check('10 soul/10 sword/occupation 2 evaluates to stability 28, raw physical 9',
          actor['stability_cap'] == 28 and actor['raw_physical_damage'] == 9, 'coefficient_groups')
    check('higher soul/sword never lowers the two outputs',
          all(evaluate(result['stability_parameters'], result['default_attack_parameters'],
                       e['soul'] + delta_s, e['sword_proficiency'] + delta_w, e['occupation'])[key] >= e[key]
              for e in result['actor_examples'] for delta_s, delta_w in ((1, 0), (0, 1))
              for key in ('stability_cap', 'raw_physical_damage')), 'coefficient_groups')
    check('profiles are paid at priority 0.6, not normalized to one',
          all(q['weight'] == .6 for q in result['requests'] if q['carrier'] == 'profile'), 'coefficient_groups')

    check('positive actual effects yield two short meaningful prefixes',
          result['naming']['auto_name'] == '洞隙·明察·薄脊剑' and
          result['naming']['prefix_groups'] == ['洞隙', '明察'], 'naming')
    check('Qinghe explicit existing name retained', result['naming']['display_name'] == '清和剑', 'naming')
    zeroed = copy.deepcopy(result['normal_effects'])
    for effect in zeroed:
        effect['value'] = 0
    empty_name, empty_prefixes = automatic_name(native, zeroed, parameters['automatic_naming'])
    check('zero effects cannot generate prefixes', empty_name == '薄脊剑' and not empty_prefixes, 'naming')
    renamed_input = copy.deepcopy(bundle)
    renamed_input['explicit_custom_name'] = '验收专名'
    renamed = calculate(renamed_input, parameters)
    check('explicit rename changes display only, no reforging',
          renamed['naming']['display_name'] == '验收专名' and
          all(renamed[key] == result[key] for key in
              ('allocations', 'normal_effects', 'stability_parameters', 'default_attack_parameters')),
          'naming')
    renamed_input['explicit_custom_name'] = ''
    unnamed = calculate(renamed_input, parameters)
    check('explicit empty custom name uses actual auto name',
          unnamed['naming']['display_name'] == result['naming']['auto_name'], 'naming')
    check('candidate neither modifies Godot runtime nor player saves',
          not result['runtime_modified'] and not result['player_save_modified'] and
          package['runtime_status'] == 'independent_candidate_not_wired_to_godot', 'scope')

    check('historical v1 retains its original two-cell coverage',
          reproduced_v1['handheld_stars'] == {'inward_length': 2, 'width': 1, 'cells': 2}, 'range_revision')
    geometry = result['handheld_stars']
    check('three-cell tip range is user-confirmed',
          geometry['status'] == 'confirmed_by_user_for_qinghe' and geometry['origin'] == 'blade_tip',
          'range_revision')
    check('canonical tip anchor and offsets are explicit',
          geometry['source_anchor'] == [1, 0] and geometry['offsets'] == [[1, 0], [2, 0], [3, 0]],
          'range_revision')
    check('canonical coverage occupies coordinates 2/3/4, not the sword body',
          geometry['range_cells'] == [[2, 0], [3, 0], [4, 0]], 'range_revision')
    check('Qinghe body remains two cells independent of the three range markers',
          result['battle_footprint']['length'] == 2 and result['battle_footprint']['width'] == 1
          and result['battle_footprint']['occupied_cell_offsets'] == [[0, 0], [1, 0]]
          and geometry['cells'] == 3, 'range_revision')
    check('inventory remains 2x6 rather than becoming either combat geometry',
          result['inventory']['width'] == 2 and result['inventory']['height'] == 6, 'range_revision')
    expected_anchors = [[1, 0], [0, 1], [-1, 0], [0, -1]]
    expected_rays = [
        [[2, 0], [3, 0], [4, 0]], [[0, 2], [0, 3], [0, 4]],
        [[-2, 0], [-3, 0], [-4, 0]], [[0, -2], [0, -3], [0, -4]],
    ]
    rotations = []
    for turns in range(4):
        oriented = held_sharpness_geometry(parameters, turns)
        body = [rotate_cell(cell, turns) for cell in result['battle_footprint']['occupied_cell_offsets']]
        check('tip and complete ray follow sword rotation: ' + str(turns),
              oriented['source_anchor'] == expected_anchors[turns]
              and oriented['range_cells'] == expected_rays[turns], 'range_revision')
        check('range markers never count as body cells: ' + str(turns),
              len(body) == 2 and len(oriented['range_cells']) == 3 and
              not set(map(tuple, body)) & set(map(tuple, oriented['range_cells'])), 'range_revision')
        rotations.append({'quarter_turns': turns, 'body_cells': body,
                          'tip_anchor': oriented['source_anchor'], 'range_cells': oriented['range_cells']})
    check('full turn restores canonical geometry', held_sharpness_geometry(parameters, 4) == geometry, 'range_revision')
    check('negative turn follows the same orientation convention',
          held_sharpness_geometry(parameters, -1) == held_sharpness_geometry(parameters, 3), 'range_revision')
    unrelated_sizes = copy.deepcopy(parameters)
    unrelated_sizes['battle_shape']['length_per_cell'] = 100
    unrelated_sizes['battle_shape']['width_per_cell'] = 100
    check('coverage is not inferred from the battle footprint formula',
          held_sharpness_geometry(unrelated_sizes) == geometry, 'range_revision')
    check('coverage remains character-sword-only with no occupancy or attack reach changes',
          geometry['target'] == 'character_sword_techniques_only' and
          not geometry['occupies_board_cells'] and not geometry['changes_attack_range'], 'range_revision')
    current_core_hash = numerical_core_sha256(result)
    previous_core_hash = numerical_core_sha256(previous_candidate_result) if previous_candidate_result else None
    if previous_core_hash is not None:
        check('range revision leaves all numerical sources/effects/budgets/coefficients unchanged',
              previous_core_hash == current_core_hash, 'range_revision')
    bundle_hash = hashlib.sha256(json.dumps(package, ensure_ascii=False, sort_keys=True,
                                            separators=(',', ':')).encode('utf-8')).hexdigest()
    return {
        'schema_version': 1, 'status': 'passed', 'rules_version': parameters['version'],
        'rule_status': 'complete_shape_pool_candidate_not_live_not_balance_approval',
        'checks_passed': len(checks), 'checks': checks,
        'v1_regression': {'archived_fields_equal': list(historical_fields),
                          'archive_values_modified': False,
                          'native_reproduction_max_allocation_error':
                              result['native_baseline_reproduction_max_allocation_error']},
        'shape_pools': profile_cases,
        'range_revision': {
            'status': 'qinghe_tip_forward_three_cells_confirmed', 'canonical': geometry,
            'four_rotations': rotations, 'v1_historical_two_cells_preserved': True,
            'numerical_core_sha256': current_core_hash,
            'previous_numerical_core_sha256': previous_core_hash,
            'numerical_core_unchanged': previous_core_hash == current_core_hash if previous_core_hash else None,
            'transformation_bundle_sha256': bundle_hash,
        },
        'equipment_effect_usage_revision': {
            'status': 'held_or_controlled_enabled_confirmed_by_user',
            'activation_modes': ['held', 'controlled'], 'requires_enabled': True,
            'recipient': 'owner_character_global', 'deduplication': 'source_instance_id+stat_id',
            'controlled_action_read': 'owner_final_stats_once_no_duplicate_instance_bonus',
            'transformation_policy_unchanged': 'held_active_only',
            'numeric_core_excludes_usage_contract_fields': True,
            'actual_runtime_attribute_ledger_implemented': False,
        },
        'skill_dispatch_revision': result['skill_dispatch'],
        'qinghe_result': {
            'profile_id': current_id, 'base_stats': {e['stat_id']: e['value'] for e in base},
            'transformation_amount': package['operations'][0]['parameters']['amount'],
            'stability_parameters': result['stability_parameters'],
            'default_attack_parameters': result['default_attack_parameters'],
            'actor_10_10_occupation_2': actor, 'naming': result['naming'],
            'budgets_after_bonds': result['budgets_after_bonds'],
            'used_after_bonds': result['used_after_bonds'],
            'unused_after_bonds': result['unused_after_bonds'],
        },
        'runtime_modified': False, 'player_save_modified': False,
    }


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--input',type=Path,required=True)
    parser.add_argument('--parameters',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--verification-output', type=Path)
    parser.add_argument('--baseline-parameters', type=Path)
    parser.add_argument('--baseline-result', type=Path)
    args=parser.parse_args()
    verification_paths = (args.verification_output, args.baseline_parameters, args.baseline_result)
    if any(verification_paths) and not all(verification_paths):
        parser.error('--verification-output requires --baseline-parameters and --baseline-result together.')
    bundle=json.loads(args.input.read_text(encoding='utf-8-sig'))
    parameters=json.loads(args.parameters.read_text(encoding='utf-8-sig'))
    result=calculate(bundle,parameters)
    result['input_sha256']=hashlib.sha256(args.input.read_bytes()).hexdigest()
    result['parameters_sha256']=hashlib.sha256(args.parameters.read_bytes()).hexdigest()
    verification = None
    if args.verification_output:
        protected = (args.input, args.baseline_parameters, args.baseline_result)
        before = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in protected}
        previous = json.loads(args.output.read_text(encoding='utf-8-sig')) if args.output.exists() else None
        verification = verify_candidate(bundle, parameters, result,
            json.loads(args.baseline_parameters.read_text(encoding='utf-8-sig')),
            json.loads(args.baseline_result.read_text(encoding='utf-8-sig')), previous)
        after = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in protected}
        if before != after:
            raise AssertionError('Verification modified an input or v1 archive.')
        verification['protected_input_and_v1_archive_sha256'] = after
        verification['input_and_v1_archive_hashes_unchanged'] = True
        verification['input_sha256'] = result['input_sha256']
        verification['parameters_sha256'] = result['parameters_sha256']
        verification['calculator_sha256'] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    if verification is not None:
        verification['result_sha256'] = hashlib.sha256(args.output.read_bytes()).hexdigest()
        verification['result_file'] = str(args.output).replace('\\', '/')
        args.verification_output.parent.mkdir(parents=True, exist_ok=True)
        args.verification_output.write_text(json.dumps(verification, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({'status':'passed','rule_status':result['rule_status'],'shape_profile':result.get('shape_profile'),'parameters':result['stability_parameters'],'attack':result['default_attack_parameters'],'actor_10':result['actor_examples'][1],'effects':result['normal_effects'],'spirit':result['spirit_occupation'],'binding':result['binding_family'],'naming':result['naming'],'native_reproduction_error':result['native_baseline_reproduction_max_allocation_error']},ensure_ascii=False))
    if verification is not None:
        print(json.dumps({'verification': 'passed', 'checks_passed': verification['checks_passed'],
                          'output': str(args.verification_output)}, ensure_ascii=False))


if __name__=='__main__': main()
