"""
export_onnx.py
---------------------------------------------------------------------------
Fase 7: exporta o pipeline treinado (model.pkl, da Tarefa 1 de
train_model.py -- classificar o tipo da proxima acao) pra ONNX, e valida
que a saida do ONNX bate com a do sklearn original antes de considerar
pronto pro cliente (Fase 8, ManaVerse + ONNX Runtime C++).

v2: desde que prev_action_type virou prev_action_was_attack (0/1) no
build_dataset.py, o pipeline inteiro e numerico -- um UNICO tensor float
de entrada, shape [None, 13]. Isso evita StringTensorType no ONNX Runtime
C++ (API bem mais chata e arriscada de acertar sem poder testar o cliente
de verdade) -- decisao tomada pensando direto na Fase 8.

Uso:
    python export_onnx.py --model model.pkl --dataset dataset.csv --out model.onnx
"""
import argparse

import joblib
import numpy as np
import onnxruntime as ort
import pandas as pd
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType

ALL_FEATURES = [
    'x', 'y', 'hp', 'max_hp', 'hp_ratio', 'velocity_x', 'velocity_y', 'speed',
    'time_since_last_action', 'time_since_last_hurt', 'last_damage_taken', 'dead',
    'prev_action_was_attack',
]


def export(pipe, out_path):
    initial_types = [('input', FloatTensorType([None, len(ALL_FEATURES)]))]
    onnx_model = convert_sklearn(
        pipe, initial_types=initial_types,
        options={id(pipe.named_steps['clf']): {'zipmap': False}},  # saida = array simples, nao lista de dicts
    )
    with open(out_path, 'wb') as f:
        f.write(onnx_model.SerializeToString())
    return onnx_model


def verify_onnx(pipe, onnx_path, X_sample):
    sk_labels = pipe.predict(X_sample)
    sk_probs = pipe.predict_proba(X_sample)

    sess = ort.InferenceSession(onnx_path)
    input_name = sess.get_inputs()[0].name
    onnx_labels, onnx_probs = sess.run(None, {input_name: X_sample.to_numpy(dtype=np.float32)})

    labels_match = bool((sk_labels == onnx_labels).all())
    max_prob_diff = float(np.abs(sk_probs - onnx_probs).max())
    return labels_match, max_prob_diff


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--model', default='model.pkl')
    ap.add_argument('--dataset', default='dataset.csv', help='usado so pra pegar linhas de exemplo pra validacao')
    ap.add_argument('--out', default='model.onnx')
    ap.add_argument('--n-verify', type=int, default=200, help='quantas linhas amostrar pra validar')
    ap.add_argument('--seed', type=int, default=42)
    args = ap.parse_args()

    pipe = joblib.load(args.model)
    print(f'modelo carregado: {args.model}')

    onnx_model = export(pipe, args.out)
    print(f'exportado: {args.out} ({len(onnx_model.SerializeToString()) / 1e6:.1f} MB)')
    print(f'entrada esperada: 1 tensor float32, shape [N, {len(ALL_FEATURES)}], colunas na ordem: {ALL_FEATURES}')

    df = pd.read_csv(args.dataset)
    df['dead'] = df['dead'].astype(int)
    sample = df.sample(min(args.n_verify, len(df)), random_state=args.seed)
    X_sample = sample[ALL_FEATURES]

    labels_match, max_prob_diff = verify_onnx(pipe, args.out, X_sample)
    print(f'\nvalidacao (n={len(X_sample)} linhas):')
    print(f'  labels identicos ao sklearn: {labels_match}')
    print(f'  diferenca maxima nas probabilidades: {max_prob_diff:.2e}')

    if not labels_match or max_prob_diff > 1e-3:
        raise SystemExit('ATENCAO: a saida do ONNX diverge do sklearn alem do esperado por '
                          'arredondamento de ponto flutuante -- nao confie neste model.onnx.')
    print('\nOK -- model.onnx pronto pra Fase 8 (ONNX Runtime C++ no ManaVerse).')


if __name__ == '__main__':
    main()
