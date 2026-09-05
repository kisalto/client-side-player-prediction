"""
generate_synthetic_data.py
---------------------------------------------------------------------------
Gera sessoes de jogo FALSAS no mesmo formato JSON que o telemetry_logger.js
produz, simulando um jogador esquivando de balas com fisica simples.

Serve para validar toda a pipeline (build_dataset.py -> train_model.py ->
export_onnx.py) antes de voce ter dados reais de jogo. Depois que voce
tiver sessoes reais (jogando com o telemetry_logger.js), e so trocar os
arquivos em data/raw/ pelos seus .json de verdade -- o resto do pipeline
nao muda.

Uso:
    python generate_synthetic_data.py --sessions 25 --out data/raw
"""
import argparse
import json
import math
import os
import random


FIELD_W = 384.0
FIELD_H = 448.0
DT_MS = 1000.0 / 60.0       # ~16.67ms, equivalente a 60fps
UNHITTABLE_MS = 1667.0      # ~100 frames a 60fps, igual ao jogo original
K_NEAREST = 12

PLAYER_CW, PLAYER_CH = 5.0, 5.0     # parecido com o hitbox apertado do Fighter real
PLAYER_SPEED = 4.3                  # px por tick (~ SPAN_FAST/SLOW do jogo real)


def aabb_collides(ax, ay, aw, ah, bx, by, bw, bh):
    return (abs(ax - bx) * 2 < (aw + bw)) and (abs(ay - by) * 2 < (ah + bh))


def simulate_session(session_idx, rng, n_ticks):
    frames = []
    deaths = []

    px, py = FIELD_W / 2, FIELD_H - 80
    unhit_until_ms = 0.0
    lives = 3
    bombs = 3
    score = 0
    graze_total = 0
    next_bullet_id = 1
    bullets = []  # cada: {id, x, y, vx, vy, cw, ch}
    t_ms = 0.0
    spawn_accum = 0.0
    difficulty = rng.uniform(0.7, 1.4)  # varia a "intensidade" da sessao

    for f in range(n_ticks):
        dt = DT_MS + rng.uniform(-2.0, 2.0)
        t_ms += dt

        # --- spawn de novas balas -------------------------------------------------
        spawn_accum += dt
        spawn_interval = 480.0 / difficulty
        while spawn_accum >= spawn_interval:
            spawn_accum -= spawn_interval
            sx = rng.uniform(0, FIELD_W)
            sy = rng.uniform(-20, 40)
            aimed = rng.random() < 0.6
            speed = rng.uniform(1.1, 2.6) * difficulty
            if aimed:
                ang = math.atan2(py - sy, px - sx) + rng.uniform(-0.35, 0.35)
            else:
                ang = rng.uniform(0, math.pi)  # geralmente descendo
            bullets.append({
                'id': next_bullet_id,
                'x': sx, 'y': sy,
                'vx': math.cos(ang) * speed, 'vy': math.sin(ang) * speed,
                'cw': rng.choice([8.0, 12.0, 16.0]), 'ch': rng.choice([8.0, 12.0, 16.0]),
            })
            next_bullet_id += 1

        # --- move balas e descarta as que sairam do campo -----------------------
        alive_bullets = []
        for b in bullets:
            b['x'] += b['vx'] * (dt / DT_MS)
            b['y'] += b['vy'] * (dt / DT_MS)
            if -40 <= b['x'] <= FIELD_W + 40 and -40 <= b['y'] <= FIELD_H + 40:
                alive_bullets.append(b)
        bullets = alive_bullets

        # --- jogador: desvia das ameacas mais proximas (com "erro" humano) -------
        is_unhittable = t_ms < unhit_until_ms
        threat_x, threat_y = 0.0, 0.0
        for b in bullets:
            dx, dy = px - b['x'], py - b['y']
            dist = math.hypot(dx, dy) + 1e-6
            # so reage a bala relativamente perto e vindo na direcao geral do jogador
            closing = -(dx * b['vx'] + dy * b['vy']) / dist
            if dist < 130 and closing > 0:
                weight = closing / (dist * dist)
                threat_x += dx / dist * weight
                threat_y += dy / dist * weight

        norm = math.hypot(threat_x, threat_y)
        reaction_error = rng.uniform(0.55, 1.0)  # jogador nao e perfeito
        if norm > 1e-6:
            mvx = threat_x / norm * reaction_error
            mvy = threat_y / norm * reaction_error
        else:
            mvx = rng.uniform(-1, 1) * 0.3
            mvy = rng.uniform(-1, 1) * 0.3
        mvx += rng.uniform(-0.25, 0.25)
        mvy += rng.uniform(-0.25, 0.25)
        mnorm = math.hypot(mvx, mvy)
        slow = rng.random() < 0.25
        speed = PLAYER_SPEED * (0.65 if slow else 1.0)
        if mnorm > 1e-6:
            px += mvx / mnorm * speed * (dt / DT_MS)
            py += mvy / mnorm * speed * (dt / DT_MS)
        px = min(max(px, 0), FIELD_W)
        py = min(max(py, 0), FIELD_H)

        # --- registra o frame (estado ANTES da resolucao de colisao deste tick) --
        withdist = sorted(bullets, key=lambda b: (b['x'] - px) ** 2 + (b['y'] - py) ** 2)
        nearest = [{'id': b['id'], 'x': round(b['x'], 2), 'y': round(b['y'], 2),
                    'cw': b['cw'], 'ch': b['ch']} for b in withdist[:K_NEAREST]]

        frames.append({
            't': round(t_ms, 2), 'f': f,
            'px': round(px, 2), 'py': round(py, 2),
            'pcw': PLAYER_CW, 'pch': PLAYER_CH,
            'slow': 1 if slow else 0,
            'unhit': 1 if is_unhittable else 0,
            'lives': lives, 'bombs': bombs, 'score': score,
            'grazeTotal': graze_total,
            'bulletCount': len(bullets),
            'bullets': nearest,
        })

        # --- graze: perto mas sem colidir (usa uma caixa maior) -------------------
        for b in bullets:
            if aabb_collides(px, py, PLAYER_CW * 3.5, PLAYER_CH * 3.5, b['x'], b['y'], b['cw'], b['ch']):
                graze_total += 1

        # --- colisao de verdade (so conta se nao estiver invencivel) --------------
        if not is_unhittable:
            for b in bullets:
                if aabb_collides(px, py, PLAYER_CW, PLAYER_CH, b['x'], b['y'], b['cw'], b['ch']):
                    deaths.append(f)
                    lives -= 1
                    unhit_until_ms = t_ms + UNHITTABLE_MS
                    px, py = FIELD_W / 2, FIELD_H - 80
                    if lives <= 0:
                        lives = 3  # "continue" simulado, sessao nao para
                    break

    return {
        'meta': {
            'kNearest': K_NEAREST, 'fieldWidth': FIELD_W, 'fieldHeight': FIELD_H,
            'recordedAt': f'synthetic-session-{session_idx}',
            'frameCount': len(frames), 'deathCount': len(deaths),
        },
        'frames': frames,
        'deaths': deaths,
    }


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--sessions', type=int, default=25, help='numero de sessoes sinteticas a gerar')
    ap.add_argument('--ticks', type=int, default=3600, help='frames por sessao (~60s a 60fps)')
    ap.add_argument('--out', default='data/raw', help='pasta de saida para os .json')
    ap.add_argument('--seed', type=int, default=42)
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    rng = random.Random(args.seed)

    total_deaths = 0
    for i in range(args.sessions):
        session_rng = random.Random(rng.randint(0, 10_000_000))
        data = simulate_session(i, session_rng, args.ticks)
        total_deaths += data['meta']['deathCount']
        path = os.path.join(args.out, f'synthetic_session_{i:03d}.json')
        with open(path, 'w') as fh:
            json.dump(data, fh)

    print(f'{args.sessions} sessoes geradas em {args.out}/ '
          f'({args.sessions * args.ticks} frames totais, {total_deaths} mortes simuladas).')


if __name__ == '__main__':
    main()