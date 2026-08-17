"""
train_model.py
---------------------------------------------------------------------------
Treina um modelo de Machine Learning (Random Forest) usando o dataset
gerado pelo build_dataset.py. Avalia a precisão e salva o modelo em .pkl.

Uso:
    python train_model.py --dataset dataset.csv --out model.pkl
"""
import argparse
import pandas as pd
import joblib
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, roc_auc_score

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--dataset', default='dataset.csv', help='Caminho para o dataset gerado')
    ap.add_argument('--out', default='model.pkl', help='Onde salvar o modelo treinado')
    args = ap.parse_args()

    print(f"Carregando dataset de {args.dataset}...")
    df = pd.read_csv(args.dataset)

    # Definir colunas que não são features (metadados e target)
    meta_cols = ['session_id', 't_ms', 'frame_f', 'label', 'n_deaths_in_subsession']
    feature_cols = [c for c in df.columns if c not in meta_cols]

    X = df[feature_cols]
    y = df['label']

    print(f"Features ({len(feature_cols)}): {feature_cols[:5]} ...")
    
    # Divisão treino/teste (estratificada para manter a proporção de mortes)
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42, stratify=y
    )

    print(f"Treinando com {len(X_train)} amostras. Testando com {len(X_test)}.")

    # Random Forest é ótimo aqui. class_weight='balanced' ajuda muito
    # já que frames de "morte" (1) são bem mais raros que frames seguros (0).
    clf = RandomForestClassifier(
        n_estimators=100, 
        max_depth=12,         # Evita overfitting e deixa o modelo ONNX mais leve
        class_weight='balanced', 
        random_state=42,
        n_jobs=-1             # Usa todos os núcleos do processador
    )
    
    print("Treinando o modelo...")
    clf.fit(X_train, y_train)

    # Avaliação
    print("\nAvaliação no conjunto de teste:")
    y_pred = clf.predict(X_test)
    y_proba = clf.predict_proba(X_test)[:, 1]
    
    print(classification_report(y_test, y_pred))
    print(f"ROC-AUC Score: {roc_auc_score(y_test, y_proba):.4f}")

    # Salva o modelo
    joblib.dump(clf, args.out)
    print(f"\nModelo salvo com sucesso em: {args.out}")

if __name__ == '__main__':
    main()