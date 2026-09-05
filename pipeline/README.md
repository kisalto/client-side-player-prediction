# pipeline/

**Status: a fazer (Fases 3-7)**

| Arquivo | Fase | O que faz |
|---|---|---|
| `generate_synthetic_data.py` | 3 | Gera sessões falsas no schema do `TelemetryBatch`/Postgres, pra testar o resto do pipeline antes de ter dados reais de jogo |
| `build_dataset.py` | 4 | Lê `frames`/`events` do Postgres, monta `X = estado_t`, `Y = ação_t+1` |
| `train_model.py` | 5-6 | Treina e compara Random Forest / XGBoost / MLP (e LSTM/GRU depois, se a sequência temporal ajudar) contra uma baseline aleatória |
| `export_onnx.py` | 7 | Exporta o melhor modelo pra `model.onnx`, valida saída ONNX vs. modelo original |

Mesma filosofia do que já foi feito antes (toho-like-js): scripts
testados de ponta a ponta com dados sintéticos antes de depender de dados
reais de jogo.
