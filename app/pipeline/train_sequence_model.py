"""
train_sequence_model.py
---------------------------------------------------------------------------
Fase 6 (parte 2): testa se dar ao modelo a SEQUENCIA das ultimas K acoes
(em vez de so o estado atual + features de historico resumidas a mao, como
em train_model.py) melhora a previsao do tipo da proxima acao. Usa LSTM e
GRU (PyTorch), a mesma tarefa de classificacao (move vs attack) do Task 1
de train_model.py, pra comparar diretamente.

DIFERENCA DE FRAMING vs. train_model.py
La, cada linha e independente: X = estado no instante da acao + um punhado
de features resumindo o historico (time_since_last_action, velocity etc).
Aqui, X = a sequencia bruta das ultimas SEQ_LEN acoes (cada uma com seu
proprio estado+tipo), e o modelo tem que aprender sozinho quais padroes
temporais importam -- sem esses resumos prontos. Se LSTM/GRU nao superar
claramente o Random Forest/MLP tabular, e evidencia de que as features
resumidas ja capturam o que importa (resultado legitimo e esperado -- nao
todo problema precisa de sequencia).

Uso:
    python train_sequence_model.py --dataset dataset.csv --seq-len 8
"""
import argparse

import numpy as np
import pandas as pd
import torch
import torch.nn as nn
from sklearn.metrics import accuracy_score, classification_report, f1_score
from sklearn.model_selection import GroupShuffleSplit
from sklearn.preprocessing import StandardScaler

NUM_FEATURES = [
    'x', 'y', 'hp', 'max_hp', 'hp_ratio', 'velocity_x', 'velocity_y', 'speed',
    'time_since_last_action', 'time_since_last_hurt', 'last_damage_taken', 'dead',
]


def build_sequences(df, seq_len):
    """Por jogador, janelas deslizantes de seq_len acoes consecutivas ->
    prevendo o tipo da acao SEGUINTE a janela."""
    feat_cols = NUM_FEATURES  # action_type de cada passo entra como coluna extra (is_attack) abaixo
    sequences, labels, groups = [], [], []

    for char_id, group in df.groupby('char_id', sort=False):
        group = group.sort_values('t').reset_index(drop=True)
        feats = group[feat_cols].to_numpy(dtype=np.float32)
        is_attack = (group['action_type'] == 'attack').to_numpy(dtype=np.float32).reshape(-1, 1)
        feats = np.concatenate([feats, is_attack], axis=1)  # cada passo "sabe" seu proprio tipo
        y = (group['action_type'] == 'attack').to_numpy(dtype=np.int64)

        for i in range(seq_len, len(group)):
            sequences.append(feats[i - seq_len:i])
            labels.append(y[i])
            groups.append(char_id)

    return np.stack(sequences), np.array(labels), np.array(groups)


class SequenceModel(nn.Module):
    def __init__(self, n_features, hidden_size=32, cell='lstm'):
        super().__init__()
        rnn_cls = nn.LSTM if cell == 'lstm' else nn.GRU
        self.rnn = rnn_cls(input_size=n_features, hidden_size=hidden_size, batch_first=True)
        self.head = nn.Linear(hidden_size, 1)

    def forward(self, x):
        _out, last_state = self.rnn(x)
        h = last_state[0] if isinstance(last_state, tuple) else last_state  # LSTM: (h,c); GRU: h
        h = h[-1]  # ultima camada
        return self.head(h).squeeze(-1)


def train_one(cell, X_train, y_train, X_test, y_test, n_features, epochs, lr, seed):
    torch.manual_seed(seed)
    model = SequenceModel(n_features, hidden_size=32, cell=cell)
    opt = torch.optim.Adam(model.parameters(), lr=lr)
    loss_fn = nn.BCEWithLogitsLoss()

    X_train_t = torch.from_numpy(X_train)
    y_train_t = torch.from_numpy(y_train).float()
    X_test_t = torch.from_numpy(X_test)

    model.train()
    for epoch in range(epochs):
        opt.zero_grad()
        logits = model(X_train_t)
        loss = loss_fn(logits, y_train_t)
        loss.backward()
        opt.step()
        if (epoch + 1) % max(1, epochs // 5) == 0:
            print(f'  [{cell}] epoch {epoch + 1}/{epochs} loss={loss.item():.4f}')

    model.eval()
    with torch.no_grad():
        preds = (torch.sigmoid(model(X_test_t)) > 0.5).numpy().astype(int)

    acc = accuracy_score(y_test, preds)
    f1 = f1_score(y_test, preds, average='macro')
    print(f'\n--- {cell} (seq_len={X_train.shape[1]}) ---')
    print(f'accuracy: {acc:.3f} | f1_macro: {f1:.3f}')
    print(classification_report(y_test, preds, target_names=['move', 'attack'], zero_division=0))
    return {'accuracy': acc, 'f1_macro': f1}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--dataset', default='dataset.csv')
    ap.add_argument('--seq-len', type=int, default=8)
    ap.add_argument('--epochs', type=int, default=60)
    ap.add_argument('--lr', type=float, default=1e-3)
    ap.add_argument('--test-size', type=float, default=0.25)
    ap.add_argument('--seed', type=int, default=42)
    args = ap.parse_args()

    df = pd.read_csv(args.dataset)
    df['dead'] = df['dead'].astype(int)

    X, y, groups = build_sequences(df, args.seq_len)
    print(f'{len(X)} sequencias construidas (seq_len={args.seq_len}), '
          f'{len(np.unique(groups))} jogadores com historico suficiente.')
    print(f'distribuicao: move={int((y == 0).sum())} attack={int((y == 1).sum())}')

    gss = GroupShuffleSplit(n_splits=1, test_size=args.test_size, random_state=args.seed)
    train_idx, test_idx = next(gss.split(X, y, groups))
    X_train, X_test = X[train_idx], X[test_idx]
    y_train, y_test = y[train_idx], y[test_idx]
    print(f'treino: {len(X_train)} | teste: {len(X_test)}')

    # normaliza (ajustado so no treino, aplicado nos dois) -- achata pra
    # (N*seq_len, n_features), normaliza, volta pro shape original
    n_features = X_train.shape[2]
    scaler = StandardScaler()
    X_train_flat = scaler.fit_transform(X_train.reshape(-1, n_features))
    X_test_flat = scaler.transform(X_test.reshape(-1, n_features))
    X_train = X_train_flat.reshape(X_train.shape).astype(np.float32)
    X_test = X_test_flat.reshape(X_test.shape).astype(np.float32)

    print('\n' + '=' * 70)
    print('LSTM vs GRU -- previsao do tipo da proxima acao a partir da sequencia')
    print('=' * 70)
    results = {}
    for cell in ('lstm', 'gru'):
        results[cell] = train_one(cell, X_train, y_train, X_test, y_test, n_features,
                                   args.epochs, args.lr, args.seed)

    print('\n' + '=' * 70)
    print('COMPARACAO (veja tambem a saida de train_model.py pra RF/MLP/XGBoost)')
    print('=' * 70)
    for name, r in results.items():
        print(f'  {name}: accuracy={r["accuracy"]:.3f} f1_macro={r["f1_macro"]:.3f}')
    print('  (compare com baseline_aleatorio/random_forest/mlp impressos por train_model.py)')


if __name__ == '__main__':
    main()
