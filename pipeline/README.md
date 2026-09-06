# pipeline/

**Status: Fase 3 concluída e testada. Fases 4-7 a fazer.**

| Arquivo | Fase | O que faz |
|---|---|---|
| `generate_synthetic_data.py` | 3 ✅ | Gera N jogadores simulados (random walk com meta + encontros de dano/morte) e grava direto no Postgres, na tabela `player_events` -- testado com 20 jogadores x 30min (72k snapshots, ~5.3k hurt, ~950 death) |
| `build_dataset.py` | 4 🔲 | Lê `player_events` do Postgres, monta `X = estado_t`, `Y = ação_t+1` |
| `train_model.py` | 5-6 🔲 | Treina e compara Random Forest / XGBoost / MLP (e LSTM/GRU depois, se a sequência temporal ajudar) contra uma baseline aleatória |
| `export_onnx.py` | 7 🔲 | Exporta o melhor modelo pra `model.onnx`, valida saída ONNX vs. modelo original |

Mesma filosofia do que já foi feito antes (toho-like-js): scripts
testados de ponta a ponta com dados sintéticos antes de depender de dados
reais de jogo.
