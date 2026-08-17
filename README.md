# projeto-secreto

## Executar treinamento:
````
# 1. Gera os dados fakes
python training/generate_synthetic_data.py --sessions 50 --out data/raw

# 2. Constrói o dataset com engenharia de features (TTCA, etc)
python training/build_dataset.py --raw-dir data/raw --out dataset.csv --horizon-ms 5000

# 3. Treina o Random Forest
python training/train_model.py --dataset dataset.csv --out model.pkl

# 4. Exporta para a Web
python training/export_onnx.py --model model.pkl --dataset dataset.csv --out model.onnx
```
