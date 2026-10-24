"""
evaluate_ingame.py
---------------------------------------------------------------------------
Avaliacao EM JOGO do telegraph: le o log que o ManaVerse grava com
TCC_PREDICTOR_LOG=<arquivo.csv> (ver beingactionpredictor.h) e mede se o
aviso antecipado veio antes dos ataques que o cliente observador viu.

GROUND TRUTH = o proprio log. As linhas "attack" sao os ataques de jogadores
remotos que o cliente observador recebeu do servidor (Being::handleAttack),
ou seja, exatamente o que o jogador ve na tela. Assim nao precisa alinhar
relogio nem mapear nome -> char_id. Com --dsn, o script cruza tambem com o
player_attack da telemetria do servidor (pelo nome do personagem, via
athena.txt), pra saber quantos ataques reais o observador NAO viu (fora da
tela, por exemplo).

DIFERENCA PRO TREINO: no treino o rotulo e "a proxima ACAO e ataque",
avaliado so nos instantes de acao. Em jogo o cliente roda o modelo a cada
200 ms, entao aqui o rotulo de cada inferencia e "esta being ataca nos
proximos --window segundos". Os numeros NAO sao comparaveis 1:1 com o
leave-one-player-out do train_model.py.

Metricas:
  - por EPISODIO de ataque (1o ataque depois de >= --episode-gap s sem
    atacar): recall = episodios com telegraph da mesma being nos --window s
    anteriores; antecedencia = t_ataque - t_telegraph
  - por TELEGRAPH: precisao = telegraphs seguidos de ataque da mesma being
    em ate --window s
  - por INFERENCIA: PR-AUC de P(attack) contra "ataca nos proximos
    --window s", com prev_action_was_attack como baseline

Uso:
    python evaluate_ingame.py --log ~/tcc/predictor-log.csv [--window 1.0] \
        [--dsn "dbname=telemetry user=postgres password=postgres host=localhost port=5433" \
         --athena ~/tcc/tmwa-server-data/world/save/athena.txt]
"""
import argparse
import json

import numpy as np
import pandas as pd
from sklearn.metrics import average_precision_score


def load_log(paths):
    log = pd.concat([pd.read_csv(p) for p in paths], ignore_index=True)
    return log.sort_values('wall_time', kind='stable').reset_index(drop=True)


def next_attack_within(t_query, t_attacks, window):
    """True se existe ataque em (t, t + window] -- t_attacks ordenado."""
    i = np.searchsorted(t_attacks, t_query, side='right')
    ok = i < len(t_attacks)
    out = np.zeros(len(t_query), dtype=bool)
    out[ok] = t_attacks[i[ok]] - t_query[ok] <= window
    return out


def evaluate(log, window, episode_gap):
    per_being, episodes_all, tele_all, infer_all = [], [], [], []
    for (bid, name), g in log[log.being_id.notna()].groupby(['being_id', 'name']):
        att = g.loc[g.event == 'attack', 'wall_time'].to_numpy()
        tel = g.loc[g.event == 'telegraph', 'wall_time'].to_numpy()
        inf = g[g.event == 'infer']

        # episodios: ataque sem outro ataque da mesma being nos episode_gap s anteriores
        starts = att[np.r_[True, np.diff(att) >= episode_gap]] if len(att) else att
        for t in starts:
            prev_tel = tel[(tel < t) & (tel >= t - window)]
            episodes_all.append({'name': name, 't': t, 'antecipado': len(prev_tel) > 0,
                                 'antecedencia_s': t - prev_tel.max() if len(prev_tel) else np.nan})
        for t, hit in zip(tel, next_attack_within(tel, att, window)):
            tele_all.append({'name': name, 't': t, 'acertou': bool(hit)})
        if len(inf):
            infer_all.append(pd.DataFrame({
                'name': name, 'p_attack': inf.p_attack.to_numpy(),
                'prev_action_was_attack': inf.prev_action_was_attack.to_numpy(),
                'y': next_attack_within(inf.wall_time.to_numpy(), att, window)}))
        per_being.append({'being_id': int(bid), 'name': name, 'ataques_vistos': len(att),
                          'episodios': len(starts), 'telegraphs': len(tel), 'inferencias': len(inf)})

    ep = pd.DataFrame(episodes_all, columns=['name', 't', 'antecipado', 'antecedencia_s'])
    te = pd.DataFrame(tele_all, columns=['name', 't', 'acertou'])
    inf = pd.concat(infer_all, ignore_index=True) if infer_all else pd.DataFrame(
        columns=['name', 'p_attack', 'prev_action_was_attack', 'y'])

    res = {'window_s': window, 'episode_gap_s': episode_gap, 'por_being': per_being}
    res['episodios'] = {'n': len(ep), 'recall': float(ep.antecipado.mean()) if len(ep) else None,
                        'antecedencia_mediana_s': float(ep.antecedencia_s.median()) if ep.antecipado.any() else None}
    res['telegraphs'] = {'n': len(te), 'precisao': float(te.acertou.mean()) if len(te) else None}
    if len(inf) and inf.y.nunique() == 2:
        res['inferencias'] = {
            'n': len(inf), 'prevalencia': float(inf.y.mean()),
            'pr_auc_modelo': float(average_precision_score(inf.y, inf.p_attack)),
            'pr_auc_baseline_repete_anterior': float(average_precision_score(inf.y, inf.prev_action_was_attack))}
    else:
        res['inferencias'] = {'n': len(inf), 'aviso': 'sem as duas classes -- PR-AUC indefinido'}
    return res


def server_crosscheck(log, dsn, athena):
    """Ataques do servidor (player_attack) vs. ataques que o observador viu, por personagem."""
    import psycopg2
    chars = pd.read_csv(athena, sep='\t', header=None, usecols=[0, 2], names=['char_id', 'name'],
                        dtype={'char_id': str}, quoting=3, on_bad_lines='skip')
    chars = chars[chars.char_id.str.fullmatch(r'\d+')].astype({'char_id': int})
    names = log.loc[log.name.notna(), 'name'].unique()
    chars = chars[chars.name.isin(names)]
    t0, t1 = float(log.wall_time.min()), float(log.wall_time.max())
    with psycopg2.connect(dsn) as conn, conn.cursor() as cur:
        cur.execute("""SELECT char_id, count(*) FROM player_events
                       WHERE event_type = 'player_attack' AND char_id = ANY(%s)
                         AND bridge_recv_time BETWEEN %s AND %s GROUP BY char_id""",
                    ([int(c) for c in chars.char_id], t0, t1))
        server = dict(cur.fetchall())
    seen = log[log.event == 'attack'].groupby('name').size()
    return [{'name': r.name, 'char_id': int(r.char_id), 'ataques_servidor': int(server.get(r.char_id, 0)),
             'ataques_vistos_cliente': int(seen.get(r.name, 0))} for r in chars.itertuples()]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--log', nargs='+', required=True, help='CSV(s) do TCC_PREDICTOR_LOG')
    ap.add_argument('--window', type=float, default=1.0, help='horizonte do aviso, em segundos')
    ap.add_argument('--episode-gap', type=float, default=2.0,
                    help='segundos sem atacar pra um ataque contar como inicio de episodio')
    ap.add_argument('--dsn', default=None, help='com --athena: cruza com player_attack do servidor')
    ap.add_argument('--athena', default=None, help='tmwa-server-data/world/save/athena.txt (nome -> char_id)')
    ap.add_argument('--out', default='ingame_metrics.json')
    args = ap.parse_args()

    log = load_log(args.log)
    thr = log.loc[log.event == 'init', 'threshold'].dropna().unique()
    print(f'log: {len(log)} linhas, eventos {dict(log.event.value_counts())}, limiar(es) no init: {list(thr)}')

    res = evaluate(log, args.window, args.episode_gap)
    res['limiares'] = [float(t) for t in thr]
    print('\npor jogador remoto observado:')
    print(pd.DataFrame(res['por_being']).to_string(index=False))
    e, t, i = res['episodios'], res['telegraphs'], res['inferencias']
    print(f'\nepisodios de ataque: {e["n"]} | recall (telegraph nos {args.window}s antes): {e["recall"]} '
          f'| antecedencia mediana: {e["antecedencia_mediana_s"]} s')
    print(f'telegraphs: {t["n"]} | precisao (ataque em ate {args.window}s): {t["precisao"]}')
    print(f'inferencias: {i}')

    if args.dsn and args.athena:
        res['servidor_vs_cliente'] = server_crosscheck(log, args.dsn, args.athena)
        print('\nataques no servidor vs. vistos pelo observador (mesmo intervalo do log):')
        print(pd.DataFrame(res['servidor_vs_cliente']).to_string(index=False))

    with open(args.out, 'w') as f:
        json.dump(res, f, indent=2, default=float)
    print(f'\nMetricas salvas em: {args.out}')


if __name__ == '__main__':
    main()
