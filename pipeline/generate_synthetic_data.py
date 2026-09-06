"""
generate_synthetic_data.py
---------------------------------------------------------------------------
Fase 3: popula o Postgres (mesmo schema.sql do TelemetryService) com sessoes
falsas, na tabela player_events, pra dar pro build_dataset.py (Fase 4) algo
realista pra processar antes de termos dados de jogo de verdade.

DIFERENCA para telemetry/bridge/ (e o simulate_tmwa.py usado pra testar a
Fase 1.5): aquilo simula TRAFEGO DE REDE (UDP -> bridge -> gRPC) pra validar
o transporte. Este script escreve DIRETO no Postgres via psycopg2 -- nao
depende de TelemetryService/bridge rodando, e um script de seed de dados,
nao um teste de rede.

MODELO DE MOVIMENTO (o que da sinal aprendivel pro Fase 4/5)
Cada jogador simulado anda em direcao a um "alvo" que muda de vez em quando
(random walk com meta, nao ruido puro) -- ou seja, a proxima posicao NAO e
independente da anterior, tem autocorrelacao de direcao. E exatamente esse
tipo de padrao que Smart Reckoning/Dead Reckoning (ver Introducao do
artigo) tentam explorar, entao serve como teste razoavel pra um modelo
baseline de "prever a proxima direcao/posicao".

Alem disso, cada jogador tem uma chance de "encontro" por snapshot que causa
dano (e possivelmente morte + respawn), gerando eventos player_hurt/
player_death intercalados com os snapshots -- igual aconteceria de verdade.

Uso:
    python generate_synthetic_data.py --players 20 --minutes 30 \
        --dsn "dbname=telemetry user=postgres password=postgres host=localhost"
"""
import argparse
import math
import random
import time
import uuid
from datetime import datetime, timezone

import psycopg2
import psycopg2.extras

SNAPSHOT_INTERVAL_S = 0.5   # igual ao SNAPSHOT_INTERVAL de telemetry.cpp (tmwa)
MAP_W, MAP_H = 200, 200     # area arbitraria -- ajuste se quiser bater com um mapa real do tmwa
MOVE_SPEED = 3.0            # unidades de mapa por snapshot
RESPAWN_INVULN_S = 5.0      # tempo "de respawn" simulado sem novo dano

EVENT_COLUMNS = [
    'session_id', 'batch_sequence', 'bridge_recv_time',
    'event_type', 'char_id', 'x', 'y', 'hp', 'max_hp', 'dead', 'extra',
    'dest_x', 'dest_y', 'target_id', 'continuous',
]


def _sentinel_action_fields(e):
    """Preenche dest_x/dest_y/target_id/continuous com sentinela quando o
    event_type nao usa aquele campo -- mesma convencao do telemetry.cpp real."""
    e.setdefault('dest_x', -1)
    e.setdefault('dest_y', -1)
    e.setdefault('target_id', 0)
    e.setdefault('continuous', False)
    return e


def simulate_player(char_id, rng, duration_s):
    """Gera a sequencia de eventos (dicts) de UM jogador ao longo do tempo."""
    events = []
    t = 0.0
    x, y = rng.uniform(0, MAP_W), rng.uniform(0, MAP_H)
    hp = max_hp = 100
    target_x, target_y = rng.uniform(0, MAP_W), rng.uniform(0, MAP_H)
    invuln_until = -1.0
    aggression = rng.uniform(0.03, 0.15)  # chance de "encontro" por snapshot -- varia por jogador

    while t < duration_s:
        is_invuln = t < invuln_until

        if not is_invuln:
            # troca de alvo de vez em quando ou ao chegar perto -- da
            # autocorrelacao ao movimento (ver docstring do modulo). Cada
            # troca de alvo e o "comando de movimento" (player_move_cmd) --
            # equivalente ao clique de WalkToXY no jogo de verdade.
            if rng.random() < 0.03 or math.hypot(target_x - x, target_y - y) < 2:
                target_x, target_y = rng.uniform(0, MAP_W), rng.uniform(0, MAP_H)
                events.append(_sentinel_action_fields({
                    't': t, 'event_type': 'player_move_cmd', 'x': round(x), 'y': round(y),
                    'hp': hp, 'max_hp': max_hp, 'dead': False, 'extra': 0,
                    'dest_x': round(target_x), 'dest_y': round(target_y),
                }))
            dx, dy = target_x - x, target_y - y
            dist = math.hypot(dx, dy) + 1e-6
            x = min(max(x + dx / dist * MOVE_SPEED, 0), MAP_W)
            y = min(max(y + dy / dist * MOVE_SPEED, 0), MAP_H)

            if rng.random() < aggression:
                # "encontro": metade das vezes o jogador reage atacando de
                # volta (player_attack contra um alvo fake), sempre recebe dano
                if rng.random() < 0.5:
                    events.append(_sentinel_action_fields({
                        't': t, 'event_type': 'player_attack', 'x': round(x), 'y': round(y),
                        'hp': hp, 'max_hp': max_hp, 'dead': False, 'extra': 0,
                        'target_id': rng.randint(10000, 99999), 'continuous': rng.random() < 0.5,
                    }))
                dmg = rng.randint(5, 35)
                hp = max(hp - dmg, 0)
                events.append(_sentinel_action_fields({
                    't': t, 'event_type': 'player_hurt', 'x': round(x), 'y': round(y),
                    'hp': hp, 'max_hp': max_hp, 'dead': False, 'extra': dmg,
                }))
                if hp <= 0:
                    events.append(_sentinel_action_fields({
                        't': t, 'event_type': 'player_death', 'x': round(x), 'y': round(y),
                        'hp': 0, 'max_hp': max_hp, 'dead': True, 'extra': dmg,
                    }))
                    invuln_until = t + RESPAWN_INVULN_S
                    hp = max_hp
                    x, y = rng.uniform(0, MAP_W), rng.uniform(0, MAP_H)

        events.append(_sentinel_action_fields({
            't': t, 'event_type': 'snapshot', 'x': round(x), 'y': round(y),
            'hp': hp, 'max_hp': max_hp, 'dead': is_invuln, 'extra': 0,
        }))
        t += SNAPSHOT_INTERVAL_S

    for e in events:
        e['char_id'] = char_id
    return events


def write_session(conn, session_id, all_events):
    with conn.cursor() as cur:
        cur.execute(
            "INSERT INTO sessions (session_id, game_version, recorded_at, bridge_host) "
            "VALUES (%s, %s, %s, %s) ON CONFLICT (session_id) DO NOTHING",
            (session_id, 'synthetic', datetime.now(timezone.utc), 'generate_synthetic_data.py'),
        )
        rows = [
            (session_id, -1, e['bridge_recv_time'], e['event_type'], e['char_id'],
             e['x'], e['y'], e['hp'], e['max_hp'], e['dead'], e['extra'],
             e['dest_x'], e['dest_y'], e['target_id'], e['continuous'])
            for e in all_events
        ]
        psycopg2.extras.execute_values(
            cur, f"INSERT INTO player_events ({', '.join(EVENT_COLUMNS)}) VALUES %s", rows,
        )
    conn.commit()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--players', type=int, default=20, help='numero de jogadores simulados')
    ap.add_argument('--minutes', type=float, default=30, help='duracao simulada de cada jogador, em minutos')
    ap.add_argument('--dsn', default='dbname=telemetry user=postgres password=postgres host=localhost')
    ap.add_argument('--seed', type=int, default=42)
    args = ap.parse_args()

    rng = random.Random(args.seed)
    duration_s = args.minutes * 60
    session_id = f'synthetic-{uuid.uuid4()}'
    now = time.time()

    all_events = []
    for char_id in range(1, args.players + 1):
        player_rng = random.Random(rng.randint(0, 10_000_000))
        for e in simulate_player(char_id, player_rng, duration_s):
            e['bridge_recv_time'] = now - duration_s + e['t']  # timestamps reais, terminando "agora"
            all_events.append(e)

    conn = psycopg2.connect(args.dsn)
    try:
        write_session(conn, session_id, all_events)
    finally:
        conn.close()

    n_snap = sum(1 for e in all_events if e['event_type'] == 'snapshot')
    n_hurt = sum(1 for e in all_events if e['event_type'] == 'player_hurt')
    n_death = sum(1 for e in all_events if e['event_type'] == 'player_death')
    n_move = sum(1 for e in all_events if e['event_type'] == 'player_move_cmd')
    n_attack = sum(1 for e in all_events if e['event_type'] == 'player_attack')
    print(f'sessao {session_id}: {args.players} jogadores x {args.minutes:.0f}min -> '
          f'{len(all_events)} eventos ({n_snap} snapshots, {n_move} move_cmd, {n_attack} attack, '
          f'{n_hurt} hurt, {n_death} death)')


if __name__ == '__main__':
    main()
