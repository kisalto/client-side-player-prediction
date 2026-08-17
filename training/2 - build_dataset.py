"""
build_dataset.py
---------------------------------------------------------------------------
Le os .json gravados pelo telemetry_logger.js (ou pelo generate_synthetic_data.py)
e monta um dataset tabular rotulado: "esse frame esta a <= N segundos de
uma morte?" pronto pra treinar com scikit-learn.

CONCEITO DO ROTULO
Para cada frame no instante t, olhamos pra frente na MESMA sessao: se
existe uma morte em algum instante d com t <= d <= t + horizon_ms, o
rotulo e 1. Usamos o timestamp real (campo "t", em ms via performance.now)
em vez de contar frames, porque o jogo roda via requestAnimationFrame e a
framerate real pode nao ser exatamente 60fps.

DECISOES IMPORTANTES (leia antes de mudar o script)
1. Frames com unhit=1 (invencibilidade pos-morte) SAO MANTIDOS no dataset,
   com "unhit" como feature. Descartar esses frames pareceria "limpar"
   dados triviais, mas na verdade pioraria o modelo: se algum dia voce for
   usar o modelo em tempo real (ex: um medidor de perigo no proprio jogo),
   ele VAI receber frames com unhit=1 tambem, entao precisa ter visto esse
   regime durante o treino. Arvores de decisao aprendem essa condicional
   ("se unhit=1, risco imediato ~0") sem problema.
2. Sessoes sao quebradas automaticamente quando ha um salto grande de
   tempo entre frames consecutivos (--gap-break-ms, default 1000ms) --
   indica que a aba ficou em segundo plano, nao uma morte de verdade
   (mortes nao geram esse tipo de salto, ja verificamos no codigo do jogo).
3. Os ultimos `horizon_ms` de cada sessao sao descartados SE o rotulo
   desse frame desse "0": nao da pra saber com certeza se uma morte
   aconteceria logo depois do fim da gravacao. Se o rotulo ja der "1"
   (achou uma morte dentro da janela disponivel), o frame e mantido mesmo
   perto do fim.
4. Balas "fantasmas" (quando existem menos que K balas por perto) recebem
   valores-sentinela bem seguros (distancia = diagonal do campo, ttca =
   horizonte), nunca NaN -- assim RandomForest/LogisticRegression funcionam
   sem tratamento especial de ausentes.

Uso:
    python build_dataset.py --raw-dir data/raw --out dataset.csv --horizon-ms 5000
"""
import argparse
import glob
import json
import math
import os

import numpy as np
import pandas as pd

DEFAULT_FIELD_W = 384.0
DEFAULT_FIELD_H = 448.0
EPS = 1e-6


# ---------------------------------------------------------------------------
# 1. Carregamento e segmentacao em subsessoes
# ---------------------------------------------------------------------------
def load_raw_session(path):
    with open(path) as fh:
        data = json.load(fh)
    frames = data['frames']
    deaths = set(data.get('deaths', []))
    meta = data.get('meta', {})
    field_w = meta.get('fieldWidth') or DEFAULT_FIELD_W
    field_h = meta.get('fieldHeight') or DEFAULT_FIELD_H
    k_nearest = meta.get('kNearest')
    return frames, deaths, field_w, field_h, k_nearest


def split_subsessions(frames, deaths, gap_break_ms):
    """Quebra a lista de frames em blocos continuos, cortando onde o
    timestamp real pula mais que gap_break_ms (pausa/aba em segundo plano)."""
    if not frames:
        return []
    blocks = []
    current = [frames[0]]
    for prev, cur in zip(frames, frames[1:]):
        if (cur['t'] - prev['t']) > gap_break_ms:
            blocks.append(current)
            current = []
        current.append(cur)
    blocks.append(current)

    out = []
    for block in blocks:
        f_ids = {fr['f'] for fr in block}
        block_deaths = {f for f in deaths if f in f_ids}
        out.append((block, block_deaths))
    return out


# ---------------------------------------------------------------------------
# 2. Reconstrucao das arrays de balas (com casamento de id p/ velocidade)
# ---------------------------------------------------------------------------
def build_bullet_arrays(sub_frames, k_nearest):
    """Sequencial (O(T*K)): para cada frame, casa balas por id com o frame
    anterior pra poder calcular velocidade por diferenca finita."""
    T = len(sub_frames)
    bx = np.zeros((T, k_nearest))
    by = np.zeros((T, k_nearest))
    bvx = np.zeros((T, k_nearest))
    bvy = np.zeros((T, k_nearest))
    bcw = np.zeros((T, k_nearest))
    bch = np.zeros((T, k_nearest))
    bvalid = np.zeros((T, k_nearest), dtype=bool)

    prev_positions = {}  # id -> (x, y)
    for i, fr in enumerate(sub_frames):
        dt_s = None
        if i > 0:
            dt_s = max((fr['t'] - sub_frames[i - 1]['t']) / 1000.0, 1e-4)

        cur_positions = {}
        for k, b in enumerate(fr['bullets'][:k_nearest]):
            bx[i, k] = b['x']
            by[i, k] = b['y']
            bcw[i, k] = b['cw']
            bch[i, k] = b['ch']
            bvalid[i, k] = True
            cur_positions[b['id']] = (b['x'], b['y'])

            prev = prev_positions.get(b['id'])
            if prev is not None and dt_s is not None:
                bvx[i, k] = (b['x'] - prev[0]) / dt_s
                bvy[i, k] = (b['y'] - prev[1]) / dt_s
            # se a bala e nova (acabou de entrar no top-K), velocidade fica 0:
            # isso e um "chute conservador" (assume parada), o que so faz o
            # ttca cair de volta pra distancia atual -- nunca gera um sinal
            # de perigo inventado.

        prev_positions = cur_positions

    return dict(bx=bx, by=by, bvx=bvx, bvy=bvy, bcw=bcw, bch=bch, bvalid=bvalid)


# ---------------------------------------------------------------------------
# 3. Engenharia de features (vetorizada em numpy)
# ---------------------------------------------------------------------------
def engineer_features(sub_frames, bullets, field_w, field_h, horizon_s):
    T = len(sub_frames)
    t_ms = np.array([fr['t'] for fr in sub_frames], dtype=float)
    px = np.array([fr['px'] for fr in sub_frames], dtype=float)
    py = np.array([fr['py'] for fr in sub_frames], dtype=float)
    pcw = np.array([fr['pcw'] for fr in sub_frames], dtype=float)
    pch = np.array([fr['pch'] for fr in sub_frames], dtype=float)
    slow = np.array([fr['slow'] for fr in sub_frames], dtype=float)
    unhit = np.array([fr['unhit'] for fr in sub_frames], dtype=float)
    lives = np.array([fr['lives'] for fr in sub_frames], dtype=float)
    bombs = np.array([fr.get('bombs', 0) for fr in sub_frames], dtype=float)
    graze_total = np.array([fr['grazeTotal'] for fr in sub_frames], dtype=float)
    bullet_count = np.array([fr['bulletCount'] for fr in sub_frames], dtype=float)

    dt_s = np.diff(t_ms) / 1000.0
    dt_s = np.clip(dt_s, 1e-4, None)
    pvx = np.concatenate([[0.0], np.diff(px) / dt_s])
    pvy = np.concatenate([[0.0], np.diff(py) / dt_s])

    field_diag = math.hypot(field_w, field_h)

    bx, by = bullets['bx'], bullets['by']
    bvx, bvy = bullets['bvx'], bullets['bvy']
    bcw, bch = bullets['bcw'], bullets['bch']
    bvalid = bullets['bvalid']

    now_dx = bx - px[:, None]
    now_dy = by - py[:, None]
    now_dist = np.hypot(now_dx, now_dy)

    rvx = bvx - pvx[:, None]
    rvy = bvy - pvy[:, None]
    denom = rvx ** 2 + rvy ** 2
    dot = now_dx * rvx + now_dy * rvy
    ttca = np.where(denom > EPS, -dot / np.maximum(denom, EPS), 0.0)
    ttca = np.clip(ttca, 0.0, horizon_s)

    closest_x = now_dx + rvx * ttca
    closest_y = now_dy + rvy * ttca
    closest_dist = np.hypot(closest_x, closest_y)
    combined_radius = 0.5 * np.hypot(pcw, pch)[:, None] + 0.5 * np.hypot(bcw, bch)
    predicted_gap = closest_dist - combined_radius

    # balas "fantasma" (slot vazio) viram sentinelas seguras, nunca NaN
    now_dist = np.where(bvalid, now_dist, field_diag)
    now_dx = np.where(bvalid, now_dx, field_diag)
    now_dy = np.where(bvalid, now_dy, field_diag)
    ttca = np.where(bvalid, ttca, horizon_s)
    predicted_gap = np.where(bvalid, predicted_gap, field_diag)

    collision_course = bvalid & (predicted_gap <= 0)
    ttca_if_course = np.where(collision_course, ttca, horizon_s)

    feats = {
        'pos_x_norm': px / field_w,
        'pos_y_norm': py / field_h,
        'dist_left_norm': px / field_w,
        'dist_right_norm': (field_w - px) / field_w,
        'dist_top_norm': py / field_h,
        'dist_bottom_norm': (field_h - py) / field_h,
        'player_speed': np.hypot(pvx, pvy),
        'player_vx': pvx,
        'player_vy': pvy,
        'is_slow': slow,
        'is_unhittable': unhit,
        'lives': lives,
        'bombs': bombs,
        'bullet_count_total': bullet_count,
        'bullet_count_within_60': (bvalid & (now_dist < 60)).sum(axis=1).astype(float),
        'bullet_count_within_120': (bvalid & (now_dist < 120)).sum(axis=1).astype(float),
        'min_now_dist': now_dist.min(axis=1),
        'mean_now_dist_topk': np.where(bvalid, now_dist, np.nan).mean(axis=1) if bvalid.any() else now_dist.mean(axis=1),
        'min_gap_at_ttca': predicted_gap.min(axis=1),
        'count_on_collision_course': collision_course.sum(axis=1).astype(float),
        'min_ttca_on_collision_course': ttca_if_course.min(axis=1),
        'time_since_start_s': (t_ms - t_ms[0]) / 1000.0,
    }
    # mean_now_dist_topk pode gerar NaN se algum frame nao tiver NENHUMA bala
    # valida (bvalid todo False); nesse caso e seguro (campo vazio de balas)
    feats['mean_now_dist_topk'] = np.nan_to_num(feats['mean_now_dist_topk'], nan=field_diag)

    k_nearest = bx.shape[1]
    for k in range(k_nearest):
        feats[f'slot{k}_dx'] = now_dx[:, k]
        feats[f'slot{k}_dy'] = now_dy[:, k]
        feats[f'slot{k}_ttca'] = ttca[:, k]
        feats[f'slot{k}_gap'] = predicted_gap[:, k]

    df = pd.DataFrame(feats)
    df['t_ms'] = t_ms
    df['frame_f'] = [fr['f'] for fr in sub_frames]
    # descarta a primeira linha: velocidade de jogador/bala ainda nao existe
    return df.iloc[1:].reset_index(drop=True)


# ---------------------------------------------------------------------------
# 4. Rotulagem (olhando para frente na mesma subsessao)
# ---------------------------------------------------------------------------
def add_labels(df, death_t_ms_sorted, horizon_ms):
    t_ms = df['t_ms'].to_numpy()
    last_t = t_ms[-1] if len(t_ms) else 0.0
    death_arr = np.asarray(death_t_ms_sorted, dtype=float)

    labels = np.zeros(len(df), dtype=int)
    has_future_death_in_view = np.zeros(len(df), dtype=bool)

    if death_arr.size > 0:
        # para cada frame, acha a menor morte futura (busca binaria)
        idx = np.searchsorted(death_arr, t_ms, side='left')
        idx = np.clip(idx, 0, death_arr.size - 1)
        # candidatos: a morte em idx (>= t) e a anterior podem ambas servir
        next_death = death_arr[idx]
        within = (next_death - t_ms >= 0) & (next_death - t_ms <= horizon_ms)
        labels = within.astype(int)
        has_future_death_in_view = within.copy()

    # nao da pra confiar no rotulo "0" se a sessao acabou antes do horizonte
    # inteiro ter sido observado a partir deste frame
    fully_observed = (last_t - t_ms) >= horizon_ms
    keep = fully_observed | (labels == 1) | has_future_death_in_view

    df = df.copy()
    df['label'] = labels
    return df[keep].reset_index(drop=True)


# ---------------------------------------------------------------------------
# 5. Pipeline principal
# ---------------------------------------------------------------------------
def process_file(path, horizon_ms, gap_break_ms, k_nearest_default):
    frames, deaths, field_w, field_h, k_nearest_file = load_raw_session(path)
    subsessions = split_subsessions(frames, deaths, gap_break_ms)

    session_stem = os.path.splitext(os.path.basename(path))[0]
    horizon_s = horizon_ms / 1000.0
    dfs = []
    for sub_idx, (sub_frames, sub_deaths) in enumerate(subsessions):
        if len(sub_frames) < 5:
            continue
        bullets = build_bullet_arrays(sub_frames, k_nearest_default)
        feats = engineer_features(sub_frames, bullets, field_w, field_h, horizon_s)

        f_to_t = {fr['f']: fr['t'] for fr in sub_frames}
        death_t_ms = sorted(f_to_t[f] for f in sub_deaths if f in f_to_t)
        labeled = add_labels(feats, death_t_ms, horizon_ms)
        if labeled.empty:
            continue
        labeled['session_id'] = f'{session_stem}__{sub_idx}'
        labeled['n_deaths_in_subsession'] = len(death_t_ms)
        dfs.append(labeled)

    return dfs


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--raw-dir', default='data/raw', help='pasta com os .json exportados')
    ap.add_argument('--out', default='dataset.csv', help='arquivo csv de saida')
    ap.add_argument('--horizon-ms', type=int, default=5000, help='janela de previsao em ms (default: 5000 = 5s)')
    ap.add_argument('--gap-break-ms', type=int, default=1000, help='salto de tempo que conta como quebra de sessao')
    ap.add_argument('--k-nearest', type=int, default=12, help='quantas balas por frame o logger gravou')
    args = ap.parse_args()

    paths = sorted(glob.glob(os.path.join(args.raw_dir, '*.json')))
    if not paths:
        raise SystemExit(f'nenhum .json encontrado em {args.raw_dir}/')

    all_dfs = []
    for p in paths:
        all_dfs.extend(process_file(p, args.horizon_ms, args.gap_break_ms, args.k_nearest))

    if not all_dfs:
        raise SystemExit('nenhuma subsessao valida (muito curta ou sem frames suficientes).')

    dataset = pd.concat(all_dfs, ignore_index=True)
    dataset.to_csv(args.out, index=False)

    n_sessions = dataset['session_id'].nunique()
    pos_rate = dataset['label'].mean()
    n_feature_cols = len([c for c in dataset.columns if c not in
                           ('session_id', 't_ms', 'frame_f', 'label', 'n_deaths_in_subsession')])

    print(f'{len(paths)} arquivo(s) processado(s) -> {len(dataset)} linhas em {n_sessions} subsessoes.')
    print(f'Taxa de positivos (label=1, "vai morrer em <= {args.horizon_ms}ms"): {pos_rate:.1%}')
    print(f'{n_feature_cols} colunas de features + "label". Salvo em: {args.out}')
    if pos_rate < 0.02 or pos_rate > 0.9:
        print('Aviso: classe bem desbalanceada ou quase toda de um jeito so -- '
              'confira se as mortes estao sendo registradas (telemetry_logger.js '
              'imprime no console do navegador a cada morte).')


if __name__ == '__main__':
    main()