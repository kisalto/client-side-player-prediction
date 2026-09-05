# tmwa-ml-pipeline

Pipeline de telemetria, dataset e Machine Learning para previsão de ações do
jogador (client-side prediction) aplicado ao [tmwa](https://github.com/kisalto/tmwa)
(fork pessoal do servidor do [TheManaWorld](https://github.com/themanaworld/tmwa)).

Este repositório é separado do fork do jogo de propósito: o `tmwa` é GPL-2.0+
e só recebe a instrumentação mínima de telemetria; todo o resto do pipeline
(infra, dataset, treino, export ONNX, experimentos de latência) mora aqui.

## Repositórios relacionados

- **Jogo (servidor)**: [kisalto/tmwa](https://github.com/kisalto/tmwa) — fork
  do tmwa com os hooks de telemetria em `src/map/telemetry.{hpp,cpp}` e o
  patch em `pc.cpp` (dano e morte do jogador).
- **Jogo (cliente)**: [kisalto/manaverse-mirror](https://github.com/kisalto/manaverse-mirror)
  — mirror do ManaVerse (só necessário a partir da Fase 8, previsão no
  cliente; nada aqui depende dele ainda).

## Estrutura

```
tmwa-ml-pipeline/
├── telemetry/
│   ├── proto/telemetry.proto      # contrato gRPC (Fase 2 -- pronto)
│   ├── service/                   # TelemetryService: recebe gRPC, grava no Postgres (Fase 2 -- pronto, testado)
│   └── bridge/                    # UDP (do tmwa-map) -> gRPC (Fase 1.5 -- a fazer)
├── data/
│   ├── raw/                       # exports reais de sessão (gitignored)
│   └── synthetic/                 # dados sintéticos gerados localmente (gitignored)
├── pipeline/                      # generate_synthetic_data.py, build_dataset.py,
│                                   # train_model.py, export_onnx.py (Fases 3-7 -- a fazer)
├── experiments/
│   ├── latency_injection/         # testes de latência/jitter/perda artificiais (Fase 9)
│   └── bot_load/                  # testes com 50-100 bots simultâneos (Fase 10)
├── docs/
│   └── architecture.md            # diagrama e decisões de arquitetura
└── docker-compose.yml             # sobe Postgres + TelemetryService localmente
```

## Onde cada Fase do roadmap mora aqui

| Fase | Descrição | Status | Onde |
|---|---|---|---|
| 0 | Entender o jogo (tmwa + ManaVerse) | ✅ feito | (não é código, foi exploração) |
| 1 | Telemetria no servidor (C++) | ✅ feito | fork do `tmwa`, `src/map/telemetry.*` |
| 1.5 | Bridge UDP → gRPC | 🔲 a fazer | `telemetry/bridge/` |
| 2 | `telemetry.proto` + `TelemetryService` | ✅ feito, testado ponta a ponta | `telemetry/proto/`, `telemetry/service/` |
| 3 | Dataset sintético (novo, para o tmwa) | 🔲 a fazer | `pipeline/generate_synthetic_data.py` |
| 4 | `build_dataset.py` (estado_t → ação_t+1) | 🔲 a fazer | `pipeline/build_dataset.py` |
| 5 | Modelo baseline (Random Forest / XGBoost vs. aleatório) | 🔲 a fazer | `pipeline/train_model.py` |
| 6 | MLP, depois LSTM/GRU se ajudar | 🔲 a fazer | `pipeline/train_model.py` |
| 7 | Export para `model.onnx` | 🔲 a fazer | `pipeline/export_onnx.py` |
| 8 | Previsão client-side (ManaVerse + ONNX Runtime C++) | 🔲 a fazer | repo do cliente (`manaverse-mirror`) |
| 9 | Latência/jitter/perda artificiais | 🔲 a fazer | `experiments/latency_injection/` |
| 10 | 50-100 bots simultâneos | 🔲 a fazer | `experiments/bot_load/` |
| 11 | Multi-região (Foz → Edge SP → Costa Leste) | 🔲 a fazer | `experiments/` (pasta nova quando chegar lá) |

## Rodando o TelemetryService localmente

```bash
docker compose up --build
```

Isso sobe Postgres (porta 5432) e o TelemetryService (porta 50051, gRPC).
O schema é aplicado automaticamente na primeira subida
(`telemetry/service/schema.sql` via `docker-entrypoint-initdb.d`).

## Licença

Código deste repositório: MIT (ajuste se preferir outra). Isso é
independente da licença do jogo em si — o `tmwa` continua GPL-2.0+ no fork
dele; nada daqui é derivado do código do jogo.
