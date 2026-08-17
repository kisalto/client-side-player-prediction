"""
export_onnx.py
---------------------------------------------------------------------------
Converte o modelo Scikit-Learn (.pkl) para formato ONNX (.onnx) para
poder ser rodado no navegador (ex: usando onnxruntime-web no jogo).

Uso:
    python export_onnx.py --model model.pkl --dataset dataset.csv --out model.onnx
"""
import argparse
import joblib
import pandas as pd
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--model', default='model.pkl', help='Modelo treinado pelo train_model.py')
    ap.add_argument('--dataset', default='dataset.csv', help='Dataset original (usado só pra ler o formato das colunas)')
    ap.add_argument('--out', default='model.onnx', help='Caminho de saída para o ONNX')
    args = ap.parse_args()

    print(f"Carregando modelo {args.model}...")
    clf = joblib.load(args.model)

    # Lemos apenas a primeira linha do dataset para saber o número exato
    # de features que o modelo espera receber
    df = pd.read_csv(args.dataset, nrows=1)
    meta_cols = ['session_id', 't_ms', 'frame_f', 'label', 'n_deaths_in_subsession']
    feature_cols = [c for c in df.columns if c not in meta_cols]
    n_features = len(feature_cols)

    print(f"O modelo espera {n_features} features de entrada (float32).")

    # Define o tipo da entrada (batch size dinâmico None, numero de features)
    initial_type = [('float_input', FloatTensorType([None, n_features]))]

    # Desativa o ZipMap. Isso garante que a saída seja um tensor limpo [P(0), P(1)]
    # em vez de uma lista de dicionários (muito melhor pra ler no JS)
    options = {id(clf): {'zipmap': False}}

    print("Convertendo para ONNX...")
    onx = convert_sklearn(
        clf, 
        initial_types=initial_type, 
        options=options
    )

    with open(args.out, 'wb') as f:
        f.write(onx.SerializeToString())

    print(f"Sucesso! Modelo exportado para: {args.out}")
    print("\nNo JavaScript (onnxruntime-web), a saída será um array, onde o índice 1 é a chance de morte.")

if __name__ == '__main__':
    main()