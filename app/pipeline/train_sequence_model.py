"""
train_sequence_model.py
---------------------------------------------------------------------------
Fase 6 (parte 2): testa se dar ao modelo a SEQUENCIA das ultimas K acoes do
jogador melhora a previsao de ataque em relacao a so o estado atual (as 9
features do train_model.py). LSTM e GRU (PyTorch), no MESMO protocolo do
compare_models.py: ataque como classe positiva, leave-one-player-out por
PESSOA, varias sementes, P(ataque) out-of-fold de cada linha do dataset salva
pro statistical_analysis.py (oof_sequencia.csv.gz).

ENTRADA DE CADA LINHA
- historico: as K acoes anteriores do MESMO personagem na MESMA sessao. Cada
  passo = as 9 features daquela acao + se ela foi ataque + 1 (passo valido).
  Antes do inicio do bloco, os passos ficam zerados com valido=0;
- estado atual: as 9 features da propria linha (o mesmo que a RF ve).
O RNN resume o historico num vetor h e a cabeca (MLP) recebe [h, estado atual].
Tudo isso o cliente observa: ele ve as acoes passadas do jogador remoto, e
nada da acao ATUAL entra no historico.

CONTROLE: "rede_sem_historico" e a MESMA cabeca, com o MESMO pre-processamento
e o MESMO treino, so que sem o historico. A diferenca GRU/LSTM menos o controle
mede o efeito da sequencia, separado de otimizador, pre-processamento etc.

TREINO
Features com log1p nas caudas longas (compare_models.log_features) e
padronizadas com media/dp dos jogadores de treino. Adam, mini-batches, BCE com
pos_weight = n_move/n_ataque (o equivalente ao class_weight='balanced' da RF).
Early stopping pelo PR-AUC numa validacao = ultimos 20% (no tempo) de cada
bloco dos jogadores de TREINO. O jogador de teste nao e usado nem no treino nem
na escolha da epoca.

(A versao anterior deste script era da epoca dos dados sinteticos: usava hp e
dead, que o cliente nao observa, juntava sessoes diferentes do mesmo char_id e
avaliava com um unico sorteio e com acuracia.)

Uso:
    python train_sequence_model.py --dataset dataset.csv --out-dir resultados/ [--seq-len 16] [--seeds 5] \
        [--k-sweep 4 8 16 32]
"""
import argparse
import json
import os
import time

import numpy as np
import pandas as pd
import torch
import torch.nn as nn
from sklearn.metrics import average_precision_score
from sklearn.model_selection import LeaveOneGroupOut
from sklearn.preprocessing import StandardScaler

from compare_models import BLOCK, check_block_order, log_features
from train_model import ALL_FEATURES, load_dataset

CELLS = ['rede_sem_historico', 'lstm', 'gru']


def build_history(F, is_attack, pos_in_block, seq_len):
    """(n, seq_len, d+2): passo -1 = acao imediatamente anterior. Linhas do
    mesmo bloco sao contiguas e em ordem de tempo (check_block_order)."""
    n, d = F.shape
    hist = np.zeros((n, seq_len, d + 2), dtype=np.float32)
    for o in range(1, seq_len + 1):
        valid = pos_in_block >= o
        src = np.arange(n)[valid] - o
        hist[valid, seq_len - o, :d] = F[src]
        hist[valid, seq_len - o, d] = is_attack[src]
        hist[valid, seq_len - o, d + 1] = 1.0
    return hist


class AttackNet(nn.Module):
    def __init__(self, n_features, cell, hidden=64):
        super().__init__()
        self.cell = cell
        head_in = n_features
        if cell != 'rede_sem_historico':
            rnn_cls = nn.LSTM if cell == 'lstm' else nn.GRU
            self.rnn = rnn_cls(input_size=n_features + 2, hidden_size=hidden, batch_first=True)
            head_in += hidden
        self.head = nn.Sequential(nn.Linear(head_in, 64), nn.ReLU(), nn.Dropout(0.1), nn.Linear(64, 1))

    def forward(self, hist, cur):
        if self.cell != 'rede_sem_historico':
            _out, state = self.rnn(hist)
            h = state[0] if isinstance(state, tuple) else state  # LSTM: (h, c); GRU: h
            cur = torch.cat([h[-1], cur], dim=1)
        return self.head(cur).squeeze(-1)


def predict(model, hist, cur, idx, batch_size=4096):
    model.eval()
    out = []
    with torch.no_grad():
        for b in range(0, len(idx), batch_size):
            i = torch.from_numpy(idx[b:b + batch_size])
            out.append(torch.sigmoid(model(hist[i], cur[i])).numpy())
    return np.concatenate(out)


def fit_predict(cell, hist, cur, y, tr_idx, va_idx, te_idx, seed, args):
    torch.manual_seed(seed)
    rng = np.random.default_rng(seed)
    model = AttackNet(cur.shape[1], cell, hidden=args.hidden)
    opt = torch.optim.Adam(model.parameters(), lr=args.lr, weight_decay=1e-5)
    n_pos = y[tr_idx].sum()
    loss_fn = nn.BCEWithLogitsLoss(pos_weight=torch.tensor((len(tr_idx) - n_pos) / max(n_pos, 1), dtype=torch.float32))
    y_t = torch.from_numpy(y.astype(np.float32))

    best_ap, best_state, best_epoch, bad = -1.0, None, 0, 0
    for epoch in range(1, args.max_epochs + 1):
        model.train()
        perm = rng.permutation(tr_idx)
        for b in range(0, len(perm), args.batch_size):
            i = torch.from_numpy(perm[b:b + args.batch_size])
            opt.zero_grad()
            loss = loss_fn(model(hist[i], cur[i]), y_t[i])
            loss.backward()
            opt.step()
        ap = average_precision_score(y[va_idx], predict(model, hist, cur, va_idx))
        if ap > best_ap + 1e-4:
            best_ap, best_epoch, bad = ap, epoch, 0
            best_state = {k: v.clone() for k, v in model.state_dict().items()}
        else:
            bad += 1
            if bad >= args.patience:
                break
    model.load_state_dict(best_state)
    return predict(model, hist, cur, te_idx), best_epoch, best_ap


def run(df, cells, seeds, seq_len, args, log):
    y = (df['action_type'] == 'attack').to_numpy(dtype=np.int64)
    feats = log_features(df[ALL_FEATURES])
    block = df.groupby(BLOCK, sort=False)
    pos = block.cumcount().to_numpy()
    size = block['t'].transform('size').to_numpy()
    is_val_tail = pos >= np.floor(size * 0.8)  # ultimos 20% de cada bloco (validacao, so nos de treino)

    preds = {f'{c}@{s}': np.full(len(df), np.nan) for c in cells for s in seeds}
    runs = []
    for train_idx, test_idx in LeaveOneGroupOut().split(df, groups=df['player_id']):
        person = df['player_id'].iloc[test_idx[0]]
        tr_idx = train_idx[~is_val_tail[train_idx]]
        va_idx = train_idx[is_val_tail[train_idx]]
        scaler = StandardScaler().fit(feats.iloc[train_idx])
        F = scaler.transform(feats).astype(np.float32)
        hist = torch.from_numpy(build_history(F, y.astype(np.float32), pos, seq_len))
        cur = torch.from_numpy(F)
        for c in cells:
            for s in seeds:
                t0 = time.time()
                p, epoch, val_ap = fit_predict(c, hist, cur, y, tr_idx, va_idx, test_idx, s, args)
                preds[f'{c}@{s}'][test_idx] = p
                ap = average_precision_score(y[test_idx], p)
                runs.append({'jogador_teste': person, 'modelo': c, 'semente': s, 'seq_len': seq_len,
                             'melhor_epoca': epoch, 'pr_auc_validacao': float(val_ap), 'pr_auc_teste': float(ap),
                             'tempo_s': round(time.time() - t0, 1)})
                log(f'  K={seq_len} {person} {c}@{s}: PR-AUC teste {ap:.3f} (val {val_ap:.3f}, '
                    f'epoca {epoch}, {time.time() - t0:.0f}s)')
    return pd.DataFrame(preds), pd.DataFrame(runs)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--dataset', default='dataset.csv')
    ap.add_argument('--out-dir', default='resultados')
    ap.add_argument('--seq-len', type=int, default=16)
    ap.add_argument('--seeds', type=int, default=5)
    ap.add_argument('--base-seed', type=int, default=42)
    ap.add_argument('--hidden', type=int, default=64)
    ap.add_argument('--batch-size', type=int, default=512)
    ap.add_argument('--lr', type=float, default=1e-3)
    ap.add_argument('--max-epochs', type=int, default=60)
    ap.add_argument('--patience', type=int, default=8)
    ap.add_argument('--k-sweep', type=int, nargs='*', default=[],
                    help='tamanhos de janela extras pra LSTM/GRU (1 semente cada); ex: 4 8 32')
    ap.add_argument('--threads', type=int, default=os.cpu_count())
    args = ap.parse_args()

    torch.set_num_threads(args.threads)
    os.makedirs(args.out_dir, exist_ok=True)
    log_file = open(os.path.join(args.out_dir, 'train_sequence_model.log'), 'a')

    def log(msg):
        print(msg, flush=True)
        log_file.write(msg + '\n')
        log_file.flush()

    df = load_dataset(args.dataset)
    check_block_order(df)
    seeds = [args.base_seed + i for i in range(args.seeds)]
    log(f'dataset: {len(df)} linhas, {df.player_id.nunique()} pessoas | K={args.seq_len} | sementes {seeds}')

    t0 = time.time()
    preds, runs = run(df, CELLS, seeds, args.seq_len, args, log)
    preds.insert(0, 'row', np.arange(len(df)))
    preds.to_csv(os.path.join(args.out_dir, 'oof_sequencia.csv.gz'), index=False)
    runs.to_csv(os.path.join(args.out_dir, 'sequence_runs.csv'), index=False)
    log(f'LOPO sequencia ok em {time.time() - t0:.0f}s')

    if args.k_sweep:
        sweep = []
        for k in args.k_sweep:
            _, r = run(df, ['lstm', 'gru'], seeds[:1], k, args, log)
            sweep.append(r)
        pd.concat(sweep).to_csv(os.path.join(args.out_dir, 'k_sweep.csv'), index=False)
        log(f'varredura de K ok em {time.time() - t0:.0f}s')

    with open(os.path.join(args.out_dir, 'sequence_config.json'), 'w') as f:
        json.dump(vars(args) | {'sementes': seeds}, f, indent=2)


if __name__ == '__main__':
    main()
