"""
compare_models.py
---------------------------------------------------------------------------
Comparacao dos modelos tabulares e dos baselines no MESMO protocolo da
Tarefa 1b do train_model.py: ataque como classe positiva, leave-one-player-out
por PESSOA. O que muda em relacao ao train_model.py:

- varias sementes por modelo estocastico (RF, XGBoost, MLP): a variacao entre
  sementes vai pro relatorio, nao so o resultado de uma rodada;
- salva a P(ataque) out-of-fold de CADA linha do dataset, de cada modelo e
  semente (oof_tabular.csv.gz). statistical_analysis.py usa essas predicoes
  pra bootstrap, testes pareados, calibracao etc. -- todos os modelos
  (inclusive LSTM/GRU do train_sequence_model.py) avaliados nas MESMAS linhas;
- hiperparametros fixos, definidos antes de olhar o teste (nenhuma busca),
  pra nenhum modelo sair na frente so por ter sido mais "tunado";
- modelos classicos pra banca: regressao logistica (features cruas e com log
  nas caudas longas), LDA e cadeias de Markov de ordem k sobre a sequencia
  move/ataque (a generalizacao da regra "repete a acao anterior");
- importancia por permutacao (queda de PR-AUC) no jogador de teste de cada
  fold, por feature e por grupo de features correlacionadas;
- (--learning-curve) curva de aprendizado: PR-AUC no jogador de teste em
  funcao do NUMERO DE PESSOAS no treino e da FRACAO do tempo de jogo de cada
  pessoa -- mostra se coletar mais dados ainda ajudaria.

O limiar de decisao NAO e escolhido aqui: statistical_analysis.py escolhe,
pra cada jogador de teste, o limiar de recall-alvo nas predicoes OOF dos
OUTROS jogadores (mesma regra pra todos os modelos). Pra RF tambem sai o
limiar "estrito" do train_model.py (LOPO interno so nos jogadores de treino),
como checagem de sensibilidade.

Uso:
    python compare_models.py --dataset dataset.csv --out-dir resultados/ [--seeds 5] [--learning-curve]
"""
import argparse
import itertools
import json
import os
import time

import numpy as np
import pandas as pd
from sklearn.discriminant_analysis import LinearDiscriminantAnalysis
from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import average_precision_score
from sklearn.model_selection import LeaveOneGroupOut
from sklearn.neural_network import MLPClassifier
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import FunctionTransformer, StandardScaler
from xgboost import XGBClassifier

from train_model import ALL_FEATURES, load_dataset, pick_threshold

BLOCK = ['session_id', 'char_id']  # historico nunca atravessa sessao nem personagem
MARKOV_ORDERS = (1, 2, 3, 5)
DETERMINISTIC = ['regressao_logistica', 'regressao_logistica_log', 'lda']
STOCHASTIC = ['random_forest', 'xgboost', 'mlp']
PERM_MODELS = ['random_forest', 'xgboost', 'regressao_logistica_log', 'mlp']
LC_MODELS = ['random_forest', 'xgboost', 'regressao_logistica_log']
LC_FRACTIONS = (0.1, 0.25, 0.5, 0.75, 1.0)
# grupos de features correlacionadas: permutar uma so do grupo subestima a
# importancia dele (as outras do grupo compensam)
FEATURE_GROUPS = {
    'grupo_posicao(x,y)': ['x', 'y'],
    'grupo_velocidade(vx,vy,speed)': ['velocity_x', 'velocity_y', 'speed'],
    'grupo_dano(tslh,last_damage)': ['time_since_last_hurt', 'last_damage_taken'],
}


def log_features(X):
    """As 9 features com log1p nas caudas longas (tempo, velocidade, dano) e um
    indicador 'nunca levou dano' no lugar do sentinela -1. Arvores nao precisam
    disso; modelos lineares e redes sim (time_since_* vai de ms a dezenas de min)."""
    out = pd.DataFrame(index=X.index)
    for col in ('x', 'y', 'velocity_x', 'velocity_y'):
        out[col] = X[col].astype(float)
    out['log_speed'] = np.log1p(X['speed'].clip(lower=0))
    out['log_time_since_last_action'] = np.log1p(X['time_since_last_action'].clip(lower=0))
    never = X['time_since_last_hurt'] < 0
    out['never_hurt'] = never.astype(float)
    out['log_time_since_last_hurt'] = np.where(never, 0.0, np.log1p(X['time_since_last_hurt'].clip(lower=0)))
    out['log_last_damage_taken'] = np.log1p(X['last_damage_taken'].clip(lower=0))
    out['prev_action_was_attack'] = X['prev_action_was_attack'].astype(float)
    return out


def make_model(name, seed):
    if name == 'regressao_logistica':
        return Pipeline([('scale', StandardScaler()), ('clf', LogisticRegression(max_iter=5000))])
    if name == 'regressao_logistica_log':
        return Pipeline([('log', FunctionTransformer(log_features)), ('scale', StandardScaler()),
                         ('clf', LogisticRegression(max_iter=5000))])
    if name == 'lda':
        return Pipeline([('log', FunctionTransformer(log_features)), ('scale', StandardScaler()),
                         ('clf', LinearDiscriminantAnalysis())])
    if name == 'random_forest':
        # mesma configuracao da RF do cliente (train_model.make_attack_rf); n_jobs nao muda o resultado
        return RandomForestClassifier(n_estimators=300, random_state=seed, class_weight='balanced', n_jobs=-1)
    if name == 'xgboost':
        return XGBClassifier(n_estimators=400, learning_rate=0.05, max_depth=6, subsample=0.8,
                             colsample_bytree=0.8, tree_method='hist', random_state=seed, n_jobs=-1)
    if name == 'mlp':
        # igual a MLP da Tarefa 1b do train_model.py
        return Pipeline([('scale', StandardScaler()),
                         ('clf', MLPClassifier(hidden_layer_sizes=(64, 32), max_iter=500, random_state=seed,
                                               early_stopping=True, n_iter_no_change=15))])
    raise ValueError(name)


def check_block_order(df):
    # varias funcoes aqui (Markov, curva por fracao, sequencias) assumem que as
    # linhas de cada (sessao, personagem) estao em ordem de tempo -- o
    # build_dataset.py ja gera assim
    ok = df.groupby(BLOCK, sort=False)['t'].apply(lambda s: s.is_monotonic_increasing).all()
    if not ok:
        raise SystemExit('dataset fora de ordem de tempo dentro de (session_id, char_id)')


def markov_codes(df, order):
    """Codigo do historico das ultimas `order` acoes (0=move, 1=ataque, 2=antes
    do inicio do bloco). Lag 1 = prev_action_was_attack da propria linha; lag j
    = prev_action_was_attack da linha j-1 posicoes antes no mesmo bloco."""
    prev = df.groupby(BLOCK, sort=False)['prev_action_was_attack']
    code = np.zeros(len(df), dtype=np.int64)
    for j in range(1, order + 1):
        lag = prev.shift(j - 1).fillna(2).astype(int).to_numpy()
        code = code * 3 + lag
    return code


def markov_fit_predict(codes, is_attack, train_idx, test_idx, order):
    """P(ataque | ultimas k acoes) estimada nos jogadores de treino, com
    suavizacao (1 pseudo-observacao) em direcao a prevalencia do treino."""
    n_states = 3 ** order
    n = np.bincount(codes[train_idx], minlength=n_states)
    a = np.bincount(codes[train_idx], weights=is_attack[train_idx], minlength=n_states)
    prior = is_attack[train_idx].mean()
    return ((a + prior) / (n + 1.0))[codes[test_idx]]


def permutation_importance_ap(model, X, y, n_repeats, rng):
    """Queda de PR-AUC ao embaralhar cada feature (e cada grupo) no jogador de teste."""
    base = average_precision_score(y, model.predict_proba(X)[:, 1])
    rows = []
    for name, cols in [(c, [c]) for c in ALL_FEATURES] + list(FEATURE_GROUPS.items()):
        drops = []
        for _ in range(n_repeats):
            Xp = X.copy()
            Xp[cols] = X[cols].to_numpy()[rng.permutation(len(X))]
            drops.append(base - average_precision_score(y, model.predict_proba(Xp)[:, 1]))
        rows.append({'feature': name, 'queda_pr_auc_media': float(np.mean(drops)),
                     'queda_pr_auc_dp': float(np.std(drops, ddof=1)), 'pr_auc_base': float(base)})
    return rows


def run_lopo(df, seeds, perm_repeats, inner_threshold, target_recall, log):
    X, groups = df[ALL_FEATURES], df['player_id']
    y = (df['action_type'] == 'attack').to_numpy(dtype=int)
    n = len(df)

    preds = {'baseline_repete_anterior@0': df['prev_action_was_attack'].to_numpy(dtype=float)}
    codes = {k: markov_codes(df, k) for k in MARKOV_ORDERS}
    for k in MARKOV_ORDERS:
        preds[f'markov_ordem{k}@0'] = np.full(n, np.nan)
    runs = [(m, seeds[0]) for m in DETERMINISTIC] + [(m, s) for m in STOCHASTIC for s in seeds]
    for m, s in runs:
        preds[f'{m}@{s}'] = np.full(n, np.nan)

    folds, importances = [], []
    rng = np.random.default_rng(seeds[0])
    for train_idx, test_idx in LeaveOneGroupOut().split(X, y, groups):
        person = groups.iloc[test_idx[0]]
        fold = {'jogador_teste': person, 'n_teste': int(len(test_idx)), 'ataques_teste': int(y[test_idx].sum()),
                'tempo_s': {}}
        for k in MARKOV_ORDERS:
            preds[f'markov_ordem{k}@0'][test_idx] = markov_fit_predict(codes[k], y, train_idx, test_idx, k)

        for m, s in runs:
            t0 = time.time()
            model = make_model(m, s).fit(X.iloc[train_idx], y[train_idx])
            p = model.predict_proba(X.iloc[test_idx])[:, 1]
            preds[f'{m}@{s}'][test_idx] = p
            fold['tempo_s'][f'{m}@{s}'] = round(time.time() - t0, 1)
            log(f'  {person} {m}@{s}: PR-AUC {average_precision_score(y[test_idx], p):.3f} '
                f'({time.time() - t0:.0f}s)')
            if s == seeds[0] and m in PERM_MODELS and perm_repeats:
                for r in permutation_importance_ap(model, X.iloc[test_idx], y[test_idx], perm_repeats, rng):
                    importances.append({'jogador_teste': person, 'modelo': m, **r})

        if inner_threshold:
            # limiar "estrito" do train_model.py: LOPO interno so nos jogadores de treino
            sub = df.iloc[train_idx]
            inner = np.zeros(len(sub))
            for tr, te in LeaveOneGroupOut().split(sub, groups=sub['player_id']):
                rf = make_model('random_forest', seeds[0]).fit(sub[ALL_FEATURES].iloc[tr], y[train_idx][tr])
                inner[te] = rf.predict_proba(sub[ALL_FEATURES].iloc[te])[:, 1]
            fold['limiar_rf_lopo_interno'] = pick_threshold(y[train_idx], inner, target_recall)
            log(f'  {person} limiar RF (LOPO interno): {fold["limiar_rf_lopo_interno"]:.3f}')
        folds.append(fold)

    return pd.DataFrame(preds), folds, pd.DataFrame(importances)


def run_learning_curve(df, seed, log):
    X = df[ALL_FEATURES]
    y = (df['action_type'] == 'attack').to_numpy(dtype=int)
    persons = sorted(df['player_id'].unique())
    # posicao relativa (0-1] de cada linha no tempo de jogo do seu bloco
    block = df.groupby(BLOCK, sort=False)
    frac_pos = (block.cumcount() + 1) / block['t'].transform('size')
    rows = []

    def fit_eval(tr_mask, te_mask, extra):
        for m in LC_MODELS:
            model = make_model(m, seed).fit(X[tr_mask], y[tr_mask])
            ap = average_precision_score(y[te_mask], model.predict_proba(X[te_mask])[:, 1])
            rows.append({'modelo': m, 'n_treino': int(tr_mask.sum()), 'pr_auc': float(ap), **extra})

    for test in persons:
        te = (df['player_id'] == test).to_numpy()
        others = [p for p in persons if p != test]
        for k in range(1, len(others) + 1):
            for subset in itertools.combinations(others, k):
                tr = df['player_id'].isin(subset).to_numpy()
                fit_eval(tr, te, {'jogador_teste': test, 'curva': 'n_pessoas', 'n_pessoas': k, 'fracao': 1.0,
                                  'pessoas_treino': '+'.join(subset)})
            log(f'  curva {test}: {k} pessoa(s) no treino ok')
        for f in LC_FRACTIONS:
            tr = ((df['player_id'] != test) & (frac_pos <= f)).to_numpy()
            fit_eval(tr, te, {'jogador_teste': test, 'curva': 'fracao_do_tempo', 'n_pessoas': len(others),
                              'fracao': f, 'pessoas_treino': '+'.join(others)})
        log(f'  curva {test}: fracoes ok')
    return pd.DataFrame(rows)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--dataset', default='dataset.csv')
    ap.add_argument('--out-dir', default='resultados')
    ap.add_argument('--seeds', type=int, default=5, help='sementes por modelo estocastico (RF, XGBoost, MLP)')
    ap.add_argument('--base-seed', type=int, default=42)
    ap.add_argument('--perm-repeats', type=int, default=10, help='repeticoes da importancia por permutacao (0 = pula)')
    ap.add_argument('--no-inner-threshold', action='store_true', help='pula o limiar estrito (LOPO interno) da RF')
    ap.add_argument('--target-recall', type=float, default=0.7)
    ap.add_argument('--learning-curve', action='store_true')
    args = ap.parse_args()

    os.makedirs(args.out_dir, exist_ok=True)
    log_file = open(os.path.join(args.out_dir, 'compare_models.log'), 'a')

    def log(msg):
        print(msg, flush=True)
        log_file.write(msg + '\n')
        log_file.flush()

    df = load_dataset(args.dataset)
    check_block_order(df)
    seeds = [args.base_seed + i for i in range(args.seeds)]
    log(f'dataset: {len(df)} linhas, {df.player_id.nunique()} pessoas, {int((df.action_type == "attack").sum())} '
        f'ataques | sementes {seeds}')

    t0 = time.time()
    preds, folds, importances = run_lopo(df, seeds, args.perm_repeats, not args.no_inner_threshold,
                                         args.target_recall, log)
    preds.insert(0, 'row', np.arange(len(df)))
    preds.to_csv(os.path.join(args.out_dir, 'oof_tabular.csv.gz'), index=False)
    if len(importances):
        importances.to_csv(os.path.join(args.out_dir, 'perm_importance.csv'), index=False)
    with open(os.path.join(args.out_dir, 'folds_tabular.json'), 'w') as f:
        json.dump({'sementes': seeds, 'folds': folds}, f, indent=2)
    log(f'LOPO tabular ok em {time.time() - t0:.0f}s -> {args.out_dir}/oof_tabular.csv.gz')

    if args.learning_curve:
        t0 = time.time()
        lc = run_learning_curve(df, seeds[0], log)
        lc.to_csv(os.path.join(args.out_dir, 'learning_curve.csv'), index=False)
        log(f'curva de aprendizado ok em {time.time() - t0:.0f}s')


if __name__ == '__main__':
    main()
