"""Confere os valores P1/P3 calculados pela classe C++ (test_predictor) contra o
sklearn (pipe.pkl) para o MESMO vetor de features. O unico valor nao controlado e
time_since_last_action (depende do sleep), entao varre uma janela em torno do esperado.
Com um 2o argumento (CSV do TCC_PREDICTOR_LOG), confere tambem o log de avaliacao."""
import sys, joblib, numpy as np, pandas as pd
F = ['x','y','velocity_x','velocity_y','speed','time_since_last_action',
     'time_since_last_hurt','last_damage_taken','prev_action_was_attack']
pipe = joblib.load('pipe.pkl'); ai = list(pipe.classes_).index('attack')
def pa(hp, ts):  # hp nao e feature (fica so pra manter os casos P1/P3 legiveis)
    r = dict(x=50,y=60,velocity_x=0,velocity_y=0,speed=0,
             time_since_last_action=ts,time_since_last_hurt=-1.0,last_damage_taken=0,prev_action_was_attack=0)
    return float(pipe.predict_proba(pd.DataFrame([r])[F])[0][ai])
got = {l.split()[0]: float(l.split()[1]) for l in open(sys.argv[1]) if l.startswith(('P1 ','P3 '))}
ok = True
for name, hp, lo, hi in (('P1', 35, 0.02, 0.20), ('P3', 99, 0.25, 0.55)):
    allowed = {round(pa(hp, ts), 4) for ts in np.arange(lo, hi, 0.01)}
    good = round(got[name], 4) in allowed
    ok &= good
    print(f"[{'OK' if good else 'FALHA'}] {name}: C++={got[name]:.4f} sklearn(janela de tempo)={sorted(allowed)}")

def check(cond, msg):
    global ok
    ok &= bool(cond)
    print(f"[{'OK' if cond else 'FALHA'}] {msg}")

if len(sys.argv) > 2:
    log = pd.read_csv(sys.argv[2])
    check(list(log.columns[:6]) == ['wall_time','event','being_id','name','p_attack','threshold']
          and list(log.columns[6:]) == F, 'log: cabecalho = 6 colunas de contexto + as 9 features na ordem do modelo')
    check((log.columns == 'wall_time').sum() == 1 and (log.event == 'wall_time').sum() == 0,
          'log: cabecalho escrito uma vez so')
    check({'init','move','attack','infer','telegraph'} <= set(log.event), 'log: tem init, move, attack, infer e telegraph')
    infer = log[log.event == 'infer']
    check(infer[F].notna().all().all(), 'log: linhas infer tem as 9 features preenchidas')
    check(np.isclose(infer.p_attack, [round(got['P1'], 5)], atol=1e-5).any()
          and np.isclose(infer.p_attack, [round(got['P3'], 5)], atol=1e-5).any(), 'log: P(attack) de P1 e P3 aparecem nas linhas infer')
    # P(attack) do log == sklearn sobre as features do proprio log (todas as inferencias)
    sk = pipe.predict_proba(infer[F])[:, ai]
    check(np.abs(sk - infer.p_attack).max() < 1e-3, f'log: P(attack) == sklearn(features do log) (diff max {np.abs(sk - infer.p_attack).max():.1e})')
    check((log.name == 'Ama "x", y').any(), 'log: nome com aspas e virgula sobrevive ao CSV')
    tel = log[log.event == 'telegraph']
    check(tel.threshold.isna().all() or (tel.threshold > 0).all(), 'log: telegraph registra o limiar (>0) ou vazio')
    now = __import__('time').time()
    check(((now - log.wall_time) < 600).all(), 'log: wall_time e epoch (relogio de parede, mesmo do bridge_recv_time)')

sys.exit(0 if ok else 1)
