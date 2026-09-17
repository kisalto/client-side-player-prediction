"""
build_dataset.py
---------------------------------------------------------------------------
Fase 4: le player_events do Postgres e monta um dataset supervisionado
pronto pra scikit-learn: X = estado do jogador no instante da decisao,
Y = a proxima acao que ele realmente tomou (player_move_cmd ou
player_attack).

CONCEITO (por que "por evento de acao", nao "por janela de tempo")
Ao contrario do toho-like-js (onde a pergunta era "vai morrer nos proximos
N segundos?", uma janela de tempo fixa fazia sentido porque a morte podia
vir de varias fontes ao mesmo tempo), aqui o objetivo e compensar latencia
de rede -- ou seja, prever a PROXIMA acao discreta que o jogador vai
comandar, nao "alguma acao numa janela". Entao cada linha do dataset =
1 acao real que aconteceu (um evento player_move_cmd ou player_attack),
com X = tudo que dava pra saber IMEDIATAMENTE ANTES daquela acao (posicao e
vida atuais -- ja vem no proprio evento, telemetry.cpp grava sd->bl_x/y e
sd->status.hp no momento da acao -- mais o historico recente: velocidade,
tempo desde a ultima acao, tempo desde o ultimo dano).

LIMITACAO CONHECIDA: coordenadas nao tem identidade de mapa
telemetry.cpp (v2) nao grava em qual mapa o jogador estava (so x,y). Isso
significa que, com dados REAIS (jogador trocando de mapa), x/y absolutos de
mapas diferentes ficam misturados sem sentido -- 2 pontos com o mesmo (x,y)
em mapas diferentes sao lugares completamente distintos. Por isso este
script usa principalmente features RELATIVAS (velocidade, deslocamento,
tempo) em vez de x/y absoluto como sinal principal -- x/y absoluto entra
como feature auxiliar, nao like a unica fonte de posicao. Se quiser
features de mapa mais ricas depois (ex: "essa regiao do mapa X tem mais
combate"), precisa voltar em telemetry.cpp e adicionar o nome do mapa
(sd->bl_m->name_) ao payload -- nao fiz isso agora pra nao reabrir o C++
de novo sem necessidade clara.

Uso:
    python build_dataset.py --dsn "dbname=telemetry user=postgres password=postgres host=localhost" \
        --out dataset.csv [--session-id SESSION_ID]
"""
import argparse

import numpy as np
import pandas as pd
import psycopg2

ACTION_TYPES = {'player_move_cmd', 'player_attack'}
NO_HISTORY_SENTINEL = -1.0  # "nunca aconteceu ainda" pra time_since_* (vira o maior valor da coluna na hora do uso)
MIN_DT_S = 0.05   # piso pro delta-tempo usado no calculo de velocidade -- eventos
                    # praticamente simultaneos (ex: move_cmd seguido de attack no
                    # mesmo tick) geram dt~0, que dividindo um deslocamento real
                    # da uma "velocidade" absurda; 50ms e mais realista que 1ms
MAX_SPEED = 500.0  # teto de seguranca (unidades de mapa/s) -- corta qualquer
                    # resíduo numérico que passe do piso de dt acima


def load_events(dsn, session_id=None):
    query = """
        SELECT session_id, char_id, event_type, x, y, hp, max_hp, dead,
               extra, dest_x, dest_y, target_id, continuous, bridge_recv_time
        FROM player_events
    """
    params = ()
    if session_id:
        query += " WHERE session_id = %s"
        params = (session_id,)
    query += " ORDER BY char_id, bridge_recv_time"

    with psycopg2.connect(dsn) as conn:
        df = pd.read_sql_query(query, conn, params=params)
    return df


def build_rows_for_player(events):
    """events: DataFrame de UM char_id, ja ordenado por bridge_recv_time.
    Retorna lista de dicts, uma por acao (move_cmd/attack) com historico
    suficiente pra calcular as features."""
    rows = []

    prev_x = prev_y = prev_t = None       # ultima posicao conhecida (qualquer tipo de evento)
    last_action_t = None
    last_action_type = 'none'
    last_hurt_t = None
    last_hurt_extra = 0.0

    for row in events.itertuples(index=False):
        t = row.bridge_recv_time

        if row.event_type == 'player_hurt':
            last_hurt_t = t
            last_hurt_extra = float(row.extra)

        if row.event_type in ACTION_TYPES:
            if prev_t is not None and last_action_t is not None:
                dt_since_action = t - last_action_t
                dt_since_pos = max(t - prev_t, MIN_DT_S)
                vx = np.clip((row.x - prev_x) / dt_since_pos, -MAX_SPEED, MAX_SPEED)
                vy = np.clip((row.y - prev_y) / dt_since_pos, -MAX_SPEED, MAX_SPEED)

                is_move = row.event_type == 'player_move_cmd'
                rows.append({
                    'char_id': row.char_id,
                    'session_id': row.session_id,
                    't': t,
                    # -- features (X): tudo conhecido ANTES/NO momento da acao --
                    'x': row.x,
                    'y': row.y,
                    'hp': row.hp,
                    'max_hp': row.max_hp,
                    'hp_ratio': row.hp / row.max_hp if row.max_hp else np.nan,
                    'dead': bool(row.dead),
                    'velocity_x': vx,
                    'velocity_y': vy,
                    'speed': (vx ** 2 + vy ** 2) ** 0.5,
                    'time_since_last_action': dt_since_action,
                    'time_since_last_hurt': (t - last_hurt_t) if last_hurt_t is not None else NO_HISTORY_SENTINEL,
                    'last_damage_taken': last_hurt_extra,
                    'prev_action_type': last_action_type,
                    # -- labels (Y) --
                    'action_type': 'move' if is_move else 'attack',
                    'move_dx': (row.dest_x - row.x) if is_move else np.nan,
                    'move_dy': (row.dest_y - row.y) if is_move else np.nan,
                    'attack_continuous': bool(row.continuous) if not is_move else np.nan,
                })

            last_action_t = t
            last_action_type = 'move' if row.event_type == 'player_move_cmd' else 'attack'

        # toda mensagem carrega x,y (ver telemetry.proto) -- atualiza a
        # posicao conhecida mais recente independente do tipo de evento
        prev_x, prev_y, prev_t = row.x, row.y, t

    return rows


def build_dataset(df):
    all_rows = []
    for char_id, group in df.groupby('char_id', sort=False):
        all_rows.extend(build_rows_for_player(group))
    return pd.DataFrame(all_rows)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--dsn', default='dbname=telemetry user=postgres password=postgres host=localhost')
    ap.add_argument('--session-id', default=None, help='filtra por uma sessao especifica (default: todas)')
    ap.add_argument('--out', default='dataset.csv')
    args = ap.parse_args()

    raw = load_events(args.dsn, args.session_id)
    if raw.empty:
        raise SystemExit('nenhum evento encontrado no Postgres (rode generate_synthetic_data.py primeiro?)')

    dataset = build_dataset(raw)
    if dataset.empty:
        raise SystemExit('nenhuma linha de acao com historico suficiente (precisa de pelo menos 2 eventos por jogador)')

    dataset.to_csv(args.out, index=False)

    n_players = dataset['char_id'].nunique()
    class_counts = dataset['action_type'].value_counts()
    print(f'{len(raw)} eventos brutos -> {len(dataset)} linhas de acao, {n_players} jogadores.')
    print(f'Distribuicao de action_type: {dict(class_counts)}')
    print(f'Salvo em: {args.out}')


if __name__ == '__main__':
    main()
