# pipeline/

**Status: Fases 3 e 4 concluídas e testadas. Fases 5-7 a fazer.**

| Arquivo | Fase | O que faz |
|---|---|---|
| `generate_synthetic_data.py` | 3 ✅ | Gera N jogadores simulados (random walk com meta + encontros de dano/morte + ações de movimento/ataque) e grava direto no Postgres |
| `build_dataset.py` | 4 ✅ | Lê `player_events`, monta 1 linha por ação real (`player_move_cmd`/`player_attack`) com `X` = estado+histórico no instante da decisão, `Y` = tipo de ação + parâmetros (direção do movimento ou se o ataque é contínuo) -- testado: 83k eventos → 5.5k linhas de ação |
| `train_model.py` | 5-6 🔲 | Treina e compara Random Forest / XGBoost / MLP (e LSTM/GRU depois, se a sequência temporal ajudar) contra uma baseline aleatória |
| `export_onnx.py` | 7 🔲 | Exporta o melhor modelo pra `model.onnx`, valida saída ONNX vs. modelo original |

## Sobre o dataset (`build_dataset.py`)

Uma linha por **ação real** (não por janela de tempo): cada `player_move_cmd`
ou `player_attack` vira uma linha, com features computadas do que já era
conhecido até aquele instante (posição/vida atuais, velocidade recente,
tempo desde a última ação, tempo desde o último dano). Ver o docstring do
arquivo pra mais detalhes, incluindo uma limitação conhecida: coordenadas
não carregam identidade de mapa ainda (só relevante quando houver dados
reais com trocas de mapa).

Mesma filosofia do que já foi feito antes (toho-like-js): scripts
testados de ponta a ponta com dados sintéticos antes de depender de dados
reais de jogo.
