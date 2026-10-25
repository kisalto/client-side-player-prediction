"""
mixed_model_mcmc.py
---------------------------------------------------------------------------
Logistica mista (efeito aleatorio por pessoa) ajustada por MCMC (NUTS, PyMC),
como conferencia do ajuste variacional (BinomialBayesMixedGLM.fit_vb) da secao
4c do statistical_analysis.py. Mesmo desenho: features com log nas caudas,
continuas padronizadas (coeficiente por +1 dp), binarias cruas (0 -> 1).

Modelos
  M1  intercepto aleatorio por pessoa
  M2  intercepto + inclinacao aleatoria de prev_action_was_attack por pessoa
  M2s M2 com priori mais larga nos desvios-padrao dos efeitos aleatorios --
      sensibilidade: com so 6 pessoas a priori de sigma pesa no resultado

Parametrizacao NAO centrada (efeito_p = sigma * z_p, z_p ~ N(0, 1)): com
poucos grupos e sigma pequeno, a centrada forma um "funil" que trava o NUTS
(divergencias). Prioris fracamente informativas: beta ~ N(0, 2.5) (escala
padronizada), intercepto ~ N(0, 5), sigma ~ HalfNormal(1) [M2s: HalfNormal(2.5)].

Burn-in: as --tune iteracoes iniciais de cada cadeia (adaptacao do passo e da
matriz de massa do NUTS) sao descartadas; so as --draws seguintes entram no
resultado. Diagnosticos: R-hat < 1.01, ESS bulk/tail > 400, 0 divergencias,
E-BFMI > 0.3 (Vehtari et al. 2021).

Uso:
    python mixed_model_mcmc.py --dataset dataset.csv --out-dir resultados/estatistica/mcmc \
        [--chains 4 --tune 2000 --draws 2000]
"""
import argparse
import json
import os
import time
import warnings

# 1 thread de BLAS por cadeia: cada cadeia e um processo; sem isso cada uma abre
# varias threads e 4 cadeias disputam a CPU entre si (medido: carga 48 em 12 nucleos)
for _var in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS'):
    os.environ.setdefault(_var, '1')

import matplotlib
matplotlib.use('Agg')
import arviz as az
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
import pymc as pm
from statsmodels.genmod.bayes_mixed_glm import BinomialBayesMixedGLM

from statistical_analysis import design_matrix
from train_model import load_dataset

PREV = 'prev_action_was_attack'
MODELS = {
    'M1_intercepto': {'slope': False, 'sigma_prior': 1.0},
    'M2_intercepto_inclinacao_prev': {'slope': True, 'sigma_prior': 1.0},
    'M2s_priori_larga': {'slope': True, 'sigma_prior': 2.5},
}


def build_model(Z, y, person_idx, n_persons, slope, sigma_prior):
    with pm.Model(coords={'feature': list(Z.columns), 'pessoa': list(range(n_persons))}) as model:
        alpha = pm.Normal('intercepto', 0.0, 5.0)
        beta = pm.Normal('beta', 0.0, 2.5, dims='feature')
        sigma_a = pm.HalfNormal('sigma_intercepto', sigma_prior)
        z_a = pm.Normal('z_intercepto', 0.0, 1.0, dims='pessoa')
        a_p = pm.Deterministic('efeito_intercepto', sigma_a * z_a, dims='pessoa')
        eta = alpha + a_p[person_idx] + pm.math.dot(Z.to_numpy(), beta)
        if slope:
            sigma_b = pm.HalfNormal('sigma_inclinacao_prev', sigma_prior)
            z_b = pm.Normal('z_inclinacao_prev', 0.0, 1.0, dims='pessoa')
            b_p = pm.Deterministic('efeito_inclinacao_prev', sigma_b * z_b, dims='pessoa')
            eta = eta + b_p[person_idx] * Z[PREV].to_numpy()
        pm.Bernoulli('ataque', logit_p=eta, observed=y)
    return model


def eti(a):
    return float(np.percentile(a, 2.5)), float(np.percentile(a, 97.5))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--dataset', default='dataset.csv')
    ap.add_argument('--out-dir', default='resultados/estatistica/mcmc')
    ap.add_argument('--chains', type=int, default=4)
    ap.add_argument('--tune', type=int, default=2000, help='burn-in por cadeia (descartado)')
    ap.add_argument('--draws', type=int, default=2000, help='amostras guardadas por cadeia')
    # 0.95 deixou 10 divergencias no M2s; com 0.99 o passo fica menor (mais lento, mais seguro)
    ap.add_argument('--target-accept', type=float, default=0.99)
    # 'jitter+adapt_diag' (padrao do PyMC) sorteia o ponto inicial; com 55 mil acoes a verossimilhanca e tao
    # concentrada que uma cadeia que comeca longe trava (rodada de 2026-10-25: 1 de 4 cadeias presa, R-hat 1.53)
    ap.add_argument('--init', default='adapt_diag')
    ap.add_argument('--seed', type=int, default=42)
    ap.add_argument('--models', nargs='+', default=list(MODELS), choices=list(MODELS))
    args = ap.parse_args()

    os.makedirs(args.out_dir, exist_ok=True)
    report = open(os.path.join(args.out_dir, 'mcmc_report.txt'), 'w')

    def log(msg=''):
        print(msg, flush=True)
        report.write(msg + '\n')
        report.flush()

    df = load_dataset(args.dataset)
    y = (df['action_type'] == 'attack').to_numpy(dtype=int)
    Z = design_matrix(df)
    persons = sorted(df['player_id'].unique())
    person_idx = df['player_id'].map({p: i for i, p in enumerate(persons)}).to_numpy()
    log(f'{len(df)} acoes, {y.sum()} ataques, {len(persons)} pessoas ({", ".join(persons)}) | '
        f'{args.chains} cadeias x ({args.tune} burn-in descartado + {args.draws} amostras) | '
        f'target_accept {args.target_accept}')

    summary_json = {}
    for name in args.models:
        cfg = MODELS[name]
        log('\n' + '=' * 78 + f'\n{name}  (inclinacao aleatoria de prev: {cfg["slope"]}; '
            f'sigma ~ HalfNormal({cfg["sigma_prior"]}))\n' + '=' * 78)
        model = build_model(Z, y, person_idx, len(persons), cfg['slope'], cfg['sigma_prior'])
        t0 = time.time()
        n_iter, total_iter = 0, args.chains * (args.tune + args.draws)

        def progress(trace, draw):
            # chamado a cada iteracao de cada cadeia (burn-in incluso); mcmc_progress.txt alimenta o contador
            nonlocal n_iter
            n_iter += 1
            if n_iter % 200 == 0 or n_iter == total_iter:
                with open(os.path.join(args.out_dir, 'mcmc_progress.txt'), 'w') as f:
                    f.write(f'{name} {n_iter} {total_iter} {time.time() - t0:.0f}\n')

        with model:
            idata = pm.sample(draws=args.draws, tune=args.tune, chains=args.chains, cores=args.chains,
                              target_accept=args.target_accept, random_seed=args.seed, progressbar=False,
                              init=args.init, callback=progress)
        elapsed = time.time() - t0

        post = idata.posterior
        stats_ = idata.sample_stats
        divergences = int(stats_['diverging'].values.sum())
        energy = stats_['energy'].values  # (cadeias, amostras)
        bfmi = (np.diff(energy, axis=1) ** 2).mean(axis=1) / energy.var(axis=1)
        var_names = ['intercepto', 'beta', 'sigma_intercepto', 'efeito_intercepto']
        if cfg['slope']:
            var_names += ['sigma_inclinacao_prev', 'efeito_inclinacao_prev']
        with warnings.catch_warnings():
            warnings.simplefilter('ignore')
            summ = az.summary(idata, var_names=var_names, ci_kind='eti', ci_prob=0.95)
        summ = pd.DataFrame(summ)
        summ.to_csv(os.path.join(args.out_dir, f'{name}_resumo_posterior.csv'))
        rhat_max = float(summ['r_hat'].max())
        ess_min = float(min(summ['ess_bulk'].min(), summ['ess_tail'].min()))
        ok = rhat_max < 1.01 and ess_min > 400 and divergences == 0 and bfmi.min() > 0.3
        log(f'tempo {elapsed / 60:.1f} min | divergencias {divergences} | R-hat max {rhat_max:.4f} | '
            f'ESS min (bulk/tail) {ess_min:.0f} | E-BFMI por cadeia {", ".join(f"{b:.2f}" for b in bfmi)} | '
            f'diagnosticos {"OK" if ok else "COM PROBLEMA -- nao usar sem investigar"}')

        # efeitos fixos como razao de chances (IC = intervalo de credibilidade 95%, percentis)
        beta = post['beta'].values.reshape(-1, Z.shape[1])
        alpha = post['intercepto'].values.ravel()
        rows = [{'termo': 'intercepto', 'coef_media': alpha.mean(), 'coef_dp': alpha.std(),
                 'OR': np.exp(np.median(alpha)), 'OR_IC95': '[%.4g, %.4g]' % tuple(np.exp(eti(alpha)))}]
        for j, f in enumerate(Z.columns):
            b = beta[:, j]
            rows.append({'termo': f, 'coef_media': b.mean(), 'coef_dp': b.std(), 'OR': np.exp(np.median(b)),
                         'OR_IC95': '[%.4g, %.4g]' % tuple(np.exp(eti(b))), 'P(coef>0)': (b > 0).mean()})
        fe = pd.DataFrame(rows).set_index('termo')
        fe.to_csv(os.path.join(args.out_dir, f'{name}_efeitos_fixos.csv'))
        log('\nefeitos fixos (OR = exp da mediana a posteriori; IC95 de credibilidade):')
        log(fe.to_string(float_format=lambda v: f'{v:.4g}'))

        sa = post['sigma_intercepto'].values.ravel()
        icc = sa ** 2 / (sa ** 2 + np.pi ** 2 / 3)
        mor = np.exp(0.6745 * np.sqrt(2) * sa)
        log(f'\nsigma do intercepto por pessoa: mediana {np.median(sa):.3f} IC95 [{eti(sa)[0]:.3f}, {eti(sa)[1]:.3f}]')
        log(f'ICC latente: mediana {np.median(icc):.3f} IC95 [{eti(icc)[0]:.3f}, {eti(icc)[1]:.3f}] | '
            f'MOR: mediana {np.median(mor):.2f} IC95 [{eti(mor)[0]:.2f}, {eti(mor)[1]:.2f}]')
        re_rows = []
        ea = post['efeito_intercepto'].values.reshape(-1, len(persons))
        eb = post['efeito_inclinacao_prev'].values.reshape(-1, len(persons)) if cfg['slope'] else None
        for i, p in enumerate(persons):
            r = {'pessoa': p, 'intercepto_media': ea[:, i].mean(),
                 'intercepto_IC95': '[%.3f, %.3f]' % eti(ea[:, i]), 'P(intercepto>0)': (ea[:, i] > 0).mean()}
            if eb is not None:
                r.update({'inclinacao_prev_media': eb[:, i].mean(), 'inclinacao_prev_IC95': '[%.3f, %.3f]' % eti(eb[:, i])})
            re_rows.append(r)
        re = pd.DataFrame(re_rows).set_index('pessoa')
        re.to_csv(os.path.join(args.out_dir, f'{name}_efeitos_aleatorios.csv'))
        log('\nefeito aleatorio de cada pessoa (desvio em log-odds em relacao a pessoa media):')
        log(re.to_string(float_format=lambda v: f'{v:.3f}'))
        if cfg['slope']:
            sb = post['sigma_inclinacao_prev'].values.ravel()
            log(f'sigma da inclinacao de prev por pessoa: mediana {np.median(sb):.3f} '
                f'IC95 [{eti(sb)[0]:.3f}, {eti(sb)[1]:.3f}]')

        # tracos e densidades por cadeia dos parametros de variancia + 2 efeitos fixos principais
        show = [('intercepto', alpha.reshape(args.chains, -1)), ('sigma_intercepto', post['sigma_intercepto'].values),
                (PREV, post['beta'].sel(feature=PREV).values),
                ('log_time_since_last_hurt', post['beta'].sel(feature='log_time_since_last_hurt').values)]
        if cfg['slope']:
            show.append(('sigma_inclinacao_prev', post['sigma_inclinacao_prev'].values))
        fig, axes = plt.subplots(len(show), 2, figsize=(12, 2.3 * len(show)))
        for (label, v), (ax_t, ax_d) in zip(show, axes):
            for c in range(v.shape[0]):
                ax_t.plot(v[c], lw=0.4, alpha=0.7)
                ax_d.hist(v[c], bins=50, histtype='step', density=True)
            ax_t.set_title(f'{label} (traco por cadeia, apos burn-in)', fontsize=9)
            ax_d.set_title(f'{label} (posterior por cadeia)', fontsize=9)
        fig.tight_layout()
        fig.savefig(os.path.join(args.out_dir, f'{name}_tracos.png'), dpi=110)
        plt.close(fig)

        summary_json[name] = {'tempo_min': elapsed / 60, 'divergencias': divergences, 'rhat_max': rhat_max,
                              'ess_min': ess_min, 'bfmi_min': float(bfmi.min()), 'diagnosticos_ok': bool(ok),
                              'sigma_intercepto_mediana': float(np.median(sa)), 'icc_mediana': float(np.median(icc)),
                              'or_prev_mediana': float(np.exp(np.median(beta[:, list(Z.columns).index(PREV)])))}

    # comparacao com o ajuste variacional (mesmo desenho, mesma formula da secao 4c)
    d = Z.copy()
    d['attack'] = y
    d['player_id'] = df['player_id'].to_numpy()
    with warnings.catch_warnings():
        warnings.simplefilter('ignore')
        vb = BinomialBayesMixedGLM.from_formula('attack ~ ' + ' + '.join(Z.columns),
                                                {'pessoa': '0 + C(player_id)'}, d).fit_vb()
    log('\n' + '=' * 78 + '\nVariacional (secao 4c) x MCMC (M1), mesmo modelo de intercepto aleatorio\n' + '=' * 78)
    if 'M1_intercepto' in args.models:
        m1 = pd.read_csv(os.path.join(args.out_dir, 'M1_intercepto_efeitos_fixos.csv'), index_col=0)
        comp = pd.DataFrame({'OR_variacional': np.exp(vb.fe_mean), 'OR_mcmc': m1['OR'].to_numpy(),
                             'OR_IC95_mcmc': m1['OR_IC95'].to_numpy()}, index=m1.index)
        log(comp.to_string(float_format=lambda v: f'{v:.4g}'))
        log(f'sigma do intercepto: variacional {np.exp(vb.vcp_mean[0]):.3f} x MCMC '
            f'{summary_json["M1_intercepto"]["sigma_intercepto_mediana"]:.3f}')
    with open(os.path.join(args.out_dir, 'mcmc_resumo.json'), 'w') as f:
        json.dump(summary_json, f, indent=2)


if __name__ == '__main__':
    main()
