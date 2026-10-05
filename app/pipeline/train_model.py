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

SPLIT: agrupado por char_id (GroupShuffleSplit), nao aleatorio por linha --
acoes do MESMO jogador sao correlacionadas no tempo, um split aleatorio por
linha vazaria informacao do treino pro teste (mesmo problema classico do
toho-like-js, so que agora agrupando por jogador em vez de por sessao).

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
from sklearn.metrics import accuracy_score, classification_report, f1_score, mean_absolute_error
from sklearn.model_selection import GroupShuffleSplit
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
ALL_FEATURES = [
    'x', 'y', 'hp', 'max_hp', 'hp_ratio', 'velocity_x', 'velocity_y', 'speed',
    'time_since_last_action', 'time_since_last_hurt', 'last_damage_taken', 'dead',
    'prev_action_was_attack',
]


def load_dataset(path):
    df = pd.read_csv(path)
    df['dead'] = df['dead'].astype(int)
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

    train_df, test_df = group_split(df, 'char_id', test_size, seed)
    X_train, y_train = train_df[ALL_FEATURES], train_df['action_type']
    X_test, y_test = test_df[ALL_FEATURES], test_df['action_type']
    print(f'treino: {len(train_df)} linhas ({train_df.char_id.nunique()} jogadores) | '
          f'teste: {len(test_df)} linhas ({test_df.char_id.nunique()} jogadores)')
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
    train_df, test_df = group_split(move_df, 'char_id', test_size, seed)
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
                     help='onde salvar o pipeline Random Forest treinado (Tarefa 1) -- usado pela Fase 7 (export_onnx.py)')
    args = ap.parse_args()

    df = load_dataset(args.dataset)
    print(f'dataset: {len(df)} linhas, {df.char_id.nunique()} jogadores, '
          f'distribuicao geral: {dict(df.action_type.value_counts())}')

    action_results, rf_clf = run_action_type_task(df, args.test_size, args.seed)
    move_results = run_move_direction_task(df, args.test_size, args.seed)

    all_metrics = {'action_type_classification': action_results, 'move_direction': move_results}
    with open(args.metrics_out, 'w') as f:
        json.dump(all_metrics, f, indent=2)
    print(f'\nMetricas salvas em: {args.metrics_out}')

    joblib.dump(rf_clf, args.model_out)
    print(f'Modelo (Random Forest, Tarefa 1) salvo em: {args.model_out}')


if __name__ == '__main__':
    main()
