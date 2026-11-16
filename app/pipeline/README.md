# pipeline/

**Status: Fases 3 a 7 concluídas e testadas. Pipeline Python completo.**

| Arquivo | Fase | O que faz |
|---|---|---|
| `generate_synthetic_data.py` | 3 ✅ | Gera N jogadores simulados (random walk com meta + encontros de dano/morte + ações de movimento/ataque) e grava direto no Postgres |
| `build_dataset.py` | 4 ✅ | Lê `player_events`, monta 1 linha por ação real com `X` = estado+histórico, `Y` = tipo de ação + parâmetros; `--player-map` liga personagem → pessoa |
| `player_map.csv` | 4 | char_id → pessoa (pseudônimo P01…P11), grupo do leave-one-player-out |
| `train_model.py` | 5-6 ✅ | Random Forest + MLP + XGBoost vs. baseline aleatório/classe majoritária (classificação) + Random Forest Regressor vs. Dead Reckoning (direção); salva `model.pkl` |
| `train_sequence_model.py` | 6 ✅ | LSTM/GRU sobre a sequência bruta das últimas K ações, pra testar se sequência crua supera as features resumidas à mão; `--device cuda` e `--n-folds` |
| `compare_models.py` | 9 | 14 modelos no mesmo LOPO (baseline, Markov, lineares, RF, XGBoost, MLP) + curva de aprendizado; `--n-folds` (grupos de pessoas) e `--lc-*` |
| `statistical_analysis.py` | 9 | Bootstrap por pessoa/hierárquico, Wilcoxon + Holm, Friedman, calibração, limiar, curva de aprendizado → `estatistica/report.txt` |
| `export_results_json.py` | 9 | Junta tudo de `--results-dir` em `resultados_estatisticos.json` |
| `mixed_model_mcmc.py` | 9 | Logística mista bayesiana (PyMC); pesado (horas) |
| `evaluate_ingame.py` | 8 | Lê o CSV do `TCC_PREDICTOR_LOG` do ManaVerse: recall por episódio de ataque, precisão do telegraph, antecedência, PR-AUC por inferência (+ cruzamento com a telemetria do servidor) |
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

## Resultados (dados reais, coletas oficiais 1–5, 11 pessoas, rodada de 2026-11-15)

Sessões em `session_labels`: oficiais 5f8b0218 (19/10), 0eb49873 (20/10),
74ce842c (24/10), 5f5c171f (11/11) e a40d72d9 (15/11); 09d65843 (12/10) é
`dev` (teste anterior à coleta oficial 1). Comandos (venv `~/tcc/venv-ml`,
torch `2.14.0+cu130`, GPU RTX 3050):

```bash
python build_dataset.py --split official --exclude-chars 150003 150006 150010 150013 \
    --player-map player_map.csv --out dataset_v2.csv
python compare_models.py --dataset dataset_v2.csv --out-dir resultados_v2 --n-folds 0 --seeds 3 \
    --learning-curve --lc-test-persons 0 --lc-max-subsets 3 --lc-sizes 1 2 3 5 7 10
python train_sequence_model.py --dataset dataset_v2.csv --out-dir resultados_v2 --n-folds 0 --seeds 3 \
    --device cuda --batch-size 2048
python statistical_analysis.py --dataset dataset_v2.csv --results-dir resultados_v2
python export_results_json.py --dataset dataset_v2.csv --results-dir resultados_v2
```

**Resultados oficiais: `resultados_v2_b512/`.** `resultados_v2/` tem LSTM/GRU
treinados com batch 2048, que piora esses modelos (ver testes abaixo).
`resultados_v2_b512/` = arquivos tabulares de `resultados_v2/` + LSTM/GRU do
teste T1 (`--batch-size 512`, o mesmo da rodada de 6 pessoas), com
`statistical_analysis.py` e `export_results_json.py` rodados de novo nessa
pasta. Os números abaixo são de `resultados_v2_b512/`.

Exclusões: 150003 (apagado) e 150010 (banido), como antes, mais todo
personagem com menos de 300 eventos nas sessões oficiais (150006: 31
eventos; 150013: 139), todos com 0 ataques. **O 150006 entrava na rodada de
6 pessoas** (como P03, que agora é só o 150009).

Dataset: 147.739 eventos → **71.817 ações (6.775 ataques, prevalência
9,4%)**, 13 personagens de **11 pessoas**. Validação: LOPO (uma pessoa por
rodada, 11 rodadas), 3 sementes. Tempo: tabular 21 min + curva 15 min (CPU);
LSTM/GRU 10,5 min (GPU, batch 2048) ou 22 min (GPU, batch 512).

| Pessoa | Ações | Ataques | Prevalência |
|---|---|---|---|
| P01 | 24.365 | 2.160 | 0,089 |
| P02 | 4.301 | 453 | 0,105 |
| P03 | 10.153 | 264 | 0,026 |
| P04 | 5.853 | 405 | 0,069 |
| P05 | 10.911 | 1.073 | 0,098 |
| P06 | 4.555 | 369 | 0,081 |
| P07 | 1.010 | 242 | 0,240 |
| P08 | 5.108 | 384 | 0,075 |
| P09 | 1.251 | 347 | 0,277 |
| P10 | 1.590 | 336 | 0,211 |
| P11 | 2.720 | 742 | 0,273 |

**PR-AUC de ataque, média entre pessoas (dp entre pessoas)**, ao lado da
rodada de 6 pessoas (`~/tcc/work/stats`, 5 sementes, LSTM/GRU em CPU com
batch 512); LSTM/GRU de 11 pessoas em GPU com batch 512:

| Modelo | 6 pessoas | 11 pessoas |
|---|---|---|
| Baseline "repete a ação anterior" | 0,524 | 0,564 (0,143) |
| Markov ordem 5 | 0,658 | 0,677 (0,119) |
| Logística (log) | 0,649 | 0,690 (0,137) |
| LDA | 0,648 | 0,694 (0,138) |
| MLP | 0,718 | 0,651 (0,144) |
| Rede sem histórico | 0,675 | 0,754 (0,119) |
| Random Forest | 0,747 | **0,787 (0,127)** |
| XGBoost | 0,743 | **0,799 (0,129)** |
| LSTM | 0,764 | **0,796 (0,100)** |
| GRU | 0,765 | **0,789 (0,124)** |

- **RF − baseline:** +0,223, IC95 hierárquico [0,190; 0,253]; RF vence nas
  11/11 pessoas; Wilcoxon exato p = 0,00098, **Holm p = 0,013** (com 6
  pessoas: p = 0,031, Holm 0,406; com n=6 o menor p possível já era 0,031).
- **Friedman** (14 modelos × 11 pessoas): χ² = 124,2, p = 3,0e-20, W de
  Kendall = 0,869. Posto médio: XGBoost 2,09, LSTM 2,55, GRU 2,64, RF 3,27.
- **Contra a RF:** XGBoost +0,012 [0,003; 0,021], 10/11 pessoas, Holm
  p = 0,039 (efeito pequeno); LSTM +0,009 [−0,018; 0,039], 6/11, Holm p = 1;
  GRU +0,002 [−0,028; 0,023], 6/11, Holm p = 1. **XGBoost, LSTM, GRU e RF
  ficam praticamente empatados.** Com 6 pessoas o GRU ficava +0,018
  [−0,008; 0,047]. LSTM e GRU também vencem o baseline em 11/11 (Holm
  p = 0,013); no recall do baseline, precisão LSTM 0,813, GRU 0,805, RF 0,787.
- **Limiar RF (recall-alvo 0,7 nos outros 10):** 0,187 a 0,463 entre
  pessoas (antes 0,087 a 0,557); recall atingido de 0,462 (P01) a 0,911
  (P10), dp 0,120 (antes 0,165). O limiar continua não transferindo para P01.
- **Curva de aprendizado (RF):** 1 pessoa 0,647 → 2: 0,724 → 5: 0,773 →
  7: 0,780 → 10: 0,788. De 7 para 10 pessoas: +0,008 (melhora em 8/11);
  XGBoost +0,010 (10/11). Ainda sobe, mas pouco.
- **P01–P06 quase não mudaram** (RF por pessoa, diferença ≤ 0,012). A média
  subiu com a entrada de P07–P11; parte disso é a prevalência maior de P07,
  P09, P10 e P11 (0,21 a 0,28) e parte é o P01 pesar menos (teste T4 abaixo).

**Testes de diagnóstico** (`resultados_v2_testes/`, script `rodar_testes.sh`;
PR-AUC médio entre pessoas, sementes 42–44):

| Teste | O que isola | Resultado |
|---|---|---|
| T1: dataset_v2, GPU, batch 512 (vs. 2048) | tamanho do batch | rede sem histórico 0,726 → 0,754; LSTM 0,777 → **0,796**; GRU 0,767 → **0,789**. LSTM − RF +0,009 (vence 6/11), GRU − RF +0,002 (6/11) |
| T2: dataset de 6 pessoas, GPU batch 512 vs. CPU batch 512 (rodada antiga) | CPU × GPU | LSTM 0,762 → 0,768; GRU 0,765 → 0,757; rede sem histórico 0,678 → 0,677 (maior diferença numa pessoa: 0,034, GRU) |
| T3: dataset_v2 só com P01–P06 (tabular + LSTM/GRU GPU batch 512) | dados novos das mesmas 6 pessoas | baseline 0,524 (antes 0,524); RF 0,743 (0,747); XGBoost 0,745 (0,743); LSTM 0,766 (0,762); GRU 0,768 (0,765); LSTM/GRU − RF ≈ +0,02, vencem 5/6 |

- **Verificado:** a queda do LSTM/GRU na rodada principal vem do **batch
  2048**, não da GPU (T2: diferença de no máximo 0,008 na média) nem dos dados
  (T3 reproduz a rodada de 6 pessoas). Com batch 512, LSTM/GRU **empatam**
  com a RF nas 11 pessoas, em vez de ficar ~0,02 acima como nas 6.
- **Verificado:** com as mesmas 6 pessoas o resultado é o mesmo da rodada
  antiga, então o aumento da média (RF 0,747 → 0,787) vem das 5 pessoas novas.
  Quanto disso é prevalência: ver T4.
- **Feito:** estatística refeita com batch 512 em `resultados_v2_b512/`
  (números da tabela acima).

**T4, prevalência** (`resultados_v2_testes/t4_prevalencia.txt`; RF, média por
grupo de pessoas):

| Grupo | Prevalência | PR-AUC | PR-AUC normalizado (AP−prev)/(1−prev) | ROC-AUC |
|---|---|---|---|---|
| P01–P06 | 0,078 | 0,745 | 0,725 | 0,916 |
| P07–P11 | 0,215 | 0,838 | 0,793 | 0,934 |
| P02–P06 (sem P01) | 0,076 | 0,795 | 0,781 | 0,938 |

A diferença entre os grupos cai de 0,093 (PR-AUC) para 0,068 (normalizado)
e 0,018 (ROC-AUC); sem o P01 as pessoas antigas ficam no nível das novas.
Spearman(prevalência, PR-AUC) entre as 11 pessoas: 0,31 (p = 0,36).
Conclusão: **parte** do aumento da média vem da prevalência maior das
pessoas novas; o resto é o P01 (a pessoa mais difícil) pesar menos numa
média de 11.

**Varredura da janela K (LSTM/GRU, 11 pessoas, GPU batch 512, semente 42;
`resultados_v2_b512/k_sweep.csv`, seção 7 do report):**

| K | LSTM | GRU | LSTM (6 pessoas) | GRU (6 pessoas) |
|---|---|---|---|---|
| 4 | 0,795 | 0,785 | 0,772 | 0,771 |
| 8 | 0,791 | 0,786 | 0,768 | 0,764 |
| 16 | 0,799 | 0,786 | 0,761 | 0,768 |
| 32 | 0,785 | 0,783 | 0,763 | 0,762 |

K quase não importa (variação ≤ 0,014, contra dp entre pessoas de ~0,10–0,13),
como na rodada de 6 pessoas: 4 ações de histórico já carregam o sinal.

**Direção do movimento** (`train_model.py`, Tarefa 2, split agrupado por
pessoa, 1 sorteio; treino 37.397 / teste 27.645 moves): cosseno RF 0,566 vs.
Dead Reckoning 0,549 (6 pessoas: 0,550 vs. 0,540); MAE (dx,dy) 0,8 vs. 5,2
(antes 0,9 vs. 5,1). Mesma conclusão: a vantagem é na magnitude, não na
direção.

**Modelo do cliente** (`resultados_v2_b512/modelo_cliente/`: `train.log`,
`metrics.json`, `model.pkl`, `model.onnx`):

```bash
python train_model.py --dataset dataset_v2.csv --metrics-out resultados_v2_b512/modelo_cliente/metrics.json \
    --model-out resultados_v2_b512/modelo_cliente/model.pkl
python export_onnx.py --model resultados_v2_b512/modelo_cliente/model.pkl --dataset dataset_v2.csv \
    --out resultados_v2_b512/modelo_cliente/model.onnx
```

RF treinada nas 11 pessoas. LOPO: PR-AUC 0,788 (limiar 0,5: precisão 0,88,
recall 0,66). **Limiar do cliente: 0,227** (recall-alvo 0,7 nas predições
OOF das 11 pessoas → precisão 0,71, recall 0,70; antes 0,143 com precisão
0,53). Limiares por fold de 0,187 a 0,547 (o de P01 é o 0,547).
`model.onnx`: 81,5 MB (antes 53,6), saída igual ao sklearn (diferença máx.
1,8e-7), 0,089 ms por inferência de 1 linha no ONNX Runtime Python (CPU,
linhas reais, com o MCMC rodando em paralelo; não comparável com os 0,013 ms
antigos, medidos com entrada aleatória e máquina livre). **Não instalado** em
`~/tcc/manaverse-local`: inclui todas as pessoas, e para o teste em jogo o
modelo deve ser treinado sem quem vai jogar.

**Modelo misto por MCMC** (`mixed_model_mcmc.py --dataset dataset_v2.csv
--out-dir resultados_v2_b512/estatistica/mcmc`; 4 cadeias × (2000 burn-in +
2000 amostras), target_accept 0,99; 27 + 54 + 52 min). Os 3 modelos
convergiram: 0 divergências, R-hat ≤ 1,0062, ESS ≥ 1712, E-BFMI ≥ 0,71.

| | 6 pessoas | 11 pessoas |
|---|---|---|
| M1: OR de `prev_action_was_attack` | 26,7 [24,1; 29,8] | 22,0 [20,2; 23,8] |
| M1: σ intercepto / ICC latente | 0,30 / 0,027 | 0,40 [0,26; 0,69] / 0,046 [0,020; 0,125] |
| M2: σ intercepto | 0,58 [0,33; 1,25] | 0,70 [0,46; 1,17] |
| M2: σ inclinação de prev | 0,97 [0,58; 1,80] | 0,90 [0,60; 1,46] |
| M2: OR de prev (pessoa média) | 42,6 [17,0; 96,7] | 29,5 [16,4; 50,7] |
| M2: ICC latente / MOR | 0,094 / 1,74 | 0,129 [0,061; 0,293] / 1,95 [1,55; 3,05] |
| M2s (priori larga): σ inclinação | 1,07 [0,60; 2,40] | 0,93 [0,61; 1,64] |

- A diferença entre pessoas está mais em **quanto o ataque anterior prevê o
  próximo** (inclinação, σ 0,90) do que no nível de base (σ 0,70); igual à
  rodada de 6 pessoas, agora com ICs mais estreitos.
- Pessoas com intercepto alto e inclinação negativa (atacam mais "do nada",
  rajadas menos previsíveis pelo ataque anterior): P01 (+0,53 / −1,06), P07
  (+0,82 / −0,95), P09 (+1,15 / −1,24). Antes P01 era o único caso
  (+0,89 / −1,34).
- M2s ≈ M2: com 11 pessoas a priori de σ quase não pesa mais (com 6 pessoas o
  IC da inclinação ia até 2,40 com a priori larga).
- Variacional (seção 4c) × MCMC (M1): ORs iguais até a 3ª casa; σ 0,383 × 0,397.

Mudanças de código nesta rodada: patch `pipeline_grupos_gpu` (`--n-folds`,
`--lc-*`, `--device`); `statistical_analysis.py` e `export_results_json.py`
sem textos fixos de "6 pessoas"; seção 7 do `statistical_analysis.py` não
quebra mais quando `--lc-sizes` não é consecutivo (compara os dois maiores
tamanhos avaliados).

## Resultados (dados reais, coletas oficiais 1–3, rodada de 2026-10-24)

```bash
python build_dataset.py --split official --exclude-chars 150003 150010 \
    --player-map player_map.csv --out dataset.csv
python train_model.py --dataset dataset.csv          # --target-recall 0.7 (default)
python export_onnx.py --model model.pkl --dataset dataset.csv --out model.onnx
```

Dataset: 106.448 eventos → 55.321 ações (4.242 ataques, prevalência 7,7%),
8 personagens de **6 pessoas** (`player_map.csv`). O leave-one-player-out
agrupa por pessoa (`player_id`), não por `char_id`: o autor jogou com 3
personagens em 3 coletas e um amigo trocou de personagem no meio da coleta 3.

**Ataque vs. resto, leave-one-player-out (média de 6 pessoas):**

| Modelo | PR-AUC | Precisão | Recall |
|---|---|---|---|
| Aleatório (= prevalência) | 0,078 | – | – |
| Baseline "repete a ação anterior" | 0,524 | 0,70 | 0,70 |
| Random Forest (limiar 0,5) | **0,748** | 0,89 | 0,66 |
| MLP (limiar 0,5) | 0,723 | 0,84 | 0,65 |

**No mesmo recall do baseline**, a precisão da RF é maior **nas 6 pessoas**
(média 0,786 vs. 0,698; de +0,03 a +0,16). Esse é o ponto da curva PR da RF
no recall do baseline em cada jogador de teste: uma comparação de curvas,
não um limiar que daria pra escolher antes de ver o jogador. Com 3 jogadores
(rodada de 2026-10-20) a RF NÃO ganhava nesse ponto.

**Limiar para o cliente:** 0,143 (recall-alvo 0,7 nas predições OOF das 6
pessoas → precisão 0,53, recall 0,70). Ressalva: os limiares calibrados por
fold variam muito (0,077 a 0,637). No fold P01, o limiar escolhido nos
outros 5 deu recall 0,41 em vez de 0,70, ou seja, a calibração do limiar
não transfere bem entre pessoas. P01 também é a pessoa mais difícil
(PR-AUC 0,498).

Direção de movimento (split agrupado, 1 sorteio): cosseno RF 0,550 vs.
Dead Reckoning 0,540. MAE (dx,dy) 0,9 vs. 5,1. A vantagem está na
magnitude, não na direção.

`model.onnx`: 53,6 MB (RF de 300 árvores), 0,013 ms por inferência de 1
linha no ONNX Runtime Python (CPU, entrada aleatória). Saída idêntica ao
sklearn (diferença máxima 1,8e-7).

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
