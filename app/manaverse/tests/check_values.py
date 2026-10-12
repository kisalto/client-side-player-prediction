"""Confere os valores P1/P3 calculados pela classe C++ (test_predictor) contra o
sklearn (pipe.pkl) para o MESMO vetor de features. O unico valor nao controlado e
time_since_last_action (depende do sleep), entao varre uma janela em torno do esperado."""
import sys, joblib, numpy as np, pandas as pd
F = ['x','y','hp','max_hp','hp_ratio','velocity_x','velocity_y','speed','time_since_last_action',
     'time_since_last_hurt','last_damage_taken','dead','prev_action_was_attack']
pipe = joblib.load('pipe.pkl'); ai = list(pipe.classes_).index('attack')
def pa(hp, ts):
    r = dict(x=50,y=60,hp=hp,max_hp=100,hp_ratio=hp/100,velocity_x=0,velocity_y=0,speed=0,
             time_since_last_action=ts,time_since_last_hurt=-1.0,last_damage_taken=0,dead=0,prev_action_was_attack=0)
    return float(pipe.predict_proba(pd.DataFrame([r])[F])[0][ai])
got = {l.split()[0]: float(l.split()[1]) for l in open(sys.argv[1]) if l.startswith(('P1 ','P3 '))}
ok = True
for name, hp, lo, hi in (('P1', 35, 0.02, 0.20), ('P3', 99, 0.25, 0.55)):
    allowed = {round(pa(hp, ts), 4) for ts in np.arange(lo, hi, 0.01)}
    good = round(got[name], 4) in allowed
    ok &= good
    print(f"[{'OK' if good else 'FALHA'}] {name}: C++={got[name]:.4f} sklearn(janela de tempo)={sorted(allowed)}")
sys.exit(0 if ok else 1)
