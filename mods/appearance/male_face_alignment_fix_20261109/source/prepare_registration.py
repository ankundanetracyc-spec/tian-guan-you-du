"""Refresh only explicitly supplied native pair controls; staging stays separate."""
import argparse
import json
import numpy as np
from PIL import Image
from diagnose_geometry import PACK,centroid,write_json

def main():
    parser=argparse.ArgumentParser()
    for n in [1,4,5]:parser.add_argument(f'--eyes{n:02}-native')
    args=parser.parse_args()
    path=PACK/'source/feature_registration.json';config=json.loads(path.read_text(encoding='utf-8-sig'))
    for n in [1,4,5]:
        native=getattr(args,f'eyes{n:02}_native')
        if not native:continue
        record=next(r for r in config['records'] if r['part']=='eyes' and r['index']==n)
        arr=np.asarray(Image.open(PACK/native).convert('RGBA'))
        record['native']=native
        record['native_points']=[centroid(arr,roi,100) for roi in [[453,358,475,374],[557,358,581,374]]]
        record['native_control_method']='MeanRGB dark kernel100 times alpha in fixed small source ROIs [453,358,475,374]/[557,358,581,374]. Final semantic/probe measurements are independent and mandatory.'
    write_json(path,config)
    print('Explicit native controls refreshed; run register_features.py then measure_final_semantics.py and independent visual gate.')

if __name__=='__main__':main()
