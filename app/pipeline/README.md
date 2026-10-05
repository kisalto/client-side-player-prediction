# pipeline/

**Status: Fases 3 a 7 concluídas e testadas. Pipeline Python completo.**

| Arquivo | Fase | O que faz |
|---|---|---|
| `generate_synthetic_data.py` | 3 ✅ | Gera N jogadores simulados (random walk com meta + encontros de dano/morte + ações de movimento/ataque) e grava direto no Postgres |
| `build_dataset.py` | 4 ✅ | Lê `player_events`, monta 1 linha por ação real com `X` = estado+histórico, `Y` = tipo de ação + parâmetros |
| `train_model.py` | 5-6 ✅ | Random Forest + MLP + XGBoost vs. baseline aleatório/classe majoritária (classificação) + Random Forest Regressor vs. Dead Reckoning (direção); salva `model.pkl` |
| `train_sequence_model.py` | 6 ✅ | LSTM/GRU sobre a sequência bruta das últimas K ações, pra testar se sequência crua supera as features resumidas à mão |
| `export_onnx.py` | 7 ✅ | Converte `model.pkl` → `model.onnx`, valida saída idêntica ao sklearn (diferença ~1e-7, só arredondamento) |

## Fase 7: exportação ONNX

Modelo escolhido pra exportar: **Random Forest** (Tarefa 1 -- classificar
tipo da próxima ação), não o MLP (que teve acurácia levemente maior) nem o
LSTM/GRU -- suporte a ONNX mais robusto e testado, e mais simples de
consumir em C++ depois (não precisa replicar um `StandardScaler` nem
gerenciar estado de sequência).

**Detalhe técnico que muda em relação aos projetos anteriores**: como o
pipeline tem uma coluna categórica string (`prev_action_type`), ONNX não
aceita um único tensor float de entrada como antes -- cada coluna do
DataFrame vira uma entrada nomeada separada no grafo (`FloatTensorType`
pras numéricas, `StringTensorType` pra categórica). Isso significa que o
código C++ que for consumir esse `model.onnx` (Fase 8) precisa montar um
tensor por coluna, nomeado igual à coluna -- ver `_to_onnx_inputs()` em
`export_onnx.py` como referência exata do formato.

**Nota pra Fase 8**: o `model.onnx` saiu com ~53MB (Random Forest de 300
árvores) -- funciona, mas é pesado pra carregar/rodar em tempo real num
cliente de jogo. Se a latência de inferência incomodar na Fase 8/9, vale
reduzir `n_estimators` ou trocar por um modelo mais leve (o MLP, por
exemplo, tende a gerar um ONNX bem menor).

## Resultados (dados sintéticos, 40 jogadores x 30min, ~11k linhas de ação)

**Classificação do tipo de ação** (move vs attack), split agrupado por `char_id`:

| Modelo | Acurácia | F1 macro |
|---|---|---|
| Baseline aleatório | 51,3% | 0,51 |
| Baseline classe majoritária | 46,9% | 0,32 |
| Random Forest | 55,9% | 0,56 |
| MLP | 56,3% | 0,56 |
| XGBoost | 54,1% | 0,54 |
| LSTM (sequência bruta, seq_len=8) | 53,8% | 0,54 |
| GRU (sequência bruta, seq_len=8) | 53,0% | 0,53 |

**Achado da Fase 6**: LSTM/GRU não superaram os modelos tabulares (RF/MLP)
nos dados sintéticos atuais -- indício de que as features de histórico já
resumem o que importa (`time_since_last_action`, `velocity` etc.), sem
sinal temporal adicional que só a sequência bruta capturaria. Isso pode
mudar com dados reais, onde os padrões de jogador tendem a ser mais ricos
que o "encontro aleatório por tick" do simulador -- vale reavaliar quando
houver telemetria real.

**Direção de movimento vs. Dead Reckoning**: Random Forest Regressor teve
similaridade de cosseno 0,49 contra ~0,00 do Dead Reckoning. **Ressalva
importante**: isso é inflado pela simplicidade do simulador -- o próximo
alvo é sorteado sem relação com a direção atual, o que deixa o Dead
Reckoning artificialmente ruim (jogadores reais tendem a manter a direção
entre comandos). Reavaliar quando houver dados reais.

## Achado da Fase 5: um bug real no gerador sintético

A primeira rodada de treino deu **100% de acurácia** -- sinal de alerta.
Investigando, `speed` valia exatamente 0 em 100% das linhas de `move` e
sempre >0 nas de `attack`: o gerador registrava `player_move_cmd` ANTES do
passo de movimento do tick (posição sempre igual à do snapshot anterior) e
`player_attack` DEPOIS (posição já andou). Corrigido invertendo a ordem;
resultados acima já refletem a correção.

Mesma filosofia do que já foi feito antes (toho-like-js): scripts
testados de ponta a ponta com dados sintéticos antes de depender de dados
reais de jogo.
