"""
statistical_analysis.py
---------------------------------------------------------------------------
Analise estatistica dos resultados da previsao de ataque (leave-one-player-
out por pessoa), a partir do dataset.csv e das P(ataque) out-of-fold salvas
por compare_models.py (oof_tabular.csv.gz) e train_sequence_model.py
(oof_sequencia.csv.gz). Todos os modelos sao avaliados nas MESMAS linhas, o
que permite testes pareados.

Secoes do relatorio (report.txt; tabelas em tabelas/*.csv; figuras em figuras/):
 1. Comparacao dos modelos: PR-AUC, ROC-AUC, Brier, precisao/recall no limiar
    de recall-alvo (escolhido nas predicoes OOF dos OUTROS jogadores), precisao
    no recall do baseline; variacao entre sementes.
 2. Incerteza: bootstrap por pessoa, bootstrap de blocos de 60 s dentro de
    cada pessoa e bootstrap hierarquico (pessoa -> blocos); Wilcoxon pareado
    (exato) com tamanho de efeito e correcao de Holm; Friedman; teste de
    permutacao dos rotulos (embaralhamento livre e deslocamento circular, que
    preserva a autocorrelacao); McNemar.
 3. Associacao de cada feature com o ataque: ponto-bisserial, Mann-Whitney,
    AUC de uma variavel, d de Cohen, delta de Cliff, informacao mutua,
    importancia por permutacao, tabela 2x2 de prev_action_was_attack
    (qui-quadrado, Fisher, razao de chances, Mantel-Haenszel, Breslow-Day).
 4. Modelos estatisticos classicos: regressao logistica (razoes de chances),
    logistica com efeito fixo e com efeito aleatorio por pessoa (modelo misto),
    sobrevivencia (Kaplan-Meier e Weibull dos intervalos entre ataques, Cox de
    risco especifico de ataque pelo tempo desde a ultima acao) e cadeias de
    Markov (ordem, homogeneidade entre pessoas).
 5. Calibracao e limiar: Brier, ECE, intercepto/inclinacao de calibracao,
    recalibracao Platt/isotonica, calibracao por pessoa numa janela inicial de
    cada sessao, curva de decisao (ganho liquido).
 6. PCA e alternativas: PCA, correlacao de Spearman e VIF, Kruskal-Wallis e
    ANOVA de Welch entre pessoas (com post-hoc de Dunn), MANOVA, LDA
    (ataque vs move e entre pessoas).
 7. Curva de aprendizado e tamanho de janela K (se os arquivos existirem).

CUIDADO NA LEITURA: as acoes nao sao independentes (mesmo jogador, em
sequencia), entao p-valores calculados por ACAO (secoes 3, 4 e 6) saem
otimistas; o que vale ali e o TAMANHO DE EFEITO e a consistencia entre as
pessoas. A inferencia sobre os modelos (secao 2) usa a PESSOA como unidade
(n=6) ou reamostra blocos de tempo.

Uso:
    python statistical_analysis.py --dataset dataset.csv --results-dir resultados/ [--n-boot 10000] [--n-perm 10000]
"""
import argparse
import itertools
import json
import os
import time
import warnings

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
import statsmodels.api as sm
from scipy import stats
from sklearn.discriminant_analysis import LinearDiscriminantAnalysis
from sklearn.decomposition import PCA
from sklearn.feature_selection import mutual_info_classif
from sklearn.isotonic import IsotonicRegression
from sklearn.metrics import (average_precision_score, balanced_accuracy_score, brier_score_loss, confusion_matrix,
                             log_loss, precision_recall_curve, roc_auc_score)
from sklearn.preprocessing import StandardScaler
from statsmodels.genmod.bayes_mixed_glm import BinomialBayesMixedGLM
from statsmodels.multivariate.manova import MANOVA
from statsmodels.stats.contingency_tables import StratifiedTable, Table2x2, mcnemar
from statsmodels.stats.multitest import multipletests
from statsmodels.stats.oneway import anova_oneway
from statsmodels.stats.outliers_influence import variance_inflation_factor

from compare_models import BLOCK, log_features
from train_model import ALL_FEATURES, load_dataset, pick_threshold

BASELINE = 'baseline_repete_anterior'
REF = 'random_forest'
# ordem de exibicao; modelos ausentes nos arquivos de predicao sao ignorados
MODEL_ORDER = ['baseline_repete_anterior', 'markov_ordem1', 'markov_ordem2', 'markov_ordem3', 'markov_ordem5',
               'regressao_logistica', 'regressao_logistica_log', 'lda', 'random_forest', 'xgboost', 'mlp',
               'rede_sem_historico', 'lstm', 'gru']
FIG_MODELS = ['baseline_repete_anterior', 'markov_ordem3', 'regressao_logistica_log', 'random_forest', 'xgboost',
              'mlp', 'rede_sem_historico', 'lstm', 'gru']
MCNEMAR_PAIRS = [('random_forest', BASELINE), ('xgboost', 'random_forest'), ('lstm', 'random_forest'),
                 ('gru', 'random_forest'), ('lstm', 'rede_sem_historico'), ('gru', 'rede_sem_historico'),
                 ('regressao_logistica_log', 'random_forest')]
BLOCK_SECONDS = 60.0     # tamanho do bloco de tempo do bootstrap/permutacao por blocos
CALIB_WINDOWS_MIN = (2, 5, 10)
MIN_WINDOW_ATTACKS = 5   # janela inicial com menos ataques que isso nao calibra (volta pro global)
EPS = 1e-6


# ---------------------------------------------------------------------------
# utilitarios
# ---------------------------------------------------------------------------
class Report:
    def __init__(self, out_dir):
        self.out_dir = out_dir
        os.makedirs(os.path.join(out_dir, 'tabelas'), exist_ok=True)
        os.makedirs(os.path.join(out_dir, 'figuras'), exist_ok=True)
        self.f = open(os.path.join(out_dir, 'report.txt'), 'w')
        self.summary = {}

    def __call__(self, msg=''):
        print(msg, flush=True)
        self.f.write(msg + '\n')
        self.f.flush()

    def section(self, title):
        self('\n' + '=' * 78 + f'\n{title}\n' + '=' * 78)

    def table(self, df, name, fmt='{:.3f}', index=True):
        df.to_csv(os.path.join(self.out_dir, 'tabelas', f'{name}.csv'), index=index)
        with pd.option_context('display.max_rows', 500, 'display.max_columns', 50, 'display.width', 250,
                               'display.float_format', lambda v: fmt.format(v)):
            self(df.to_string(index=index))

    def fig(self, fig, name):
        fig.savefig(os.path.join(self.out_dir, 'figuras', f'{name}.png'), dpi=130, bbox_inches='tight')
        plt.close(fig)


class FastAP:
    """PR-AUC (average precision, mesma definicao do sklearn) recalculada rapido
    pros MESMOS scores com rotulos ou pesos diferentes (bootstrap, permutacao):
    ordena uma vez so. y e w podem ser 1-D (n,) ou 2-D (B, n)."""

    def __init__(self, scores):
        self.order = np.argsort(-scores, kind='mergesort')
        s = scores[self.order]
        self.ends = np.r_[np.nonzero(s[1:] != s[:-1])[0], len(s) - 1]  # fim de cada grupo de empate

    def __call__(self, y, w=None):
        ys = np.asarray(y, dtype=float)[..., self.order]
        if w is None:
            tot = np.broadcast_to((self.ends + 1).astype(float), ys.shape[:-1] + (len(self.ends),))
        else:
            ws = np.asarray(w, dtype=float)[..., self.order]
            ys = ys * ws
            tot = np.cumsum(ws, axis=-1)[..., self.ends]
        tp = np.cumsum(ys, axis=-1)[..., self.ends]
        pos = tp[..., -1:]
        prec = np.divide(tp, tot, out=np.zeros_like(tp), where=tot > 0)
        rec = np.divide(tp, pos, out=np.zeros_like(tp), where=pos > 0)
        rec_prev = np.concatenate([np.zeros_like(rec[..., :1]), rec[..., :-1]], axis=-1)
        ap = np.sum((rec - rec_prev) * prec, axis=-1)
        return np.where(pos[..., 0] > 0, ap, np.nan)


def holm(p):
    p = np.asarray(p, dtype=float)
    out = np.full_like(p, np.nan)
    ok = ~np.isnan(p)
    if ok.any():
        out[ok] = multipletests(p[ok], method='holm')[1]
    return out


def ci(a, level=0.95):
    a = np.asarray(a, dtype=float)
    a = a[~np.isnan(a)]
    if not len(a):
        return np.nan, np.nan
    return float(np.percentile(a, 100 * (1 - level) / 2)), float(np.percentile(a, 100 * (1 + level) / 2))


def fmt_ci(lo, hi):
    return f'[{lo:.3f}, {hi:.3f}]'


def safe_logit(p):
    p = np.clip(p, EPS, 1 - EPS)
    return np.log(p / (1 - p))


def threshold_metrics(y, p, thr, w=None):
    """precisao/recall/f1 com pesos opcionais (bootstrap)."""
    w = np.ones(len(y)) if w is None else w
    pred = p >= thr
    tp = np.sum(w * (pred & (y == 1)))
    fp = np.sum(w * (pred & (y == 0)))
    fn = np.sum(w * (~pred & (y == 1)))
    prec = tp / (tp + fp) if tp + fp > 0 else 0.0
    rec = tp / (tp + fn) if tp + fn > 0 else np.nan
    f1 = 2 * prec * rec / (prec + rec) if prec + rec > 0 else 0.0
    return prec, rec, f1


def ece(y, p, n_bins=10, strategy='uniform'):
    if strategy == 'quantile':
        edges = np.unique(np.quantile(p, np.linspace(0, 1, n_bins + 1)))
    else:
        edges = np.linspace(0, 1, n_bins + 1)
    idx = np.clip(np.searchsorted(edges, p, side='right') - 1, 0, len(edges) - 2)
    total = 0.0
    for b in range(len(edges) - 1):
        m = idx == b
        if m.any():
            total += m.sum() / len(p) * abs(y[m].mean() - p[m].mean())
    return float(total)


def calibration_slope_intercept(y, p):
    """inclinacao (ideal 1) e calibracao-na-media (intercepto com offset, ideal 0)."""
    lp = safe_logit(p)
    try:
        slope = sm.GLM(y, sm.add_constant(lp), family=sm.families.Binomial()).fit().params[1]
        inter = sm.GLM(y, np.ones(len(y)), family=sm.families.Binomial(), offset=lp).fit().params[0]
    except Exception:
        return np.nan, np.nan
    return float(slope), float(inter)


def rank_biserial(diff):
    d = diff[diff != 0]
    if not len(d):
        return 0.0
    r = stats.rankdata(np.abs(d))
    return float((r[d > 0].sum() - r[d < 0].sum()) / r.sum())


def hodges_lehmann(diff):
    walsh = [(a + b) / 2 for i, a in enumerate(diff) for b in diff[i:]]
    return float(np.median(walsh))


# ---------------------------------------------------------------------------
# dados
# ---------------------------------------------------------------------------
def load_predictions(results_dir, n_rows):
    preds = {}
    for name in ('oof_tabular.csv.gz', 'oof_sequencia.csv.gz'):
        path = os.path.join(results_dir, name)
        if not os.path.exists(path):
            print(f'(sem {path} -- modelos desse arquivo ficam de fora)')
            continue
        tab = pd.read_csv(path)
        if len(tab) != n_rows or not (tab['row'].to_numpy() == np.arange(n_rows)).all():
            raise SystemExit(f'{path} nao bate linha a linha com o dataset')
        for col in tab.columns[1:]:
            model, seed = col.rsplit('@', 1)
            preds.setdefault(model, {})[int(seed)] = tab[col].to_numpy(dtype=float)
    models = [m for m in MODEL_ORDER if m in preds] + sorted(m for m in preds if m not in MODEL_ORDER)
    out = {}
    for m in models:
        seeds = sorted(preds[m])
        arr = np.vstack([preds[m][s] for s in seeds])
        if np.isnan(arr).any():
            raise SystemExit(f'predicoes faltando (NaN) em {m}')
        out[m] = (seeds, arr)
    return out


def time_blocks(df, seconds):
    """id de bloco de `seconds` segundos dentro de cada (sessao, personagem)."""
    t0 = df.groupby(BLOCK, sort=False)['t'].transform('min')
    key = df['session_id'].astype(str) + '|' + df['char_id'].astype(str) + '|' + \
        ((df['t'] - t0) // seconds).astype(int).astype(str)
    return pd.factorize(key)[0]


# ---------------------------------------------------------------------------
# 1. comparacao dos modelos
# ---------------------------------------------------------------------------
def section_models(rep, df, y, persons, pidx, preds, target_recall, folds_info):
    rep.section('1. COMPARACAO DOS MODELOS (leave-one-player-out, ataque = classe positiva)')
    rep(f'{len(df)} acoes, {int(y.sum())} ataques (prevalencia {y.mean():.3f}), {len(persons)} pessoas. '
        f'Limiar de decisao: recall-alvo {target_recall} nas predicoes OOF dos OUTROS 5 jogadores '
        '(baseline: regra fixa, limiar 0.5).')
    base = df['prev_action_was_attack'].to_numpy(dtype=float)
    rows, thresholds = [], {}
    for m, (seeds, arr) in preds.items():
        for si, s in enumerate(seeds):
            p_all = arr[si]
            for person in persons:
                te = pidx[person]
                tr = np.setdiff1d(np.arange(len(y)), te)
                thr = 0.5 if m == BASELINE else pick_threshold(y[tr], p_all[tr], target_recall)
                thresholds[(m, s, person)] = thr
                yt, pt = y[te], p_all[te]
                prec, rec, f1 = threshold_metrics(yt, pt, thr)
                # comparacao "no mesmo ponto de operacao" do baseline (limiar escolhido no proprio
                # jogador de teste -- descritivo, nao implantavel)
                bprec, brec, _ = threshold_metrics(yt, base[te], 0.5)
                pc, rc, _ = precision_recall_curve(yt, pt)
                rows.append({
                    'modelo': m, 'semente': s, 'pessoa': person, 'n': len(te), 'prevalencia': yt.mean(),
                    'pr_auc': average_precision_score(yt, pt), 'roc_auc': roc_auc_score(yt, pt),
                    'brier': brier_score_loss(yt, pt), 'log_loss': log_loss(yt, np.clip(pt, EPS, 1 - EPS)),
                    'limiar': thr, 'precisao': prec, 'recall': rec, 'f1': f1,
                    'precisao_no_recall_base': pc[rc >= brec].max(),
                    'recall_na_precisao_base': rc[pc >= bprec].max() if (pc >= bprec).any() else 0.0,
                })
    long = pd.DataFrame(rows)
    long.to_csv(os.path.join(rep.out_dir, 'tabelas', 'metricas_por_modelo_semente_pessoa.csv'), index=False)

    metrics = ['pr_auc', 'roc_auc', 'brier', 'log_loss', 'limiar', 'precisao', 'recall', 'f1',
               'precisao_no_recall_base', 'recall_na_precisao_base']
    person_level = long.groupby(['modelo', 'pessoa'], sort=False)[metrics].mean()  # media das sementes
    models = list(preds)

    # PR-AUC por pessoa (media das sementes)
    ap_tab = person_level['pr_auc'].unstack('pessoa').loc[models]
    ap_tab['media'] = ap_tab.mean(axis=1)
    rep('\nPR-AUC por pessoa (media das sementes); prevalencia = PR-AUC de um classificador aleatorio:')
    prev_row = pd.DataFrame([[y[pidx[p]].mean() for p in persons] + [np.mean([y[pidx[p]].mean() for p in persons])]],
                            index=['(prevalencia)'], columns=ap_tab.columns)
    rep.table(pd.concat([prev_row, ap_tab]), 'pr_auc_por_pessoa')

    # resumo por modelo
    summ = []
    for m in models:
        pl = person_level.loc[m]
        seeds, arr = preds[m]
        macro_by_seed = long[long.modelo == m].groupby('semente')['pr_auc'].mean()
        pooled = [average_precision_score(y, arr[i]) for i in range(len(seeds))]
        summ.append({
            'modelo': m, 'sementes': len(seeds),
            'pr_auc_media': pl['pr_auc'].mean(), 'pr_auc_dp_pessoas': pl['pr_auc'].std(ddof=1),
            'pr_auc_min': pl['pr_auc'].min(), 'pr_auc_max': pl['pr_auc'].max(),
            'pr_auc_dp_sementes': macro_by_seed.std(ddof=1) if len(seeds) > 1 else np.nan,
            'pr_auc_agrupado': np.mean(pooled), 'roc_auc': pl['roc_auc'].mean(), 'brier': pl['brier'].mean(),
            'precisao': pl['precisao'].mean(), 'recall': pl['recall'].mean(), 'recall_dp': pl['recall'].std(ddof=1),
            'prec_no_recall_base': pl['precisao_no_recall_base'].mean(),
            'vence_baseline': int((pl['pr_auc'] > person_level.loc[BASELINE]['pr_auc']).sum()),
            'vence_rf': int((pl['pr_auc'] > person_level.loc[REF]['pr_auc']).sum()) if REF in models else np.nan,
        })
    summ = pd.DataFrame(summ).set_index('modelo')
    rep('\nResumo (medias sobre as 6 pessoas; dp_pessoas = desvio entre pessoas; dp_sementes = desvio da media '
        'entre sementes; agrupado = PR-AUC com todas as linhas juntas; vence_* = em quantas das 6 pessoas o '
        'PR-AUC e maior):')
    rep.table(summ, 'resumo_modelos')
    rep('recall-alvo e o limiar sao escolhidos sem o jogador de teste; "prec_no_recall_base" fixa o recall no '
        'do baseline DENTRO do jogador de teste (comparacao descritiva no mesmo ponto de operacao).')

    # sensibilidade do limiar da RF: regra cross-person (usada aqui) x LOPO interno estrito (train_model.py)
    if folds_info and REF in preds and 'limiar_rf_lopo_interno' in folds_info['folds'][0]:
        seeds, arr = preds[REF]
        rows = []
        for f in folds_info['folds']:
            person = f['jogador_teste']
            te = pidx[person]
            thr_x = thresholds[(REF, seeds[0], person)]
            thr_i = f['limiar_rf_lopo_interno']
            px, rx, _ = threshold_metrics(y[te], arr[0][te], thr_x)
            pi, ri, _ = threshold_metrics(y[te], arr[0][te], thr_i)
            rows.append({'pessoa': person, 'limiar_outros_oof': thr_x, 'prec': px, 'rec': rx,
                         'limiar_lopo_interno': thr_i, 'prec_interno': pi, 'rec_interno': ri})
        t = pd.DataFrame(rows).set_index('pessoa')
        rep(f'\nSensibilidade do limiar da RF (semente {seeds[0]}): regra usada aqui (OOF dos outros) x LOPO interno '
            'estrito (como no train_model.py):')
        rep.table(t, 'limiar_rf_sensibilidade')

    # variacao entre sementes
    seed_var = long.groupby(['modelo', 'pessoa'])['pr_auc'].std(ddof=1).groupby('modelo').mean().dropna()
    if len(seed_var):
        rep('\nVariacao entre sementes: dp medio do PR-AUC por pessoa entre sementes -> ' +
            ', '.join(f'{m}: {seed_var[m]:.4f}' for m in models if m in seed_var))

    rep.summary['modelos'] = summ.round(4).reset_index().to_dict(orient='records')
    return long, person_level, thresholds


def fig_pr_curves(rep, y, persons, pidx, preds):
    fig, axes = plt.subplots(2, 3, figsize=(15, 9), sharex=True, sharey=True)
    for ax, person in zip(axes.flat, persons):
        te = pidx[person]
        for m in [m for m in FIG_MODELS if m in preds]:
            pc, rc, _ = precision_recall_curve(y[te], preds[m][1][0][te])
            ax.plot(rc, pc, label=f'{m} ({average_precision_score(y[te], preds[m][1][0][te]):.2f})',
                    lw=1.8 if m in (REF, 'gru', 'lstm', 'xgboost') else 1)
        ax.axhline(y[te].mean(), color='grey', ls=':', lw=1)
        ax.set_title(f'{person} (prevalencia {y[te].mean():.3f})')
        ax.legend(fontsize=7, loc='upper right')
    for ax in axes[-1]:
        ax.set_xlabel('recall (ataque)')
    for ax in axes[:, 0]:
        ax.set_ylabel('precisao (ataque)')
    rep.fig(fig, 'curvas_pr_por_pessoa')


# ---------------------------------------------------------------------------
# 2. incerteza e testes
# ---------------------------------------------------------------------------
def section_uncertainty(rep, df, y, persons, pidx, preds, person_level, thresholds, args):
    rep.section('2. INCERTEZA E TESTES DE HIPOTESE')
    rng = np.random.default_rng(args.seed)
    models = list(preds)
    P = len(persons)
    M = person_level['pr_auc'].unstack('modelo')[models].loc[persons]  # pessoas x modelos

    # 2a. bootstrap por pessoa
    rep(f'\n2a. Bootstrap por PESSOA ({args.n_boot} reamostras das 6 pessoas, com reposicao). Com 6 pessoas so '
        'existem 462 reamostras distintas e o IC tende a ser estreito demais; o IC t (gl=5) vem ao lado como '
        'referencia mais conservadora.')
    boot_idx = rng.integers(0, P, size=(args.n_boot, P))
    tcrit = stats.t.ppf(0.975, P - 1)
    rows = []
    for metric in ('pr_auc', 'precisao', 'recall'):
        Mm = person_level[metric].unstack('modelo')[models].loc[persons].to_numpy()
        means = Mm[boot_idx].mean(axis=1)
        for j, m in enumerate(models):
            lo, hi = ci(means[:, j])
            sd = Mm[:, j].std(ddof=1)
            rows.append({'modelo': m, 'metrica': metric, 'media': Mm[:, j].mean(), 'ic95_boot_pessoa': fmt_ci(lo, hi),
                         'ic95_t': fmt_ci(Mm[:, j].mean() - tcrit * sd / np.sqrt(P), Mm[:, j].mean() + tcrit * sd / np.sqrt(P))})
    rep.table(pd.DataFrame(rows), 'bootstrap_pessoa', index=False)

    # 2b. bootstrap de blocos dentro de cada pessoa (semente base)
    blocks = time_blocks(df, BLOCK_SECONDS)
    nb = args.n_boot_blocks
    rep(f'\n2b. Bootstrap de BLOCOS de {BLOCK_SECONDS:.0f} s dentro de cada pessoa ({nb} reamostras; semente base de '
        'cada modelo; limiar fixo). Preserva a correlacao temporal das acoes. IC95 do PR-AUC por pessoa:')
    store = {person: {k: np.zeros((nb, len(models))) for k in ('ap', 'prec', 'rec')} for person in persons}
    rows = []
    for person in persons:
        te = pidx[person]
        b_ids = pd.factorize(blocks[te])[0]
        n_blocks = b_ids.max() + 1
        yt = y[te]
        aps = {m: FastAP(preds[m][1][0][te]) for m in models}
        for start in range(0, nb, 250):
            stop = min(nb, start + 250)
            counts = np.stack([np.bincount(rng.integers(0, n_blocks, n_blocks), minlength=n_blocks)
                               for _ in range(stop - start)])
            W = counts[:, b_ids].astype(float)
            for j, m in enumerate(models):
                p = preds[m][1][0][te]
                store[person]['ap'][start:stop, j] = aps[m](yt, W)
                pred = p >= thresholds[(m, preds[m][0][0], person)]
                tp = W[:, pred & (yt == 1)].sum(axis=1)
                fp = W[:, pred & (yt == 0)].sum(axis=1)
                fn = W[:, ~pred & (yt == 1)].sum(axis=1)
                store[person]['prec'][start:stop, j] = np.divide(tp, tp + fp, out=np.zeros_like(tp), where=tp + fp > 0)
                store[person]['rec'][start:stop, j] = np.divide(tp, tp + fn, out=np.full_like(tp, np.nan), where=tp + fn > 0)
        row = {'pessoa': person, 'blocos': n_blocks}
        for j, m in enumerate(models):
            lo, hi = ci(store[person]['ap'][:, j])
            row[m] = f'{aps[m](yt):.3f} {fmt_ci(lo, hi)}'
        rows.append(row)
    rep.table(pd.DataFrame(rows).set_index('pessoa').T, 'bootstrap_blocos_por_pessoa')

    # 2c. bootstrap hierarquico: pessoas com reposicao, depois blocos dentro de cada pessoa sorteada
    nh = args.n_boot
    pick_p = rng.integers(0, P, size=(nh, P))
    pick_r = rng.integers(0, nb, size=(nh, P))
    hier = {}
    for k in ('ap', 'prec', 'rec'):
        stacked = np.stack([store[p][k] for p in persons])  # (P, nb, models)
        hier[k] = np.nanmean(stacked[pick_p, pick_r], axis=1)  # (nh, models)
    rows = []
    for j, m in enumerate(models):
        row = {'modelo': m}
        for k, label in (('ap', 'pr_auc'), ('prec', 'precisao'), ('rec', 'recall')):
            point = person_level[label].unstack('modelo')[m].loc[persons].mean() if label != 'pr_auc' else M[m].mean()
            row[label] = point
            row[f'{label}_ic95'] = fmt_ci(*ci(hier[k][:, j]))
        for other in (BASELINE, REF):
            if other in models and other != m:
                o = models.index(other)
                d = hier['ap'][:, j] - hier['ap'][:, o]
                row[f'dif_pr_auc_vs_{other}'] = M[m].mean() - M[other].mean()
                row[f'dif_vs_{other}_ic95'] = fmt_ci(*ci(d))
                row[f'P(dif>0)_vs_{other}'] = float((d > 0).mean())
        rows.append(row)
    hier_tab = pd.DataFrame(rows).set_index('modelo')
    rep(f'\n2c. Bootstrap HIERARQUICO ({nh} reamostras: pessoas com reposicao -> blocos de tempo dentro de cada '
        'uma). IC95 das medias entre pessoas e das diferencas pareadas (PR-AUC) contra baseline e RF. '
        'Pontos = semente base nos ICs; colunas sem _ic95 = media das sementes.')
    rep.table(hier_tab, 'bootstrap_hierarquico')
    rep.summary['bootstrap_hierarquico'] = hier_tab.reset_index().to_dict(orient='records')

    fig, ax = plt.subplots(figsize=(10, 0.5 * len(models) + 1.5))
    for j, m in enumerate(models):
        lo, hi = ci(hier['ap'][:, j])
        ax.plot([lo, hi], [j, j], color='C0', lw=2)
        ax.plot(M[m].mean(), j, 'o', color='C0')
        ax.scatter(M[m], np.full(P, j) + 0.18, s=12, color='C1', alpha=0.8)
    ax.set_yticks(range(len(models)), models)
    ax.set_xlabel('PR-AUC de ataque (media entre pessoas, IC95 hierarquico; laranja = cada pessoa)')
    ax.axvline(np.mean([y[pidx[p]].mean() for p in persons]), color='grey', ls=':', label='prevalencia (acaso)')
    ax.legend(loc='lower right')
    ax.invert_yaxis()
    rep.fig(fig, 'modelos_pr_auc_ic95')

    # 2d. Wilcoxon pareado + Friedman (unidade = pessoa)
    rep('\n2d. Wilcoxon pareado (signed-rank, exato, bilateral) sobre o PR-AUC das 6 pessoas (media das sementes). '
        'Com n=6 o menor p-valor possivel e 2/64 = 0.031, que exige o mesmo sinal nas 6 pessoas. Tamanho de '
        'efeito: correlacao rank-bisserial r (-1 a 1), diferenca media, mediana e estimador de Hodges-Lehmann. '
        'Holm corrige as comparacoes de cada familia.')
    for metric in ('pr_auc', 'precisao_no_recall_base'):
        Mx = person_level[metric].unstack('modelo')[models].loc[persons]
        for ref in (BASELINE, REF):
            if ref not in models:
                continue
            rows = []
            for m in models:
                if m == ref:
                    continue
                d = (Mx[m] - Mx[ref]).to_numpy()
                try:
                    w = stats.wilcoxon(Mx[m], Mx[ref], method='exact')
                    stat, p = w.statistic, w.pvalue
                except ValueError:
                    stat, p = np.nan, np.nan
                n_pos = int((d > 0).sum())
                rows.append({'modelo': m, 'dif_media': d.mean(), 'dif_mediana': np.median(d),
                             'hodges_lehmann': hodges_lehmann(d), 'vitorias': f'{n_pos}/{P}', 'W': stat,
                             'p_exato': p, 'r_rank_bisserial': rank_biserial(d),
                             'p_sinal': stats.binomtest(n_pos, int((d != 0).sum()), 0.5).pvalue if (d != 0).any() else np.nan})
            t = pd.DataFrame(rows).set_index('modelo')
            t['p_holm'] = holm(t['p_exato'])
            rep(f'\n  metrica {metric}, cada modelo contra {ref}:')
            rep.table(t, f'wilcoxon_{metric}_vs_{ref}')
            if metric == 'pr_auc':
                rep.summary[f'wilcoxon_pr_auc_vs_{ref}'] = t.reset_index().to_dict(orient='records')

    fr = stats.friedmanchisquare(*[M[m] for m in models])
    kendall_w = fr.statistic / (P * (len(models) - 1))
    ranks = M.rank(axis=1, ascending=False).mean().sort_values()
    rep(f'\n  Friedman ({len(models)} modelos x {P} pessoas): chi2 = {fr.statistic:.2f}, p = {fr.pvalue:.2e}, '
        f'W de Kendall = {kendall_w:.3f}. Posto medio (1 = melhor PR-AUC): ' +
        ', '.join(f'{m} {r:.2f}' for m, r in ranks.items()))
    rep.summary['friedman'] = {'chi2': float(fr.statistic), 'p': float(fr.pvalue), 'kendall_w': float(kendall_w),
                               'posto_medio': ranks.round(3).to_dict()}

    # 2e. teste de permutacao dos rotulos
    rep(f'\n2e. Teste de PERMUTACAO ({args.n_perm} permutacoes, semente base): o modelo fica fixo e os rotulos de '
        'ataque sao embaralhados DENTRO de cada pessoa; estatistica = PR-AUC medio entre pessoas. "livre" '
        'embaralha acao por acao; "circular" desloca a sequencia de rotulos de cada (sessao, personagem) por um '
        'deslocamento aleatorio -- preserva as rajadas de ataque (autocorrelacao), entao e o nulo mais exigente. '
        'p = (1 + #nulo >= observado) / (1 + permutacoes).')
    block_of = df.groupby(BLOCK, sort=False).ngroup().to_numpy()
    obs = {m: np.mean([FastAP(preds[m][1][0][pidx[p]])(y[pidx[p]]) for p in persons]) for m in models}
    null = {kind: {m: np.zeros(args.n_perm) for m in models} for kind in ('livre', 'circular')}
    null_person = {m: np.zeros((args.n_perm, P)) for m in (REF, BASELINE) if m in models}
    for pi, person in enumerate(persons):
        te = pidx[person]
        yt = y[te]
        aps = {m: FastAP(preds[m][1][0][te]) for m in models}
        segs = [np.nonzero(block_of[te] == b)[0] for b in np.unique(block_of[te])]
        for start in range(0, args.n_perm, 500):
            stop = min(args.n_perm, start + 500)
            k = stop - start
            free = rng.permuted(np.tile(yt, (k, 1)), axis=1)
            circ = np.empty((k, len(yt)))
            for seg in segs:
                L = len(seg)
                off = rng.integers(1, L, size=k) if L > 1 else np.zeros(k, dtype=int)
                circ[:, seg] = yt[seg][(np.arange(L)[None, :] + off[:, None]) % L]
            for m in models:
                v_free = aps[m](free)
                null['livre'][m][start:stop] += v_free / P
                null['circular'][m][start:stop] += aps[m](circ) / P
                if m in null_person:
                    null_person[m][start:stop, pi] = v_free
    rows = []
    for m in models:
        row = {'modelo': m, 'pr_auc_medio_observado': obs[m]}
        for kind in ('livre', 'circular'):
            nd = null[kind][m]
            row[f'nulo_{kind}_media'] = nd.mean()
            row[f'nulo_{kind}_p99.9'] = np.percentile(nd, 99.9)
            row[f'p_{kind}'] = (1 + (nd >= obs[m]).sum()) / (1 + len(nd))
        rows.append(row)
    rep.table(pd.DataFrame(rows).set_index('modelo'), 'permutacao', fmt='{:.4f}')
    if REF in null_person:
        rep('  por pessoa (RF, nulo livre): ' + ', '.join(
            f'{p}: obs {FastAP(preds[REF][1][0][pidx[p]])(y[pidx[p]]):.3f} vs nulo max '
            f'{null_person[REF][:, i].max():.3f}' for i, p in enumerate(persons)))
    fig, axes = plt.subplots(1, 2, figsize=(12, 4))
    for ax, kind in zip(axes, ('livre', 'circular')):
        for m in [m for m in (BASELINE, REF, 'gru') if m in models]:
            ax.hist(null[kind][m], bins=60, alpha=0.6, label=f'nulo {m}')
            ax.axvline(obs[m], ls='--', color=ax.get_lines()[-1].get_color() if ax.get_lines() else None)
        ax.set_title(f'permutacao {kind}: nulo x observado (tracejado)')
        ax.set_xlabel('PR-AUC medio entre pessoas')
        ax.legend(fontsize=8)
    rep.fig(fig, 'permutacao_nulo')

    # 2f. McNemar
    rep('\n2f. McNemar (acertos/erros pareados nas MESMAS acoes, cada modelo no seu limiar de recall-alvo, '
        'semente base). b = so o 1o acerta; c = so o 2o acerta. As acoes nao sao independentes, entao o p-valor e '
        'otimista; o IC95 da diferenca de acuracia vem do bootstrap por pessoa (6 pessoas). Separado em '
        'acoes de ataque (recall) e de move (alarmes falsos).')
    rows = []
    for a, b in MCNEMAR_PAIRS:
        if a not in models or b not in models:
            continue
        diffs = []
        for person in persons + ['TODAS']:
            te = np.arange(len(y)) if person == 'TODAS' else pidx[person]
            sa, sb = preds[a][0][0], preds[b][0][0]

            def pred_of(m, s):
                if person != 'TODAS':
                    return preds[m][1][0][te] >= thresholds[(m, s, person)]
                return np.concatenate([preds[m][1][0][pidx[q]] >= thresholds[(m, s, q)] for q in persons])
            yt = y[te] if person != 'TODAS' else np.concatenate([y[pidx[q]] for q in persons])
            ca, cb = pred_of(a, sa) == yt, pred_of(b, sb) == yt
            n01, n10 = int((ca & ~cb).sum()), int((~ca & cb).sum())
            res = mcnemar([[0, n01], [n10, 0]], exact=(n01 + n10) < 50, correction=True)
            att, mov = yt == 1, yt == 0
            if person != 'TODAS':
                diffs.append((n01 - n10) / len(yt))
            rows.append({'par': f'{a} x {b}', 'pessoa': person, 'b': n01, 'c': n10, 'p_mcnemar': res.pvalue,
                         'dif_acuracia': (n01 - n10) / len(yt),
                         'b_ataques': int((ca & ~cb & att).sum()), 'c_ataques': int((~ca & cb & att).sum()),
                         'b_moves': int((ca & ~cb & mov).sum()), 'c_moves': int((~ca & cb & mov).sum())})
        d = np.array(diffs)
        lo, hi = ci(d[rng.integers(0, P, size=(args.n_boot, P))].mean(axis=1))
        rows[-1]['dif_acuracia_ic95_pessoas'] = fmt_ci(lo, hi)
    rep.table(pd.DataFrame(rows), 'mcnemar', fmt='{:.4g}', index=False)


# ---------------------------------------------------------------------------
# 3. associacao feature x ataque
# ---------------------------------------------------------------------------
def univariate_effects(x, yy):
    xa, xm = x[yy == 1], x[yy == 0]
    u = stats.mannwhitneyu(xa, xm, alternative='two-sided')
    auc = u.statistic / (len(xa) * len(xm))
    sp = np.sqrt(((len(xa) - 1) * xa.var(ddof=1) + (len(xm) - 1) * xm.var(ddof=1)) / (len(xa) + len(xm) - 2))
    r_pb = stats.pointbiserialr(yy, x)
    return {'media_ataque': xa.mean(), 'media_move': xm.mean(), 'mediana_ataque': np.median(xa),
            'mediana_move': np.median(xm), 'r_ponto_bisserial': r_pb.statistic, 'p_ponto_bisserial': r_pb.pvalue,
            'p_mann_whitney': u.pvalue, 'auc_1_variavel': auc, 'delta_cliff': 2 * auc - 1,
            'd_cohen': (xa.mean() - xm.mean()) / sp if sp > 0 else np.nan}


def section_association(rep, df, y, persons, pidx, results_dir, seed):
    rep.section('3. ASSOCIACAO DE CADA FEATURE COM O ATAQUE')
    rep('p-valores por acao (n grande e acoes dependentes) saem minusculos e otimistas; olhar o tamanho de efeito '
        '(AUC de 1 variavel = P(valor num ataque > valor num move); delta de Cliff = 2*AUC-1; d de Cohen) e se o '
        'sinal se repete nas 6 pessoas.')
    X = df[ALL_FEATURES].astype(float)
    rows = []
    for person in ['TODAS'] + persons:
        te = np.arange(len(y)) if person == 'TODAS' else pidx[person]
        for f in ALL_FEATURES:
            rows.append({'pessoa': person, 'feature': f, **univariate_effects(X[f].to_numpy()[te], y[te])})
    eff = pd.DataFrame(rows)
    for col in ('p_ponto_bisserial', 'p_mann_whitney'):
        eff[col + '_holm'] = eff.groupby('pessoa')[col].transform(lambda s: holm(s.to_numpy()))
    eff.to_csv(os.path.join(rep.out_dir, 'tabelas', 'associacao_univariada_completa.csv'), index=False)
    pooled = eff[eff.pessoa == 'TODAS'].set_index('feature')
    per = eff[eff.pessoa != 'TODAS']
    consist = per.groupby('feature').apply(
        lambda g: pd.Series({'auc_min_pessoas': g.auc_1_variavel.min(), 'auc_max_pessoas': g.auc_1_variavel.max(),
                             'mesmo_sinal_que_agrupado': int((np.sign(g.delta_cliff) ==
                                                              np.sign(pooled.loc[g.name, 'delta_cliff'])).sum())}),
        include_groups=False)
    t = pooled[['media_ataque', 'media_move', 'mediana_ataque', 'mediana_move', 'r_ponto_bisserial',
                'auc_1_variavel', 'delta_cliff', 'd_cohen', 'p_mann_whitney_holm']].join(consist)
    rep('\n3a. Univariada (todas as pessoas juntas; ultimas colunas = variacao entre as 6 pessoas):')
    rep.table(t.sort_values('auc_1_variavel', key=lambda s: -abs(s - 0.5)), 'associacao_univariada', fmt='{:.4g}')

    # 3b. informacao mutua
    rep('\n3b. Informacao mutua (k-vizinhos, nats) e fracao da entropia do rotulo H(Y) que ela explica; pega relacao '
        'nao linear que a correlacao perde.')
    disc = [ALL_FEATURES.index('prev_action_was_attack')]
    rows = []
    for person in ['TODAS'] + persons:
        te = np.arange(len(y)) if person == 'TODAS' else pidx[person]
        mi = mutual_info_classif(X.to_numpy()[te], y[te], discrete_features=disc, random_state=seed)
        prev = y[te].mean()
        h = -(prev * np.log(prev) + (1 - prev) * np.log(1 - prev))
        for f, v in zip(ALL_FEATURES, mi):
            rows.append({'pessoa': person, 'feature': f, 'mi_nats': v, 'mi_fracao_H': v / h})
    mi = pd.DataFrame(rows)
    mi_tab = mi.pivot(index='feature', columns='pessoa', values='mi_fracao_H')[['TODAS'] + persons]
    rep.table(mi_tab.sort_values('TODAS', ascending=False), 'informacao_mutua', fmt='{:.4f}')

    # 3c. importancia por permutacao (do compare_models.py)
    path = os.path.join(results_dir, 'perm_importance.csv')
    if os.path.exists(path):
        imp = pd.read_csv(path)
        rep('\n3c. Importancia por permutacao no jogador de teste de cada fold (queda de PR-AUC ao embaralhar a '
            'feature; media +- dp entre as 6 pessoas). Grupos permutam features correlacionadas juntas.')
        g = imp.groupby(['modelo', 'feature'])['queda_pr_auc_media']
        t = (g.mean().map('{:.4f}'.format) + ' +- ' + g.std(ddof=1).map('{:.4f}'.format)).unstack('modelo')
        order = imp[imp.modelo == REF].groupby('feature')['queda_pr_auc_media'].mean().sort_values(ascending=False).index \
            if REF in imp.modelo.values else t.index
        rep.table(t.loc[order], 'importancia_permutacao')
        fig, ax = plt.subplots(figsize=(10, 6))
        mean = g.mean().unstack('modelo').loc[order[::-1]]
        mean.plot.barh(ax=ax, xerr=g.std(ddof=1).unstack('modelo').loc[order[::-1]])
        ax.set_xlabel('queda de PR-AUC ao permutar (media entre pessoas)')
        rep.fig(fig, 'importancia_permutacao')

    # 3d. tabela 2x2 prev_action_was_attack x ataque
    rep('\n3d. prev_action_was_attack x ataque (2x2): qui-quadrado (com correcao de Yates), Fisher exato, razao de '
        'chances (OR) com IC95; agrupado por Mantel-Haenszel estratificado por pessoa + teste de Breslow-Day '
        '(OR igual entre pessoas?).')
    prev = df['prev_action_was_attack'].to_numpy()
    rows, tables = [], []
    for person in persons + ['TODAS']:
        te = np.arange(len(y)) if person == 'TODAS' else pidx[person]
        tab = np.array([[np.sum((prev[te] == 1) & (y[te] == 1)), np.sum((prev[te] == 1) & (y[te] == 0))],
                        [np.sum((prev[te] == 0) & (y[te] == 1)), np.sum((prev[te] == 0) & (y[te] == 0))]])
        if person != 'TODAS':
            tables.append(tab)
        t2 = Table2x2(tab + (0.5 if (tab == 0).any() else 0))
        chi = stats.chi2_contingency(tab, correction=True)
        rows.append({'pessoa': person, 'P(atk|prev atk)': tab[0, 0] / tab[0].sum(),
                     'P(atk|prev move)': tab[1, 0] / tab[1].sum(), 'OR': t2.oddsratio,
                     'OR_ic95': fmt_ci(*t2.oddsratio_confint()), 'chi2': chi.statistic, 'p_chi2': chi.pvalue,
                     'p_fisher': stats.fisher_exact(tab).pvalue})
    st = StratifiedTable(tables)
    rep.table(pd.DataFrame(rows).set_index('pessoa'), 'prev_ataque_2x2', fmt='{:.4g}')
    bd = st.test_equal_odds()
    rep(f'  Mantel-Haenszel: OR comum = {st.oddsratio_pooled:.2f} {fmt_ci(*st.oddsratio_pooled_confint())}, '
        f'teste CMH p = {st.test_null_odds().pvalue:.2e}; Breslow-Day (OR igual nas 6 pessoas): '
        f'chi2 = {bd.statistic:.2f}, p = {bd.pvalue:.2e}')
    rep.summary['prev_ataque_or_mh'] = float(st.oddsratio_pooled)


# ---------------------------------------------------------------------------
# 4. modelos estatisticos classicos
# ---------------------------------------------------------------------------
def design_matrix(df):
    """features com log nas caudas, continuas padronizadas (OR por 1 dp), binarias cruas (OR de 0 -> 1)."""
    Z = log_features(df[ALL_FEATURES])
    binary = ['never_hurt', 'prev_action_was_attack']
    cont = [c for c in Z.columns if c not in binary]
    Z[cont] = (Z[cont] - Z[cont].mean()) / Z[cont].std(ddof=0)
    return Z


def section_classic(rep, df, y, persons, pidx, person_level, args):
    rep.section('4. MODELOS ESTATISTICOS CLASSICOS')
    Z = design_matrix(df)
    groups = pd.factorize(df['player_id'])[0]

    # 4a. regressao logistica
    rep('\n4a. Regressao logistica (todas as acoes). Continuas com log1p nas caudas e padronizadas: OR = razao de '
        'chances por +1 desvio-padrao; binarias: OR de 0 -> 1. EP classico e EP robusto agrupado por pessoa '
        '(so 6 grupos: o robusto e aproximado).')
    Xc = sm.add_constant(Z)
    fit = sm.Logit(y, Xc).fit(disp=0, maxiter=200)
    fit_cl = sm.Logit(y, Xc).fit(disp=0, maxiter=200, cov_type='cluster', cov_kwds={'groups': groups})
    ci_c, ci_r = fit.conf_int(), fit_cl.conf_int()
    t = pd.DataFrame({'coef': fit.params, 'OR': np.exp(fit.params),
                      'OR_ic95': [fmt_ci(np.exp(a), np.exp(b)) for a, b in ci_c.to_numpy()], 'p': fit.pvalues,
                      'OR_ic95_robusto_pessoa': [fmt_ci(np.exp(a), np.exp(b)) for a, b in ci_r.to_numpy()],
                      'p_robusto': fit_cl.pvalues})
    rep.table(t, 'logistica_or', fmt='{:.4g}')
    rep(f'  pseudo-R2 de McFadden = {fit.prsquared:.3f}, AIC = {fit.aic:.0f}')

    # 4b. efeito fixo de pessoa
    fe = sm.Logit(y, pd.concat([Xc, pd.get_dummies(df['player_id'], prefix='pessoa', drop_first=True, dtype=float)],
                               axis=1)).fit(disp=0, maxiter=200)
    rep(f'\n4b. Mesma logistica + efeito FIXO de pessoa (dummies; referencia {persons[0]}): pseudo-R2 = '
        f'{fe.prsquared:.3f}, AIC = {fe.aic:.0f} (sem pessoa: {fit.aic:.0f}). Teste da razao de verossimilhanca '
        f'(pessoa importa?): G = {2 * (fe.llf - fit.llf):.1f}, gl = {len(persons) - 1}, '
        f'p = {stats.chi2.sf(2 * (fe.llf - fit.llf), len(persons) - 1):.2e}')
    comp = pd.DataFrame({'OR_sem_pessoa': np.exp(fit.params), 'OR_com_efeito_fixo': np.exp(fe.params[fit.params.index])})
    rep.table(comp, 'logistica_or_efeito_fixo', fmt='{:.4g}')

    # 4c. modelo misto (intercepto aleatorio e inclinacao aleatoria de prev_action_was_attack por pessoa)
    rep('\n4c. Logistica MISTA (efeitos aleatorios por pessoa; statsmodels BinomialBayesMixedGLM, Bayes variacional). '
        'Separa o efeito das features da diferenca entre jogadores. ICC latente = s2/(s2 + pi^2/3); MOR = razao de '
        'chances mediana entre duas pessoas sorteadas.')
    d = Z.copy()
    d['attack'] = y
    d['player_id'] = df['player_id'].to_numpy()
    formula = 'attack ~ ' + ' + '.join(Z.columns)
    mixed = {}
    for label, vc in (('intercepto', {'pessoa': '0 + C(player_id)'}),
                      ('intercepto+inclinacao_prev', {'pessoa': '0 + C(player_id)',
                                                      'pessoa_prev': '0 + C(player_id):prev_action_was_attack'})):
        with warnings.catch_warnings():
            warnings.simplefilter('ignore')
            res = BinomialBayesMixedGLM.from_formula(formula, vc, d).fit_vb()
        sd = np.exp(res.vcp_mean)
        mixed[label] = res
        fe_tab = pd.DataFrame({'coef': res.fe_mean, 'dp_post': res.fe_sd, 'OR': np.exp(res.fe_mean),
                               'OR_ic95': [fmt_ci(np.exp(m - 1.96 * s), np.exp(m + 1.96 * s))
                                           for m, s in zip(res.fe_mean, res.fe_sd)]}, index=res.model.exog_names)
        rep(f'\n  modelo "{label}": dp dos efeitos aleatorios = ' +
            ', '.join(f'{n}: {s:.3f}' for n, s in zip(res.model.vcp_names, sd)))
        s2 = sd[0] ** 2
        rep(f'  ICC latente (intercepto) = {s2 / (s2 + np.pi ** 2 / 3):.3f}; MOR = {np.exp(0.6745 * np.sqrt(2 * s2)):.2f}')
        rep.table(fe_tab, f'logistica_mista_{label}', fmt='{:.4g}')
    re = mixed['intercepto'].random_effects()
    re.index = [i.split('[')[-1].rstrip(']').replace('C(player_id)', '').strip('[]') for i in re.index]
    rows = []
    for person in persons:
        key = [k for k in re.index if person in k][0]
        rows.append({'pessoa': person, 'intercepto_aleatorio': re.loc[key, 'Mean'], 'dp_post': re.loc[key, 'SD'],
                     'prevalencia': y[pidx[person]].mean()})
    re_tab = pd.DataFrame(rows).set_index('pessoa')
    rep.summary['mixed_re'] = re_tab.round(4).reset_index().to_dict(orient='records')
    return re_tab


def section_survival(rep, df, y, persons, pidx):
    from lifelines import (CoxPHFitter, ExponentialFitter, KaplanMeierFitter, LogLogisticFitter, LogNormalFitter,
                           WeibullFitter)
    from lifelines.statistics import multivariate_logrank_test, proportional_hazard_test

    rep('\n4d. SOBREVIVENCIA / processo de pontos.')
    # intervalos entre ataques consecutivos do mesmo (sessao, personagem); o ultimo ataque de cada bloco ate a
    # ultima acao do bloco entra como intervalo censurado
    rows = []
    for (_sess, _char), g in df.groupby(BLOCK, sort=False):
        t = g['t'].to_numpy()
        att = t[g['action_type'].to_numpy() == 'attack']
        person = g['player_id'].iloc[0]
        for a, b in zip(att[:-1], att[1:]):
            rows.append({'pessoa': person, 'dur': max(b - a, 1e-3), 'evento': 1})
        if len(att) and t[-1] > att[-1]:
            rows.append({'pessoa': person, 'dur': t[-1] - att[-1], 'evento': 0})
    gaps = pd.DataFrame(rows)
    rep('  Intervalo entre ataques consecutivos: Kaplan-Meier e ajuste parametrico (menor AIC = melhor). Weibull '
        'com forma rho < 1 = risco decrescente (ataques em rajadas: depois de um ataque, outro logo em seguida); '
        'rho > 1 = ritmo regular; rho = 1 = Poisson (sem memoria).')
    out = []
    fig, ax = plt.subplots(figsize=(8, 5))
    for person in persons + ['TODAS']:
        g = gaps if person == 'TODAS' else gaps[gaps.pessoa == person]
        kmf = KaplanMeierFitter().fit(g['dur'], g['evento'], label=person)
        if person != 'TODAS':
            kmf.plot_survival_function(ax=ax, ci_show=False)
        row = {'pessoa': person, 'intervalos': int(g['evento'].sum()), 'censurados': int((g['evento'] == 0).sum()),
               'mediana_KM_s': kmf.median_survival_time_,
               'p25_s': kmf.percentile(0.75), 'p75_s': kmf.percentile(0.25)}
        for name, cls in (('exponencial', ExponentialFitter), ('weibull', WeibullFitter),
                          ('lognormal', LogNormalFitter), ('loglogistica', LogLogisticFitter)):
            try:
                f = cls().fit(g['dur'], g['evento'])
                row[f'AIC_{name}'] = f.AIC_
                if name == 'weibull':
                    lo, hi = f.summary.loc['rho_', ['coef lower 95%', 'coef upper 95%']]
                    row['weibull_rho'] = f.rho_
                    row['weibull_rho_ic95'] = fmt_ci(lo, hi)
            except Exception as e:
                print(f'  (ajuste {name} falhou pra {person}: {e})')
                row[f'AIC_{name}'] = np.nan
        aics = {k[4:]: v for k, v in row.items() if k.startswith('AIC_') and not np.isnan(v)}
        row['melhor_AIC'] = min(aics, key=aics.get) if aics else ''
        out.append(row)
    ax.set_xscale('log')
    ax.set_xlabel('segundos desde o ultimo ataque (escala log)')
    ax.set_ylabel('P(ainda nao atacou de novo)')
    ax.set_title('Kaplan-Meier do intervalo entre ataques, por pessoa')
    rep.fig(fig, 'km_intervalo_entre_ataques')
    rep.table(pd.DataFrame(out).set_index('pessoa'), 'sobrevivencia_intervalos', fmt='{:.4g}')
    lr = multivariate_logrank_test(gaps['dur'], gaps['pessoa'], gaps['evento'])
    rep(f'  log-rank entre as 6 pessoas: chi2 = {lr.test_statistic:.1f}, p = {lr.p_value:.2e}')

    # risco especifico de ataque em funcao do tempo desde a ultima acao (riscos competitivos: move censura)
    rep('\n  Risco ESPECIFICO de ataque x tempo desde a ultima acao (time_since_last_action; a proxima acao ser um '
        'move censura o ataque). Tabela de vida: P(proxima acao e ataque e acontece nesta faixa | nenhuma acao '
        'ate o inicio da faixa), separado pela acao anterior. Mostra o "ritmo" que o modelo explora.')
    dur = df['time_since_last_action'].clip(lower=1e-3).to_numpy()
    prev = df['prev_action_was_attack'].to_numpy()
    edges = [0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 1.0, 1.25, 1.5, 2, 3, 5, 10, np.inf]
    rows = []
    for lo, hi in zip(edges[:-1], edges[1:]):
        row = {'faixa_s': f'[{lo}, {hi})'}
        for pv, lab in ((1, 'prev_ataque'), (0, 'prev_move')):
            m = prev == pv
            at_risk = (dur[m] >= lo).sum()
            in_bin = m & (dur >= lo) & (dur < hi)
            row[f'{lab}_em_risco'] = int(at_risk)
            row[f'{lab}_risco_ataque'] = (y[in_bin] == 1).sum() / at_risk if at_risk else np.nan
            row[f'{lab}_risco_move'] = (y[in_bin] == 0).sum() / at_risk if at_risk else np.nan
        rows.append(row)
    life = pd.DataFrame(rows).set_index('faixa_s')
    rep.table(life, 'risco_ataque_tempo_desde_acao', fmt='{:.4g}')
    fig, ax = plt.subplots(figsize=(9, 4.5))
    mids = [(a + min(b, 20)) / 2 for a, b in zip(edges[:-1], edges[1:])]
    for lab in ('prev_ataque', 'prev_move'):
        ax.plot(mids, life[f'{lab}_risco_ataque'], 'o-', label=f'risco de ataque ({lab})')
        ax.plot(mids, life[f'{lab}_risco_move'], 's:', label=f'risco de move ({lab})', alpha=0.6)
    ax.set_xscale('log')
    ax.set_xlabel('segundos desde a acao anterior (meio da faixa, log)')
    ax.set_ylabel('risco condicional na faixa')
    ax.legend(fontsize=8)
    rep.fig(fig, 'risco_ataque_tempo_desde_acao')

    cox_df = pd.DataFrame({'dur': dur, 'evento': y, 'prev_action_was_attack': prev,
                           'player_id': df['player_id'].to_numpy(),
                           'bloco': df.groupby(BLOCK, sort=False).ngroup().to_numpy()})
    cph = CoxPHFitter().fit(cox_df, duration_col='dur', event_col='evento', strata=['player_id'], cluster_col='bloco')
    s = cph.summary.loc['prev_action_was_attack']
    rep(f'  Cox de risco especifico de ataque, estratificado por pessoa, EP robusto por (sessao, personagem): '
        f'HR(prev_action_was_attack) = {s["exp(coef)"]:.2f} [{s["exp(coef) lower 95%"]:.2f}, '
        f'{s["exp(coef) upper 95%"]:.2f}], p = {s["p"]:.2e}')
    try:
        ph = proportional_hazard_test(cph, cox_df, time_transform='rank')
        rep(f'  teste de riscos proporcionais (Schoenfeld, tempo em postos): p = {float(ph.p_value[0]):.2e} '
            '(p pequeno = o efeito de prev_action_was_attack muda com o tempo decorrido; ver a tabela de vida acima)')
    except Exception as e:
        rep(f'  (teste de riscos proporcionais falhou: {e})')


def section_markov(rep, df, y, persons, pidx):
    rep('\n4e. CADEIA DE MARKOV sobre a sequencia move/ataque de cada (sessao, personagem). Ordem k = depende das '
        'ultimas k acoes. Comparacao de ordens nas linhas com 5 acoes de historico (mesmas linhas pra todas as '
        'ordens), por BIC e razao de verossimilhanca. O desempenho preditivo (LOPO) das ordens 1, 2, 3 e 5 esta '
        'na secao 1 (markov_ordemK).')
    prev = df.groupby(BLOCK, sort=False)['prev_action_was_attack']
    lags = np.column_stack([prev.shift(j).fillna(2).astype(int).to_numpy() for j in range(5)])  # col j = lag j+1
    full = (lags != 2).all(axis=1)
    yy, L = y[full], lags[full]
    rows, ll_prev = [], None
    for k in range(0, 6):
        code = np.zeros(len(yy), dtype=int)
        for j in range(k):
            code = code * 2 + L[:, j]
        n = np.bincount(code, minlength=2 ** k)
        a = np.bincount(code, weights=yy, minlength=2 ** k)
        p = np.divide(a, n, out=np.zeros(len(n)), where=n > 0)
        with np.errstate(divide='ignore', invalid='ignore'):
            ll = np.nansum(a * np.log(p)) + np.nansum((n - a) * np.log(1 - p))
        params = int((n > 0).sum())
        row = {'ordem': k, 'parametros': params, 'loglik': ll, 'BIC': -2 * ll + params * np.log(len(yy))}
        if ll_prev is not None:
            g = 2 * (ll - ll_prev)
            row['G_vs_ordem_anterior'] = g
            row['p_RV'] = stats.chi2.sf(g, 2 ** (k - 1))
        rows.append(row)
        ll_prev = ll
    t = pd.DataFrame(rows).set_index('ordem')
    rep(f'  {len(yy)} linhas com historico completo.')
    rep.table(t, 'markov_ordem', fmt='{:.4g}')

    # homogeneidade entre pessoas (ordem 1 e 2): mesma matriz de transicao pra todo mundo?
    for k in (1, 2):
        g_tot, df_tot = 0.0, 0
        code = np.zeros(len(df), dtype=int)
        for j in range(k):
            code = code * 3 + lags[:, j]
        for c in np.unique(code[(lags[:, :k] != 2).all(axis=1)]):
            m = code == c
            tab = pd.crosstab(df['player_id'][m], y[m]).to_numpy()
            if tab.shape[1] == 2 and tab.shape[0] > 1:
                g = stats.chi2_contingency(tab, lambda_='log-likelihood')
                g_tot += g.statistic
                df_tot += g.dof
        rep(f'  homogeneidade entre pessoas, ordem {k} (G somado sobre os historicos): G = {g_tot:.1f}, gl = {df_tot}, '
            f'p = {stats.chi2.sf(g_tot, df_tot):.2e}')

    rows = []
    for person in persons + ['TODAS']:
        te = np.arange(len(y)) if person == 'TODAS' else pidx[person]
        l1 = lags[te, 0]
        pa = y[te][l1 == 1].mean()
        pm = y[te][l1 == 0].mean()
        # tamanho observado das rajadas de ataque (sequencias de ataques consecutivos)
        runs = []
        for _, g in df.iloc[te].groupby(BLOCK, sort=False):
            a = (g['action_type'] == 'attack').to_numpy().astype(int)
            edges = np.diff(np.r_[0, a, 0])
            runs += list(np.nonzero(edges == -1)[0] - np.nonzero(edges == 1)[0])
        pi_att = pm / (pm + 1 - pa)
        runs = np.array(runs)
        # Markov de ordem 1 => tamanho da rajada ~ geometrica(1 - P(atk|atk)). A MEDIA bate por construcao
        # (P(atk|atk) estimado nos mesmos dados); o que testa a ordem 1 e a FORMA da distribuicao.
        rows.append({'pessoa': person, 'P(atk|atk)': pa, 'P(atk|move)': pm, 'P(move|atk)': 1 - pa,
                     'estacionaria_ataque': pi_att, 'rajadas': len(runs), 'rajada_media': runs.mean(),
                     'P(rajada=1)_geom': 1 - pa, 'P(rajada=1)_obs': (runs == 1).mean(),
                     'mediana_geom': int(np.ceil(np.log(0.5) / np.log(pa))) if 0 < pa < 1 else np.nan,
                     'mediana_obs': np.median(runs),
                     'P(rajada>=5)_geom': pa ** 4, 'P(rajada>=5)_obs': (runs >= 5).mean()})
    rep('\n  Transicoes de ordem 1 por pessoa. Rajada = ataques seguidos. Se a sequencia fosse Markov de ordem 1, '
        'o tamanho da rajada seria geometrico (_geom); _obs = observado. A media coincide por construcao, entao '
        'a comparacao e na FORMA: mais rajadas de 1 E mais rajadas longas que a geometrica = memoria mais longa '
        '(mistura de ataques isolados com sequencias de ataque continuo).')
    rep.table(pd.DataFrame(rows).set_index('pessoa'), 'markov_transicoes', fmt='{:.4g}')


# ---------------------------------------------------------------------------
# 5. calibracao e limiar
# ---------------------------------------------------------------------------
def section_calibration(rep, df, y, persons, pidx, preds, thresholds, re_tab, args):
    rep.section('5. CALIBRACAO E LIMIAR')
    models = [m for m in preds if m != BASELINE]
    n = len(y)
    rep('5a. Calibracao (semente base, cada pessoa avaliada com o modelo que nao a viu). Brier skill = 1 - '
        'Brier/Brier(prevalencia dos outros jogadores); ECE = erro de calibracao esperado (10 faixas iguais e 15 '
        'faixas de mesma contagem); inclinacao ideal = 1 (< 1: probabilidades extremas demais), calibracao-na-media '
        'ideal = 0 (> 0: subestima o risco). "platt"/"isotonica" = recalibrado com as predicoes OOF dos OUTROS 5 '
        'jogadores (sem olhar a pessoa de teste). RF usa class_weight=balanced e LSTM/GRU usam pos_weight, entao '
        'as probabilidades cruas DELES sao infladas de proposito.')
    recal = {}
    rows = []
    for m in models:
        raw = preds[m][1][0]
        platt, iso = np.zeros(n), np.zeros(n)
        for person in persons:
            te = pidx[person]
            tr = np.setdiff1d(np.arange(n), te)
            lr = sm.Logit(y[tr], sm.add_constant(safe_logit(raw[tr]))).fit(disp=0)
            platt[te] = lr.predict(sm.add_constant(safe_logit(raw[te]), has_constant='add'))
            iso[te] = IsotonicRegression(out_of_bounds='clip', y_min=0, y_max=1).fit(raw[tr], y[tr]).predict(raw[te])
        recal[m] = {'cru': raw, 'platt': platt, 'isotonica': iso}
        for kind, p in recal[m].items():
            per = []
            for person in persons:
                te = pidx[person]
                tr = np.setdiff1d(np.arange(n), te)
                b_ref = brier_score_loss(y[te], np.full(len(te), y[tr].mean()))
                slope, inter = calibration_slope_intercept(y[te], p[te])
                per.append({'brier': brier_score_loss(y[te], p[te]),
                            'brier_skill': 1 - brier_score_loss(y[te], p[te]) / b_ref,
                            'ece10': ece(y[te], p[te], 10), 'ece15q': ece(y[te], p[te], 15, 'quantile'),
                            'inclinacao': slope, 'calib_na_media': inter})
            per = pd.DataFrame(per)
            rows.append({'modelo': m, 'versao': kind, **per.mean().to_dict(),
                         'ece10_dp_pessoas': per['ece10'].std(ddof=1)})
    cal = pd.DataFrame(rows).set_index(['modelo', 'versao'])
    rep.table(cal, 'calibracao', fmt='{:.4f}')

    fig, axes = plt.subplots(1, 2, figsize=(13, 5.5))
    for ax, kind in zip(axes, ('cru', 'isotonica')):
        for m in [m for m in FIG_MODELS if m in recal]:
            p = recal[m][kind]
            edges = np.unique(np.quantile(p, np.linspace(0, 1, 16)))
            idx = np.clip(np.searchsorted(edges, p, side='right') - 1, 0, len(edges) - 2)
            mp = [p[idx == b].mean() for b in range(len(edges) - 1) if (idx == b).any()]
            my = [y[idx == b].mean() for b in range(len(edges) - 1) if (idx == b).any()]
            ax.plot(mp, my, 'o-', ms=3, label=m)
        ax.plot([0, 1], [0, 1], 'k:', lw=1)
        ax.set_xlabel('probabilidade prevista (media da faixa)')
        ax.set_ylabel('frequencia observada de ataque')
        ax.set_title(f'diagrama de confiabilidade ({kind}; 15 faixas de mesma contagem)')
        ax.legend(fontsize=7)
    rep.fig(fig, 'calibracao_confiabilidade')

    # 5b. limiar global x limiar/calibracao por pessoa numa janela inicial de cada sessao
    rep(f'\n5b. Limiar por pessoa numa JANELA INICIAL de cada sessao (recall-alvo {args.target_recall}). Pra cada '
        '(pessoa, sessao): os primeiros W minutos de jogo servem pra escolher o limiar (e recalibrar, Platt); o '
        f'resto da sessao e avaliado. Janela com menos de {MIN_WINDOW_ATTACKS} ataques volta pro limiar global. '
        '"global" = limiar escolhido nos outros jogadores (secao 1), avaliado no MESMO resto. |rec-alvo| = '
        'distancia media do recall atingido ate o alvo; dp_rec = dispersao do recall entre pessoas.')
    t_rel = df['t'] - df.groupby(['player_id', 'session_id'])['t'].transform('min')
    rows = []
    for W in CALIB_WINDOWS_MIN:
        win = (t_rel <= W * 60).to_numpy()
        for m in models:
            raw = preds[m][1][0]
            s = preds[m][0][0]
            res = {'global': [], 'janela': [], 'janela_platt_brier': [], 'global_platt_brier': []}
            n_fallback = 0
            for person in persons:
                te = pidx[person]
                rest_all, pred_g, pred_w, yy, p_platt_w, p_platt_g = [], [], [], [], [], []
                for sess in df['session_id'].iloc[te].unique():
                    rows_s = te[(df['session_id'].to_numpy()[te] == sess)]
                    w_idx, r_idx = rows_s[win[rows_s]], rows_s[~win[rows_s]]
                    if not len(r_idx):
                        continue
                    thr_g = thresholds[(m, s, person)]
                    if y[w_idx].sum() >= MIN_WINDOW_ATTACKS and (y[w_idx] == 0).any():
                        thr_w = pick_threshold(y[w_idx], raw[w_idx], args.target_recall)
                        lr = sm.Logit(y[w_idx], sm.add_constant(safe_logit(raw[w_idx]))).fit(disp=0, maxiter=200)
                        pw = lr.predict(sm.add_constant(safe_logit(raw[r_idx]), has_constant='add'))
                    else:
                        thr_w, pw = thr_g, recal[m]['platt'][r_idx]
                        n_fallback += 1
                    pred_g.append(raw[r_idx] >= thr_g)
                    pred_w.append(raw[r_idx] >= thr_w)
                    yy.append(y[r_idx])
                    p_platt_w.append(pw)
                    p_platt_g.append(recal[m]['platt'][r_idx])
                if not yy:
                    continue
                yy = np.concatenate(yy)
                for key, pr in (('global', np.concatenate(pred_g)), ('janela', np.concatenate(pred_w))):
                    tp = (pr & (yy == 1)).sum()
                    res[key].append(((tp / pr.sum()) if pr.sum() else 0.0, tp / max((yy == 1).sum(), 1)))
                res['janela_platt_brier'].append(brier_score_loss(yy, np.concatenate(p_platt_w)))
                res['global_platt_brier'].append(brier_score_loss(yy, np.concatenate(p_platt_g)))
            for key in ('global', 'janela'):
                arr = np.array(res[key])
                rows.append({'janela_min': W, 'modelo': m, 'estrategia': key, 'precisao': arr[:, 0].mean(),
                             'recall': arr[:, 1].mean(), 'dp_rec': arr[:, 1].std(ddof=1),
                             '|rec-alvo|': np.abs(arr[:, 1] - args.target_recall).mean(),
                             'brier_platt': np.mean(res[f'{key}_platt_brier']),
                             'sessoes_sem_ataques_suficientes': n_fallback if key == 'janela' else 0})
    win_tab = pd.DataFrame(rows).set_index(['janela_min', 'modelo', 'estrategia'])
    rep.table(win_tab, 'limiar_janela_inicial', fmt='{:.4f}')

    # 5c. limiar "oraculo" por pessoa x intercepto aleatorio do modelo misto
    if re_tab is not None and REF in preds:
        raw = preds[REF][1][0]
        oracle = {p: pick_threshold(y[pidx[p]], raw[pidx[p]], args.target_recall) for p in persons}
        re_tab = re_tab.copy()
        re_tab['limiar_rf_oraculo'] = pd.Series(oracle)
        rho = stats.spearmanr(re_tab['intercepto_aleatorio'], re_tab['limiar_rf_oraculo'])
        rep(f'\n5c. Limiar da RF que daria exatamente recall {args.target_recall} em cada pessoa (oraculo, '
            'descritivo) x intercepto aleatorio da pessoa no modelo misto (secao 4c). Spearman (n=6, so '
            f'descritivo): rho = {rho.statistic:.2f}, p = {rho.pvalue:.3f}')
        rep.table(re_tab, 'limiar_oraculo_x_intercepto', fmt='{:.4f}')

    # 5d. curva de decisao
    rep('\n5d. Curva de DECISAO (ganho liquido). Limiar de probabilidade pt <=> um ataque perdido custa '
        '(1-pt)/pt alarmes falsos (pt=0.2: 1 ataque perdido = 4 alarmes falsos). Ganho liquido = VP/N - FP/N * '
        'pt/(1-pt), em "ataques corretamente antecipados por acao, ja descontados os alarmes falsos". Modelos com '
        'probabilidade isotonica recalibrada (sem olhar a pessoa de teste); "avisar sempre" e "nunca" sao as '
        'referencias; a regra do baseline tem um unico ponto de operacao.')
    pts = np.round(np.arange(0.02, 0.81, 0.01), 2)
    prev = y.mean()
    base = df['prev_action_was_attack'].to_numpy()
    dca = {'avisar_sempre': prev - (1 - prev) * pts / (1 - pts), 'nunca': np.zeros(len(pts)),
           BASELINE: np.array([((base == 1) & (y == 1)).sum() / n - ((base == 1) & (y == 0)).sum() / n * pt / (1 - pt)
                               for pt in pts])}
    for m in models:
        p = recal[m]['isotonica']
        dca[m] = np.array([((p >= pt) & (y == 1)).sum() / n - ((p >= pt) & (y == 0)).sum() / n * pt / (1 - pt)
                           for pt in pts])
    dca = pd.DataFrame(dca, index=pts)
    dca.index.name = 'pt'
    dca.to_csv(os.path.join(rep.out_dir, 'tabelas', 'curva_decisao_completa.csv'))
    rep.table(dca.loc[[0.05, 0.1, 0.15, 0.2, 0.3, 0.4, 0.5]], 'curva_decisao', fmt='{:.4f}')
    fig, ax = plt.subplots(figsize=(9, 5.5))
    for col in ['avisar_sempre', 'nunca'] + [m for m in FIG_MODELS if m in dca]:
        ax.plot(dca.index, dca[col], label=col, lw=1.8 if col in (REF, 'gru', 'xgboost') else 1,
                ls=':' if col in ('avisar_sempre', 'nunca') else '-')
    ax.set_ylim(-0.01, prev * 1.05)
    ax.set_xlabel('limiar de probabilidade pt')
    ax.set_ylabel('ganho liquido (por acao)')
    ax.legend(fontsize=7)
    rep.fig(fig, 'curva_decisao')


# ---------------------------------------------------------------------------
# 6. PCA e alternativas
# ---------------------------------------------------------------------------
def dunn_test(x, g):
    """post-hoc de Dunn (aprox. normal, correcao de empates), todos os pares."""
    ranks = stats.rankdata(x)
    N = len(x)
    _, counts = np.unique(x, return_counts=True)
    sigma2 = N * (N + 1) / 12 - (counts ** 3 - counts).sum() / (12 * (N - 1))
    levels = sorted(np.unique(g))
    rbar = {lv: ranks[g == lv].mean() for lv in levels}
    nn = {lv: (g == lv).sum() for lv in levels}
    out = []
    for a, b in itertools.combinations(levels, 2):
        z = (rbar[a] - rbar[b]) / np.sqrt(sigma2 * (1 / nn[a] + 1 / nn[b]))
        u = stats.mannwhitneyu(x[g == a], x[g == b]).statistic
        out.append({'par': f'{a}-{b}', 'z': z, 'p': 2 * stats.norm.sf(abs(z)), 'P(a>b)': u / (nn[a] * nn[b])})
    out = pd.DataFrame(out)
    out['p_holm'] = holm(out['p'])
    return out


def section_pca_alternatives(rep, df, y, persons, pidx, seed):
    rep.section('6. PCA E ALTERNATIVAS (estrutura das features e diferenca entre pessoas)')
    X = df[ALL_FEATURES].astype(float)
    Xs = StandardScaler().fit_transform(X)
    g = df['player_id'].to_numpy()

    # 6a. PCA
    pca = PCA().fit(Xs)
    scores = pca.transform(Xs)
    ev = pd.DataFrame({'variancia_explicada': pca.explained_variance_ratio_,
                       'acumulada': np.cumsum(pca.explained_variance_ratio_)},
                      index=[f'PC{i + 1}' for i in range(len(ALL_FEATURES))])
    rep('6a. PCA (features padronizadas, todas as pessoas). Sem redundancia forte, a variancia se espalha por '
        'muitos componentes.')
    rep.table(ev, 'pca_variancia')
    rep(f'  componentes pra 85% da variancia: {int(np.searchsorted(ev["acumulada"], 0.85) + 1)}')
    load = pd.DataFrame(pca.components_.T, index=ALL_FEATURES, columns=ev.index)
    rep.table(load, 'pca_cargas')
    sd = scores.std(axis=0)
    sep = pd.DataFrame([(scores[pidx[p]][y[pidx[p]] == 1].mean(axis=0) - scores[pidx[p]][y[pidx[p]] == 0].mean(axis=0)) / sd
                        for p in persons], index=persons, columns=ev.index)
    rep('  separacao ataque - move por pessoa, em desvios-padrao de cada PC (mesmos eixos pra todos):')
    rep.table(sep, 'pca_separacao_ataque_move')
    means = pd.DataFrame([scores[pidx[p]].mean(axis=0) / sd for p in persons], index=persons, columns=ev.index)
    rep('  score medio de cada pessoa (em dp do PC):')
    rep.table(means, 'pca_media_por_pessoa')
    rows = []
    for p in persons:
        pp = PCA().fit(StandardScaler().fit_transform(X.iloc[pidx[p]]))
        top = lambda k: ', '.join(f'{ALL_FEATURES[i]} {pp.components_[k][i]:+.2f}'
                                  for i in np.argsort(-np.abs(pp.components_[k]))[:3])
        rows.append({'pessoa': p, 'var_PC1': pp.explained_variance_ratio_[0], 'var_PC2': pp.explained_variance_ratio_[1],
                     'var_PC3': pp.explained_variance_ratio_[2],
                     'PCs_p_85%': int(np.searchsorted(np.cumsum(pp.explained_variance_ratio_), 0.85) + 1),
                     'PC1': top(0), 'PC2': top(1)})
    rep('  PCA separada por pessoa (eixos proprios):')
    rep.table(pd.DataFrame(rows).set_index('pessoa'), 'pca_por_pessoa')
    fig, axes = plt.subplots(1, 2, figsize=(13, 4.5))
    axes[0].bar(ev.index, ev['variancia_explicada'])
    axes[0].plot(ev.index, ev['acumulada'], 'o-', color='C1')
    axes[0].set_title('PCA: variancia explicada (barras) e acumulada')
    im = axes[1].imshow(load.to_numpy(), cmap='RdBu_r', vmin=-1, vmax=1, aspect='auto')
    axes[1].set_xticks(range(len(ev)), ev.index)
    axes[1].set_yticks(range(len(ALL_FEATURES)), ALL_FEATURES)
    axes[1].set_title('cargas')
    fig.colorbar(im, ax=axes[1])
    rep.fig(fig, 'pca')

    # 6b. Spearman + VIF
    rho, p = stats.spearmanr(X)
    rho = pd.DataFrame(rho, index=ALL_FEATURES, columns=ALL_FEATURES)
    per = np.mean([stats.spearmanr(X.iloc[pidx[q]]).statistic for q in persons], axis=0)
    off = ~np.eye(len(ALL_FEATURES), dtype=bool)
    rep('\n6b. Correlacao de Spearman (todas as acoes):')
    rep.table(rho, 'spearman')
    pd.DataFrame(per, index=ALL_FEATURES, columns=ALL_FEATURES).to_csv(
        os.path.join(rep.out_dir, 'tabelas', 'spearman_media_por_pessoa.csv'))
    i, j = np.unravel_index(np.argmax(np.where(off, np.abs(rho.to_numpy()), 0)), rho.shape)
    rep(f'  maior |rho| fora da diagonal: {ALL_FEATURES[i]} x {ALL_FEATURES[j]} = {rho.iloc[i, j]:.3f}; '
        f'|rho| medio = {np.abs(rho.to_numpy()[off]).mean():.3f}')
    vif = pd.Series([variance_inflation_factor(sm.add_constant(Xs), k + 1) for k in range(len(ALL_FEATURES))],
                    index=ALL_FEATURES, name='VIF')
    rep('  VIF (fator de inflacao da variancia; > 5-10 = colinearidade preocupante): ' +
        ', '.join(f'{k} {v:.2f}' for k, v in vif.items()))
    vif.to_csv(os.path.join(rep.out_dir, 'tabelas', 'vif.csv'))
    fig, ax = plt.subplots(figsize=(7, 6))
    im = ax.imshow(rho.to_numpy(), cmap='RdBu_r', vmin=-1, vmax=1)
    ax.set_xticks(range(len(ALL_FEATURES)), ALL_FEATURES, rotation=60, ha='right')
    ax.set_yticks(range(len(ALL_FEATURES)), ALL_FEATURES)
    for a in range(len(ALL_FEATURES)):
        for b in range(len(ALL_FEATURES)):
            ax.text(b, a, f'{rho.iloc[a, b]:.2f}', ha='center', va='center', fontsize=7)
    fig.colorbar(im)
    ax.set_title('Spearman')
    rep.fig(fig, 'spearman')

    # 6c. Kruskal-Wallis, ANOVA de Welch, Dunn
    rep('\n6c. Diferenca de ESTILO entre pessoas, feature a feature: Kruskal-Wallis (epsilon2 = fracao da variacao dos '
        'postos explicada pela pessoa; ~0.01 pequeno, ~0.08 medio, >0.26 grande) e ANOVA de Welch (omega2). '
        'Post-hoc de Dunn com Holm em tabelas/dunn_*.csv; abaixo, o par P03 x P06.')
    rows, dunn_all = [], []
    k = len(persons)
    for f in ALL_FEATURES:
        x = X[f].to_numpy()
        h = stats.kruskal(*[x[g == q] for q in persons])
        w = anova_oneway(x, g, use_var='unequal')
        grand = x.mean()
        ssb = sum((g == q).sum() * (x[g == q].mean() - grand) ** 2 for q in persons)
        sst = ((x - grand) ** 2).sum()
        msw = (sst - ssb) / (len(x) - k)
        rows.append({'feature': f, 'H': h.statistic, 'p_KW': h.pvalue, 'epsilon2': (h.statistic - k + 1) / (len(x) - k),
                     'F_welch': w.statistic, 'p_welch': w.pvalue, 'omega2': (ssb - (k - 1) * msw) / (sst + msw)})
        d = dunn_test(x, g)
        d.insert(0, 'feature', f)
        dunn_all.append(d)
    rep.table(pd.DataFrame(rows).set_index('feature'), 'kruskal_anova_pessoas', fmt='{:.4g}')
    dunn_all = pd.concat(dunn_all)
    dunn_all.to_csv(os.path.join(rep.out_dir, 'tabelas', 'dunn_todos_os_pares.csv'), index=False)
    pair = dunn_all[dunn_all.par.isin(['P03-P06', 'P06-P03'])]
    if len(pair):
        rep.table(pair.set_index('feature'), 'dunn_P03_P06', fmt='{:.4g}')

    # 6d. MANOVA
    d = pd.DataFrame(Xs, columns=ALL_FEATURES)
    d['pessoa'] = g
    mv = MANOVA.from_formula(' + '.join(ALL_FEATURES) + ' ~ C(pessoa)', d).mv_test()
    st = mv.results['C(pessoa)']['stat']
    s_dim = min(len(ALL_FEATURES), k - 1)
    pillai = st.loc["Pillai's trace"]
    rep(f'\n6d. MANOVA (9 features juntas ~ pessoa): Pillai = {pillai["Value"]:.3f} (efeito = Pillai/s = '
        f'{pillai["Value"] / s_dim:.3f}, s = {s_dim}), F = {pillai["F Value"]:.1f}, p = {pillai["Pr > F"]:.2e}; '
        f'Wilks lambda = {st.loc["Wilks\' lambda", "Value"]:.3f}')

    # 6e. LDA
    Z = StandardScaler().fit_transform(log_features(df[ALL_FEATURES]))
    zcols = list(log_features(df[ALL_FEATURES].iloc[:2]).columns)
    lda = LinearDiscriminantAnalysis().fit(Z, y)
    ld = lda.transform(Z)[:, 0]
    if ld[y == 1].mean() < ld[y == 0].mean():
        ld = -ld
        coef = -lda.scalings_[:, 0]
    else:
        coef = lda.scalings_[:, 0]
    struct = [np.corrcoef(Z[:, i], ld)[0, 1] for i in range(Z.shape[1])]
    t = pd.DataFrame({'coef_padronizado': coef, 'coef_estrutura(corr)': struct}, index=zcols)
    sep = (ld[y == 1].mean() - ld[y == 0].mean()) / np.sqrt((ld[y == 1].var() + ld[y == 0].var()) / 2)
    rep(f'\n6e. LDA ataque x move (features com log, padronizadas): o eixo que MAIS separa as duas classes (a PCA '
        f'nao procura isso). Separacao no eixo LD1: d = {sep:.2f}; AUC de LD1 (in-sample) = '
        f'{roc_auc_score(y, ld):.3f}. PR-AUC fora da amostra (LOPO) da LDA: ver secao 1 (lda). Coeficiente de '
        'estrutura = correlacao da feature com o eixo (mais estavel que o coeficiente quando ha correlacao).')
    rep.table(t.sort_values('coef_estrutura(corr)', key=abs, ascending=False), 'lda_ataque_move')

    # LDA entre pessoas: quao "identificavel" e o estilo? validacao cruzada em blocos de tempo
    order = df.assign(_i=np.arange(len(df))).sort_values(['player_id', 'session_id', 't'])['_i'].to_numpy()
    fold = np.zeros(len(df), dtype=int)
    for q in persons:
        rows_q = order[g[order] == q]
        fold[rows_q] = np.minimum((np.arange(len(rows_q)) * 5) // len(rows_q), 4)
    pred = np.empty(len(df), dtype=object)
    for f in range(5):
        tr, te = fold != f, fold == f
        pred[te] = LinearDiscriminantAnalysis().fit(Z[tr], g[tr]).predict(Z[te])
    bacc = balanced_accuracy_score(g, pred)
    cm = pd.DataFrame(confusion_matrix(g, pred, labels=persons, normalize='true'), index=persons, columns=persons)
    lda_p = LinearDiscriminantAnalysis().fit(Z, g)
    rep(f'\n  LDA entre PESSOAS (6 classes), validacao cruzada em 5 blocos contiguos de tempo de cada pessoa: '
        f'acuracia balanceada = {bacc:.3f} (acaso = {1 / k:.3f}). Variancia discriminante explicada por LD1..LD3 = '
        + ', '.join(f'{v:.2f}' for v in lda_p.explained_variance_ratio_[:3]) + '. Matriz de confusao (linhas = '
        'pessoa real, normalizada):')
    rep.table(cm, 'lda_pessoas_confusao')
    rep.summary['lda_pessoas_acuracia_balanceada'] = float(bacc)

    fig, axes = plt.subplots(1, 2, figsize=(13, 5))
    bins = np.linspace(np.percentile(ld, 0.5), np.percentile(ld, 99.5), 60)
    axes[0].hist(ld[y == 0], bins=bins, density=True, alpha=0.6, label='move')
    axes[0].hist(ld[y == 1], bins=bins, density=True, alpha=0.6, label='ataque')
    axes[0].set_title('LDA ataque x move: eixo LD1')
    axes[0].legend()
    proj = lda_p.transform(Z)
    rng = np.random.default_rng(seed)
    for q in persons:
        idx = rng.choice(pidx[q], size=min(800, len(pidx[q])), replace=False)
        axes[1].scatter(proj[idx, 0], proj[idx, 1], s=4, alpha=0.5, label=q)
    axes[1].set_title('LDA entre pessoas (LD1 x LD2, amostra de 800 acoes por pessoa)')
    axes[1].legend(markerscale=3)
    rep.fig(fig, 'lda')


# ---------------------------------------------------------------------------
# 7. curva de aprendizado e janela K
# ---------------------------------------------------------------------------
def section_learning(rep, results_dir):
    path = os.path.join(results_dir, 'learning_curve.csv')
    if os.path.exists(path):
        rep.section('7. CURVA DE APRENDIZADO (mais dados ajudariam?)')
        lc = pd.read_csv(path)
        npes = lc[lc.curva == 'n_pessoas']
        # media sobre os subconjuntos de pessoas dentro de cada jogador de teste, depois media/dp entre jogadores
        per = npes.groupby(['modelo', 'n_pessoas', 'jogador_teste'])['pr_auc'].mean()
        t = per.groupby(['modelo', 'n_pessoas']).agg(['mean', 'std']).unstack('modelo')
        rep('PR-AUC no jogador de teste x NUMERO DE PESSOAS no treino (media de todas as combinacoes possiveis de '
            'pessoas; media e dp entre os 6 jogadores de teste):')
        rep.table(t, 'curva_aprendizado_n_pessoas', fmt='{:.4f}')
        for m in per.index.get_level_values('modelo').unique():
            pm = per.loc[m].unstack('n_pessoas')
            gain = (pm[pm.columns.max()] - pm[pm.columns.max() - 1])
            rho = stats.spearmanr(np.tile(pm.columns, len(pm)), pm.to_numpy().ravel())
            rep(f'  {m}: ganho medio de {pm.columns.max() - 1} -> {pm.columns.max()} pessoas = {gain.mean():+.4f} '
                f'(melhora em {int((gain > 0).sum())}/{len(gain)} jogadores de teste); Spearman(n_pessoas, PR-AUC) = '
                f'{rho.statistic:.2f}')
        frac = lc[lc.curva == 'fracao_do_tempo'].groupby(['modelo', 'fracao'])['pr_auc'].agg(['mean', 'std']).unstack('modelo')
        rep('\nPR-AUC x FRACAO do tempo de jogo de cada pessoa de treino (as 5 outras pessoas; inicio de cada sessao):')
        rep.table(frac, 'curva_aprendizado_fracao', fmt='{:.4f}')
        fig, axes = plt.subplots(1, 2, figsize=(13, 4.5))
        for m in per.index.get_level_values('modelo').unique():
            mm = per.loc[m].groupby('n_pessoas')
            axes[0].errorbar(mm.mean().index, mm.mean(), yerr=mm.std(), capsize=3, marker='o', label=m)
            fm = lc[(lc.curva == 'fracao_do_tempo') & (lc.modelo == m)].groupby('fracao')['pr_auc']
            axes[1].errorbar(fm.mean().index, fm.mean(), yerr=fm.std(), capsize=3, marker='o', label=m)
        axes[0].set_xlabel('pessoas no treino')
        axes[1].set_xlabel('fracao do tempo de jogo de cada pessoa de treino')
        for ax in axes:
            ax.set_ylabel('PR-AUC no jogador de teste')
            ax.legend()
        rep.fig(fig, 'curva_aprendizado')

    path = os.path.join(results_dir, 'k_sweep.csv')
    if os.path.exists(path):
        ks = pd.read_csv(path)
        main_runs = os.path.join(results_dir, 'sequence_runs.csv')
        if os.path.exists(main_runs):
            r = pd.read_csv(main_runs)
            r = r[(r.modelo.isin(['lstm', 'gru'])) & (r.semente == r.semente.min())]
            ks = pd.concat([ks, r])
        t = ks.groupby(['modelo', 'seq_len'])['pr_auc_teste'].agg(['mean', 'std']).unstack('modelo')
        rep('\nTamanho da janela K de LSTM/GRU (1 semente; PR-AUC medio e dp entre as 6 pessoas):')
        rep.table(t, 'janela_k', fmt='{:.4f}')


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--dataset', default='dataset.csv')
    ap.add_argument('--results-dir', default='resultados', help='onde estao oof_*.csv.gz (saida vai pra <dir>/estatistica)')
    ap.add_argument('--out-dir', default=None)
    ap.add_argument('--target-recall', type=float, default=0.7)
    ap.add_argument('--n-boot', type=int, default=10000)
    ap.add_argument('--n-boot-blocks', type=int, default=2000)
    ap.add_argument('--n-perm', type=int, default=10000)
    ap.add_argument('--seed', type=int, default=42)
    args = ap.parse_args()

    out_dir = args.out_dir or os.path.join(args.results_dir, 'estatistica')
    rep = Report(out_dir)
    t0 = time.time()
    df = load_dataset(args.dataset)
    y = (df['action_type'] == 'attack').to_numpy(dtype=int)
    persons = sorted(df['player_id'].unique())
    pidx = {p: np.nonzero(df['player_id'].to_numpy() == p)[0] for p in persons}
    preds = load_predictions(args.results_dir, len(df))
    folds_path = os.path.join(args.results_dir, 'folds_tabular.json')
    folds_info = json.load(open(folds_path)) if os.path.exists(folds_path) else None

    # confere a PR-AUC rapida contra o sklearn antes de usar no bootstrap/permutacao
    chk = preds[REF][1][0] if REF in preds else next(iter(preds.values()))[1][0]
    w = np.random.default_rng(0).integers(0, 3, len(y))
    assert abs(FastAP(chk)(y) - average_precision_score(y, chk)) < 1e-9
    assert abs(FastAP(chk)(y, w) - average_precision_score(y, chk, sample_weight=w)) < 1e-9

    rep(f'statistical_analysis.py | dataset {args.dataset} | modelos: ' +
        ', '.join(f'{m} ({len(s)} sem.)' for m, (s, _) in preds.items()))
    long, person_level, thresholds = section_models(rep, df, y, persons, pidx, preds, args.target_recall, folds_info)
    fig_pr_curves(rep, y, persons, pidx, preds)
    section_uncertainty(rep, df, y, persons, pidx, preds, person_level, thresholds, args)
    section_association(rep, df, y, persons, pidx, args.results_dir, args.seed)
    re_tab = section_classic(rep, df, y, persons, pidx, person_level, args)
    section_survival(rep, df, y, persons, pidx)
    section_markov(rep, df, y, persons, pidx)
    section_calibration(rep, df, y, persons, pidx, preds, thresholds, re_tab, args)
    section_pca_alternatives(rep, df, y, persons, pidx, args.seed)
    section_learning(rep, args.results_dir)

    with open(os.path.join(out_dir, 'resumo.json'), 'w') as f:
        json.dump(rep.summary, f, indent=2, default=float)
    rep(f'\nok em {time.time() - t0:.0f}s -> {out_dir}')


if __name__ == '__main__':
    main()
