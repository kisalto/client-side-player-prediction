"""
export_results_json.py
---------------------------------------------------------------------------
Junta num UNICO JSON os resultados de compare_models.py, train_sequence_model.py,
statistical_analysis.py e mixed_model_mcmc.py (tabelas CSV + resumos), com
metadados pra reproduzir a rodada (hash do dataset, sementes, configuracao).
NaN vira null (o JSON padrao nao aceita NaN). As predicoes linha a linha
(oof_*.csv.gz) ficam de fora -- sao grandes e ja estao nos proprios arquivos.

Uso:
    python export_results_json.py --dataset dataset.csv --results-dir resultados/ [--out resultados.json]
"""
import argparse
import datetime
import hashlib
import json
import math
import os

import pandas as pd

# tabela (nome do CSV em estatistica/tabelas, sem .csv) -> secao do JSON
SECTIONS = {
    'comparacao_modelos': ['resumo_modelos', 'pr_auc_por_pessoa', 'metricas_por_modelo_semente_pessoa',
                           'limiar_rf_sensibilidade'],
    'incerteza_e_testes': ['bootstrap_pessoa', 'bootstrap_blocos_por_pessoa', 'bootstrap_hierarquico',
                           'wilcoxon_pr_auc_vs_baseline_repete_anterior', 'wilcoxon_pr_auc_vs_random_forest',
                           'wilcoxon_precisao_no_recall_base_vs_baseline_repete_anterior',
                           'wilcoxon_precisao_no_recall_base_vs_random_forest', 'permutacao', 'mcnemar'],
    'associacao_features': ['associacao_univariada', 'associacao_univariada_completa', 'informacao_mutua',
                            'importancia_permutacao', 'prev_ataque_2x2'],
    'modelos_classicos': ['logistica_or', 'logistica_or_efeito_fixo', 'logistica_mista_intercepto',
                          'logistica_mista_intercepto+inclinacao_prev', 'sobrevivencia_intervalos',
                          'risco_ataque_tempo_desde_acao', 'markov_ordem', 'markov_transicoes'],
    'calibracao_e_limiar': ['calibracao', 'limiar_janela_inicial', 'limiar_oraculo_x_intercepto', 'curva_decisao',
                            'curva_decisao_completa'],
    'pca_e_alternativas': ['pca_variancia', 'pca_cargas', 'pca_separacao_ataque_move', 'pca_media_por_pessoa',
                           'pca_por_pessoa', 'spearman', 'spearman_media_por_pessoa', 'vif', 'kruskal_anova_pessoas',
                           'dunn_todos_os_pares', 'dunn_P03_P06', 'lda_ataque_move', 'lda_pessoas_confusao'],
}
# tabelas com cabecalho de 2 linhas (unstack): recalculadas abaixo a partir dos CSVs brutos
SKIP = {'curva_aprendizado_n_pessoas', 'curva_aprendizado_fracao', 'janela_k'}


def clean(obj):
    """NaN/inf -> None; floats arredondados (6 casas significativas bastam e o JSON fica legivel)."""
    if isinstance(obj, dict):
        return {str(k): clean(v) for k, v in obj.items()}
    if isinstance(obj, (list, tuple)):
        return [clean(v) for v in obj]
    if isinstance(obj, float):
        return None if not math.isfinite(obj) else float(f'{obj:.6g}')
    if hasattr(obj, 'item'):  # escalares numpy
        return clean(obj.item())
    return obj


def read_table(path):
    df = pd.read_csv(path)
    return df.rename(columns={c: 'item' for c in df.columns if c.startswith('Unnamed')}).to_dict(orient='records')


def read_json_nan(path):
    # resumo.json do statistical_analysis.py pode ter NaN (json.dump aceita, json.load tambem)
    with open(path) as f:
        return json.load(f)


def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--dataset', default='dataset.csv')
    ap.add_argument('--results-dir', default='resultados')
    ap.add_argument('--out', default=None, help='default: <results-dir>/resultados_estatisticos.json')
    args = ap.parse_args()

    R = args.results_dir
    stat = os.path.join(R, 'estatistica')
    tab_dir = os.path.join(stat, 'tabelas')
    mcmc_dir = os.path.join(stat, 'mcmc')
    out_path = args.out or os.path.join(R, 'resultados_estatisticos.json')

    df = pd.read_csv(args.dataset)
    is_att = df['action_type'] == 'attack'
    meta = {
        'gerado_em': datetime.datetime.now().astimezone().isoformat(timespec='seconds'),
        'dataset': {
            'arquivo': os.path.abspath(args.dataset), 'sha256': sha256(args.dataset), 'acoes': len(df),
            'ataques': int(is_att.sum()), 'prevalencia_ataque': float(is_att.mean()),
            'pessoas': sorted(df['player_id'].astype(str).unique()), 'personagens': int(df['char_id'].nunique()),
            'sessoes': int(df['session_id'].nunique()),
            'por_pessoa': df.assign(ataque=is_att).groupby('player_id').agg(
                acoes=('ataque', 'size'), ataques=('ataque', 'sum'), prevalencia=('ataque', 'mean')
            ).reset_index().to_dict(orient='records'),
        },
        'protocolo': {
            'validacao': 'leave-one-player-out por pessoa (6 folds); ataque = classe positiva',
            'metrica_principal': 'PR-AUC (average precision) de ataque, media entre pessoas',
            'limiar': 'recall-alvo 0.7 nas predicoes OOF dos outros 5 jogadores (baseline: regra fixa)',
            'unidade_de_inferencia': 'pessoa (n=6); bootstrap hierarquico pessoa -> blocos de 60 s',
        },
    }
    for name, key in (('folds_tabular.json', 'folds_tabular'), ('sequence_config.json', 'config_sequencia')):
        path = os.path.join(R, name)
        if os.path.exists(path):
            meta[key] = read_json_nan(path)

    out = {'metadados': meta}
    resumo_path = os.path.join(stat, 'resumo.json')
    if os.path.exists(resumo_path):
        resumo = read_json_nan(resumo_path)
        out['destaques'] = {k: resumo[k] for k in ('friedman', 'prev_ataque_or_mh', 'lda_pessoas_acuracia_balanceada')
                            if k in resumo}

    used = set()
    for section, names in SECTIONS.items():
        out[section] = {}
        for n in names:
            path = os.path.join(tab_dir, f'{n}.csv')
            if os.path.exists(path):
                out[section][n] = read_table(path)
                used.add(n)
    others = sorted(f[:-4] for f in os.listdir(tab_dir) if f.endswith('.csv') and f[:-4] not in used | SKIP)
    if others:
        out['outras_tabelas'] = {n: read_table(os.path.join(tab_dir, f'{n}.csv')) for n in others}

    # curva de aprendizado e janela K, recalculadas dos CSVs brutos (formato longo, sem cabecalho duplo)
    lc_path = os.path.join(R, 'learning_curve.csv')
    if os.path.exists(lc_path):
        lc = pd.read_csv(lc_path)
        npes = lc[lc.curva == 'n_pessoas'].groupby(['modelo', 'n_pessoas', 'jogador_teste'])['pr_auc'].mean()
        out['curva_aprendizado'] = {
            'por_n_pessoas': npes.groupby(['modelo', 'n_pessoas']).agg(['mean', 'std']).rename(
                columns={'mean': 'pr_auc_media', 'std': 'pr_auc_dp_entre_jogadores'}).reset_index().to_dict(orient='records'),
            'por_fracao_do_tempo': lc[lc.curva == 'fracao_do_tempo'].groupby(['modelo', 'fracao'])['pr_auc'].agg(
                ['mean', 'std']).rename(columns={'mean': 'pr_auc_media', 'std': 'pr_auc_dp_entre_jogadores'}
                                        ).reset_index().to_dict(orient='records'),
            'bruto': lc.to_dict(orient='records'),
        }
    seq_runs = os.path.join(R, 'sequence_runs.csv')
    if os.path.exists(seq_runs):
        runs = pd.read_csv(seq_runs)
        out['comparacao_modelos']['treinos_sequencia'] = runs.to_dict(orient='records')
        ks_path = os.path.join(R, 'k_sweep.csv')
        if os.path.exists(ks_path):
            ks = pd.concat([pd.read_csv(ks_path),
                            runs[runs.modelo.isin(['lstm', 'gru']) & (runs.semente == runs.semente.min())]])
            out['janela_k'] = ks.groupby(['modelo', 'seq_len'])['pr_auc_teste'].agg(['mean', 'std']).rename(
                columns={'mean': 'pr_auc_media', 'std': 'pr_auc_dp_entre_pessoas'}).reset_index().to_dict(orient='records')
    imp_path = os.path.join(R, 'perm_importance.csv')
    if os.path.exists(imp_path):
        out['associacao_features']['importancia_permutacao_por_fold'] = pd.read_csv(imp_path).to_dict(orient='records')

    if os.path.isdir(mcmc_dir):
        mc = {}
        if os.path.exists(os.path.join(mcmc_dir, 'mcmc_resumo.json')):
            mc['diagnosticos_e_resumo'] = read_json_nan(os.path.join(mcmc_dir, 'mcmc_resumo.json'))
        for f in sorted(os.listdir(mcmc_dir)):
            stem = f[:-4]
            for suffix in ('_efeitos_fixos', '_efeitos_aleatorios', '_resumo_posterior'):
                if f.endswith('.csv') and stem.endswith(suffix):
                    mc.setdefault(stem[:-len(suffix)], {})[suffix[1:]] = read_table(os.path.join(mcmc_dir, f))
        mc['configuracao'] = '4 cadeias NUTS x (2000 burn-in descartado + 2000 amostras), target_accept 0.99, ' \
                             'init adapt_diag, parametrizacao nao centrada (ver mixed_model_mcmc.py)'
        out['modelo_misto_mcmc'] = mc

    with open(out_path, 'w') as f:
        json.dump(clean(out), f, indent=1, ensure_ascii=False, allow_nan=False)
    n_tab = sum(len(v) for v in out.values() if isinstance(v, dict))
    print(f'{out_path}: {os.path.getsize(out_path) / 1024:.0f} KB, secoes: {", ".join(out)} ({n_tab} itens)')


if __name__ == '__main__':
    main()
