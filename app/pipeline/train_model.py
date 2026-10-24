"""
train_model.py
---------------------------------------------------------------------------
Fase 5+6: modelo baseline pra prever o tipo da proxima acao do jogador
(move vs attack) a partir do dataset.csv da Fase 4, comparado contra
baselines ingenuos (aleatorio e classe majoritaria). Random Forest e MLP
(Fase 6) como modelos principais; XGBoost entra tambem se estiver
instalado. A parte de sequencia temporal (LSTM/GRU, resto da Fase 6) fica
em train_sequence_model.py -- e um experimento a parte porque precisa de
uma preparacao de dados bem diferente (sequencias por jogador, nao linhas
independentes) e PyTorch em vez de scikit-learn.

Alem da classificacao de tipo, inclui uma segunda tarefa (regressao):
prever a DIRECAO do movimento (move_dx, move_dy) quando a acao e "move",
comparando contra um baseline de Dead Reckoning (extrapolar a velocidade
atual) -- essa e a comparacao central da Introducao do TCC (Duarte Jr. et
al. 2020; Walker et al. 2023), entao vale medir mesmo nao sendo o "modelo
baseline" literal pedido na Fase 5.

SPLIT: agrupado por PESSOA (coluna player_id do build_dataset.py --player-map;
sem o mapa, player_id = char_id), nao aleatorio por linha -- acoes do MESMO
jogador sao correlacionadas no tempo, um split aleatorio por linha vazaria
informacao do treino pro teste (mesmo problema classico do toho-like-js, so
que agora agrupando por jogador em vez de por sessao). Agrupar por char_id
nao basta: a mesma pessoa joga com mais de um personagem.

Uso:
    python train_model.py --dataset dataset.csv
"""
import argparse
import json

import joblib
import numpy as np
import pandas as pd
from sklearn.dummy import DummyClassifier
from sklearn.ensemble import RandomForestClassifier, RandomForestRegressor
from sklearn.metrics import (accuracy_score, average_precision_score, classification_report, f1_score,
                             mean_absolute_error, precision_recall_curve, precision_score, recall_score)
from sklearn.model_selection import GroupShuffleSplit, LeaveOneGroupOut
from sklearn.neural_network import MLPClassifier
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import FunctionTransformer, LabelEncoder, StandardScaler

try:
    from xgboost import XGBClassifier
    HAS_XGB = True
except ImportError:
    HAS_XGB = False

# tudo numerico de proposito (ver Fase 8): prev_action_type virou
# prev_action_was_attack (0/1) no build_dataset.py, entao o modelo inteiro
# usa 1 unico tensor float -- sem coluna categorica, sem OneHotEncoder, sem
# StringTensorType no ONNX depois. Mais simples de treinar E de consumir
# em C++ no cliente.
# SO features que o CLIENTE consegue observar de um jogador REMOTO. hp, max_hp,
# hp_ratio e dead ficaram de fora: o servidor nao manda o HP de outros
# jogadores (medido: hp=0 maxhp=0 em 100% das leituras de um jogador remoto),
# entao usa-las seria training-serving skew. Ordem == beingactionpredictor.cpp.
ALL_FEATURES = [
    'x', 'y', 'velocity_x', 'velocity_y', 'speed',
    'time_since_last_action', 'time_since_last_hurt', 'last_damage_taken',
    'prev_action_was_attack',
]


def load_dataset(path):
    df = pd.read_csv(path)
    df['dead'] = df['dead'].astype(int)
    # grupo do split/LOGO: pessoa, nao personagem (datasets antigos sem a coluna caem no char_id)
    df['player_id'] = (df['player_id'] if 'player_id' in df else df['char_id']).astype(str)
    return df


def make_preprocessor():
    return FunctionTransformer(validate=False)  # identidade -- skl2onnx nao aceita a string 'passthrough' como step de Pipeline


def make_preprocessor_scaled():
    # MLP e sensivel a escala das features (diferente de arvores, que nao
    # ligam pra isso).
    return StandardScaler()


def group_split(df, group_col, test_size, seed):
    gss = GroupShuffleSplit(n_splits=1, test_size=test_size, random_state=seed)
    train_idx, test_idx = next(gss.split(df, groups=df[group_col]))
    return df.iloc[train_idx].reset_index(drop=True), df.iloc[test_idx].reset_index(drop=True)


# ---------------------------------------------------------------------------
# Tarefa 1 (a pedida na Fase 5): classificar o TIPO da proxima acao
# ---------------------------------------------------------------------------
def evaluate_classifier(name, model, X_test, y_test, results, target_names=None):
    preds = model.predict(X_test)
    acc = accuracy_score(y_test, preds)
    f1 = f1_score(y_test, preds, average='macro')
    results[name] = {'accuracy': acc, 'f1_macro': f1}
    print(f'\n--- {name} ---')
    print(f'accuracy: {acc:.3f} | f1_macro: {f1:.3f}')
    print(classification_report(y_test, preds, target_names=target_names, zero_division=0))


def run_action_type_task(df, test_size, seed):
    print('\n' + '=' * 70)
    print('TAREFA 1: classificar o tipo da proxima acao (move vs attack)')
    print('=' * 70)

    train_df, test_df = group_split(df, 'player_id', test_size, seed)
    X_train, y_train = train_df[ALL_FEATURES], train_df['action_type']
    X_test, y_test = test_df[ALL_FEATURES], test_df['action_type']
    print(f'treino: {len(train_df)} linhas ({train_df.player_id.nunique()} jogadores) | '
          f'teste: {len(test_df)} linhas ({test_df.player_id.nunique()} jogadores)')
    print(f'distribuicao no teste: {dict(y_test.value_counts())}')

    results = {}

    dummy_random = DummyClassifier(strategy='uniform', random_state=seed)
    dummy_random.fit(X_train, y_train)
    evaluate_classifier('baseline_aleatorio_uniforme', dummy_random, X_test, y_test, results)

    dummy_majority = DummyClassifier(strategy='most_frequent')
    dummy_majority.fit(X_train, y_train)
    evaluate_classifier('baseline_classe_majoritaria', dummy_majority, X_test, y_test, results)

    rf = Pipeline([
        ('prep', make_preprocessor()),
        ('clf', RandomForestClassifier(n_estimators=300, random_state=seed, class_weight='balanced')),
    ])
    rf.fit(X_train, y_train)
    evaluate_classifier('random_forest', rf, X_test, y_test, results)

    importances = rf.named_steps['clf'].feature_importances_
    top = sorted(zip(ALL_FEATURES, importances), key=lambda p: -p[1])[:8]
    print('\ntop features (random_forest):')
    for name, imp in top:
        print(f'  {name}: {imp:.3f}')

    # MLPClassifier com early_stopping quebra com labels string nesta
    # versao do sklearn (bug real que apareceu no teste -- np.isnan falha
    # na validacao interna quando y e string) -- LabelEncoder resolve.
    label_enc = LabelEncoder()
    y_train_le = label_enc.fit_transform(y_train)
    y_test_le = label_enc.transform(y_test)
    mlp = Pipeline([
        ('prep', make_preprocessor_scaled()),
        ('clf', MLPClassifier(hidden_layer_sizes=(64, 32), max_iter=500, random_state=seed,
                               early_stopping=True, n_iter_no_change=15)),
    ])
    mlp.fit(X_train, y_train_le)
    evaluate_classifier('mlp', mlp, X_test, y_test_le, results, target_names=label_enc.classes_)

    if HAS_XGB:
        le = LabelEncoder()
        y_train_enc = le.fit_transform(y_train)
        y_test_enc = le.transform(y_test)
        xgb = Pipeline([
            ('prep', make_preprocessor()),
            ('clf', XGBClassifier(n_estimators=300, random_state=seed, eval_metric='logloss')),
        ])
        xgb.fit(X_train, y_train_enc)
        evaluate_classifier('xgboost', xgb, X_test, y_test_enc, results)
    else:
        print('\n(xgboost nao instalado -- pulei essa comparacao; pip install xgboost pra incluir)')

    return results, rf


# ---------------------------------------------------------------------------
# Tarefa 1b: ataque como classe positiva, leave-one-player-out
# ---------------------------------------------------------------------------
# Com poucos jogadores (3 na coleta oficial de 2026-10-20) o GroupShuffleSplit
# deixa 1 jogador no teste e o resultado depende de QUEM caiu la. Aqui cada
# jogador vira teste uma vez. Ataque e raro (~8% das acoes), entao accuracy
# engana (classe majoritaria ja passa de 90%): a metrica principal e PR-AUC
# (average precision) de ataque, cujo baseline aleatorio e a prevalencia.
def make_attack_rf(seed):
    # labels string de proposito: classes_ = ['attack', 'move'], entao a
    # coluna 0 de "probabilities" no ONNX e P(attack) -- e o que
    # beingactionpredictor.cpp le (probs[0])
    return Pipeline([
        ('prep', make_preprocessor()),
        ('clf', RandomForestClassifier(n_estimators=300, random_state=seed, class_weight='balanced')),
    ])


def attack_proba(model, X):
    classes = list(model.classes_)
    return model.predict_proba(X)[:, classes.index('attack') if 'attack' in classes else classes.index(1)]


def pick_threshold(is_attack, proba, target_recall):
    """Maior limiar cujo recall de ataque ainda e >= target_recall (= a melhor
    precisao possivel mantendo esse recall)."""
    _, recall, thresholds = precision_recall_curve(is_attack, proba)
    ok = recall[:-1] >= target_recall
    return float(thresholds[ok].max()) if ok.any() else float(thresholds.min())


def oof_attack_proba(df, idx, seed):
    """P(attack) out-of-fold (leave-one-player-out) so dentro das linhas idx --
    usado pra escolher o limiar sem olhar o jogador de teste."""
    sub = df.iloc[idx]
    proba = np.zeros(len(sub))
    for tr, te in LeaveOneGroupOut().split(sub, groups=sub['player_id']):
        model = make_attack_rf(seed).fit(sub[ALL_FEATURES].iloc[tr], sub['action_type'].iloc[tr])
        proba[te] = attack_proba(model, sub[ALL_FEATURES].iloc[te])
    return proba


def scores(is_attack, proba, threshold):
    pred = (proba >= threshold).astype(int)
    return {
        'pr_auc': float(average_precision_score(is_attack, proba)),
        'precision': float(precision_score(is_attack, pred, zero_division=0)),
        'recall': float(recall_score(is_attack, pred, zero_division=0)),
    }


def run_attack_logo(df, seed, target_recall):
    print('\n' + '=' * 70)
    print('TAREFA 1b: ataque vs. resto, leave-one-player-out (PR-AUC, recall)')
    print('=' * 70)

    X, y, groups = df[ALL_FEATURES], df['action_type'], df['player_id']
    is_attack = (y == 'attack').astype(int)
    if groups.nunique() < 2:
        print('menos de 2 jogadores -- pulei')
        return {}, None

    folds = []
    oof_rf = np.zeros(len(df))
    for train_idx, test_idx in LeaveOneGroupOut().split(X, y, groups):
        y_test = is_attack.iloc[test_idx]
        fold = {'jogador_teste': groups.iloc[test_idx[0]], 'n_teste': len(test_idx),
                'prevalencia_ataque': float(y_test.mean())}
        # regra trivial: "repete a acao anterior" (ataques vem em sequencia)
        fold['baseline_repete_anterior'] = scores(y_test, df['prev_action_was_attack'].iloc[test_idx], 0.5)

        rf = make_attack_rf(seed).fit(X.iloc[train_idx], y.iloc[train_idx])
        proba = attack_proba(rf, X.iloc[test_idx])
        oof_rf[test_idx] = proba
        fold['random_forest'] = scores(y_test, proba, 0.5)
        # limiar escolhido SO com os jogadores de treino (OOF entre eles),
        # depois aplicado no jogador de teste -- sem vazamento
        if groups.iloc[train_idx].nunique() >= 2:
            thr = pick_threshold(is_attack.iloc[train_idx], oof_attack_proba(df, train_idx, seed), target_recall)
        else:
            thr = 0.5
        fold['limiar_rf_calibrado'] = thr
        fold['random_forest_calibrado'] = scores(y_test, proba, thr)

        # MLPClassifier com early_stopping quebra com labels string (ver Tarefa 1) -- y binario aqui
        mlp = Pipeline([
            ('prep', make_preprocessor_scaled()),
            ('clf', MLPClassifier(hidden_layer_sizes=(64, 32), max_iter=500, random_state=seed,
                                   early_stopping=True, n_iter_no_change=15)),
        ]).fit(X.iloc[train_idx], is_attack.iloc[train_idx])
        fold['mlp'] = scores(y_test, attack_proba(mlp, X.iloc[test_idx]), 0.5)
        folds.append(fold)

    names = ['baseline_repete_anterior', 'random_forest', 'random_forest_calibrado', 'mlp']
    print(f'\n{"teste (pessoa)":>16} {"n":>6} {"prev.":>6} | ' +
          ' | '.join(f'{n[:22]:>22}' for n in names))
    print(f'{"":>16} {"":>6} {"":>6} | ' + ' | '.join(f'{"PR-AUC  prec  rec":>22}' for _ in names))
    for f in folds:
        print(f'{f["jogador_teste"]:>16} {f["n_teste"]:>6} {f["prevalencia_ataque"]:>6.3f} | ' +
              ' | '.join(f'{f[n]["pr_auc"]:>10.3f} {f[n]["precision"]:>5.2f} {f[n]["recall"]:>5.2f}' for n in names))
    summary = {n: {m: float(np.mean([f[n][m] for f in folds])) for m in ('pr_auc', 'precision', 'recall')}
               for n in names}
    print(f'{"media":>16} {"":>6} {np.mean([f["prevalencia_ataque"] for f in folds]):>6.3f} | ' +
          ' | '.join(f'{summary[n]["pr_auc"]:>10.3f} {summary[n]["precision"]:>5.2f} {summary[n]["recall"]:>5.2f}'
                     for n in names))
    print('(PR-AUC de um classificador aleatorio = prevalencia; limiar 0.5 pra precision/recall, '
          f'exceto random_forest_calibrado: recall-alvo {target_recall} escolhido nos jogadores de treino)')
    print('limiares calibrados por fold: ' +
          ', '.join(f'{f["jogador_teste"]}: {f["limiar_rf_calibrado"]:.3f}' for f in folds))

    # modelo pro cliente: RF com TODOS os jogadores; limiar = recall-alvo
    # sobre as predicoes OOF de todos (cada jogador previsto por um modelo
    # que nao o viu)
    deploy_threshold = pick_threshold(is_attack, oof_rf, target_recall)
    deploy_scores = scores(is_attack, oof_rf, deploy_threshold)
    final_rf = make_attack_rf(seed).fit(X, y)
    print(f'\nlimiar pro cliente (recall-alvo {target_recall}, OOF de {groups.nunique()} jogadores): '
          f'{deploy_threshold:.3f} -> precision {deploy_scores["precision"]:.2f}, recall {deploy_scores["recall"]:.2f}')

    return {'folds': folds, 'media': summary, 'target_recall': target_recall,
            'limiar_cliente': deploy_threshold, 'oof_no_limiar_cliente': deploy_scores}, final_rf


# ---------------------------------------------------------------------------
# Tarefa 2 (extra, liga direto na Introducao do TCC): direcao do movimento
# vs. baseline de Dead Reckoning (extrapolar a velocidade atual)
# ---------------------------------------------------------------------------
def cosine_similarity(pred_dx, pred_dy, true_dx, true_dy):
    pred_norm = np.hypot(pred_dx, pred_dy)
    true_norm = np.hypot(true_dx, true_dy)
    valid = (pred_norm > 1e-6) & (true_norm > 1e-6)
    cos = np.zeros(len(pred_dx))
    cos[valid] = (pred_dx[valid] * true_dx[valid] + pred_dy[valid] * true_dy[valid]) / (
        pred_norm[valid] * true_norm[valid])
    return cos


def run_move_direction_task(df, test_size, seed):
    print('\n' + '=' * 70)
    print('TAREFA 2 (extra): direcao do movimento vs. baseline Dead Reckoning')
    print('=' * 70)

    move_df = df[df['action_type'] == 'move'].dropna(subset=['move_dx', 'move_dy']).reset_index(drop=True)
    train_df, test_df = group_split(move_df, 'player_id', test_size, seed)
    X_train, y_train = train_df[ALL_FEATURES], train_df[['move_dx', 'move_dy']]
    X_test, y_test = test_df[ALL_FEATURES], test_df[['move_dx', 'move_dy']]
    print(f'treino: {len(train_df)} | teste: {len(test_df)}')

    # --- baseline Dead Reckoning: assume que a proxima direcao de
    # movimento continua a velocidade atual (mesma logica que a tecnica
    # classica citada na Introducao) ---
    dr_dx, dr_dy = test_df['velocity_x'].to_numpy(), test_df['velocity_y'].to_numpy()
    dr_cos = cosine_similarity(dr_dx, dr_dy, y_test['move_dx'].to_numpy(), y_test['move_dy'].to_numpy())
    dr_mae = mean_absolute_error(y_test, np.column_stack([dr_dx, dr_dy]))
    print(f'\n--- baseline_dead_reckoning ---')
    print(f'similaridade de cosseno media (direcao): {dr_cos.mean():.3f} | MAE (dx,dy): {dr_mae:.1f}')

    # --- Random Forest Regressor multi-output ---
    rf = Pipeline([
        ('prep', make_preprocessor()),
        ('reg', RandomForestRegressor(n_estimators=300, random_state=seed)),
    ])
    rf.fit(X_train, y_train)
    preds = rf.predict(X_test)
    rf_cos = cosine_similarity(preds[:, 0], preds[:, 1], y_test['move_dx'].to_numpy(), y_test['move_dy'].to_numpy())
    rf_mae = mean_absolute_error(y_test, preds)
    print(f'\n--- random_forest_regressor ---')
    print(f'similaridade de cosseno media (direcao): {rf_cos.mean():.3f} | MAE (dx,dy): {rf_mae:.1f}')

    return {
        'baseline_dead_reckoning': {'cosine_mean': float(dr_cos.mean()), 'mae': float(dr_mae)},
        'random_forest_regressor': {'cosine_mean': float(rf_cos.mean()), 'mae': float(rf_mae)},
    }


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--dataset', default='dataset.csv')
    ap.add_argument('--test-size', type=float, default=0.25)
    ap.add_argument('--seed', type=int, default=42)
    ap.add_argument('--metrics-out', default='metrics.json')
    ap.add_argument('--model-out', default='model.pkl',
                     help='onde salvar o pipeline Random Forest pro cliente -- usado pela Fase 7 (export_onnx.py). '
                          'Com 2+ jogadores e a RF da Tarefa 1b treinada com TODOS; senao, a da Tarefa 1')
    ap.add_argument('--target-recall', type=float, default=0.7,
                    help='recall de ataque desejado ao escolher o limiar do telegraph (Tarefa 1b)')
    args = ap.parse_args()

    df = load_dataset(args.dataset)
    print(f'dataset: {len(df)} linhas, {df.char_id.nunique()} personagens, {df.player_id.nunique()} pessoas, '
          f'distribuicao geral: {dict(df.action_type.value_counts())}')

    action_results, rf_clf = run_action_type_task(df, args.test_size, args.seed)
    attack_logo_results, final_rf = run_attack_logo(df, args.seed, args.target_recall)
    move_results = run_move_direction_task(df, args.test_size, args.seed)

    all_metrics = {'action_type_classification': action_results, 'attack_logo': attack_logo_results,
                   'move_direction': move_results}
    with open(args.metrics_out, 'w') as f:
        json.dump(all_metrics, f, indent=2)
    print(f'\nMetricas salvas em: {args.metrics_out}')

    if final_rf is not None:
        rf_clf = final_rf
        print(f'limiar do telegraph pro cliente: {attack_logo_results["limiar_cliente"]:.3f} '
              '(metrics.json -> attack_logo.limiar_cliente; TCC_PREDICTOR_THRESHOLD no ManaVerse)')
    joblib.dump(rf_clf, args.model_out)
    print(f'Modelo (Random Forest, {"Tarefa 1b, todos os jogadores" if final_rf is not None else "Tarefa 1"}) '
          f'salvo em: {args.model_out}')


if __name__ == '__main__':
    main()
