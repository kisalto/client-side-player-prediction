# app/ -- pipeline de ML (client-side player prediction)

Pipeline de telemetria, dataset e Machine Learning para previsão de ações
do jogador (client-side prediction) aplicado ao
[tmwa](https://github.com/kisalto/tmwa) (fork pessoal do servidor do
[TheManaWorld](https://github.com/themanaworld/tmwa)) e ao
[ManaVerse](https://github.com/kisalto/manaverse-mirror) (cliente).

Este repositório é separado dos forks do jogo de propósito: `tmwa` e
`manaverse-mirror` são GPL-2.0+ e só recebem a instrumentação mínima (ver
`tmwa/` e `manaverse/` abaixo); todo o resto do pipeline (infra, dataset,
treino, export ONNX, experimentos de latência) mora aqui.

**Setup de um clone novo**: ver o `README.md` na raiz do repo (um nível
acima desta pasta) -- ele explica como clonar `TMWA/` e
`manaverse-mirror/` como irmãos de `app/` e rodar `app/setup.sh`.

## Estrutura

```
app/
├── telemetry/
│   ├── proto/telemetry.proto      # contrato gRPC (Fase 2)
│   ├── service/                   # TelemetryService: recebe gRPC, grava no Postgres (Fase 2)
│   └── bridge/                    # UDP (do tmwa-map) -> gRPC (Fase 1.5)
├── data/
│   ├── raw/                       # exports reais de sessão (gitignored)
│   └── synthetic/                 # dados sintéticos gerados localmente (gitignored)
├── pipeline/                      # generate_synthetic_data.py, build_dataset.py,
│                                   # train_model.py, train_sequence_model.py, export_onnx.py (Fases 3-7)
├── tmwa/                          # Fase 1: telemetry.hpp/.cpp + pc.cpp.patch (vai pro clone TMWA/)
├── manaverse/                     # Fase 8: beingactionpredictor.h/.cpp + 4 patches + tests/ (vai pro clone manaverse-mirror/)
├── experiments/
│   ├── latency_injection/         # testes de latência/jitter/perda artificiais (Fase 9)
│   └── bot_load/                  # testes com 50-100 bots simultâneos (Fase 10)
├── docs/
│   └── architecture.md            # diagrama e decisões de arquitetura
├── setup.sh                       # copia/aplica tmwa/ e manaverse/ nos clones irmãos
└── docker-compose.yml             # sobe Postgres + TelemetryService localmente
```

## Onde cada Fase do roadmap mora aqui

| Fase | Descrição | Status | Onde |
|---|---|---|---|
| 0 | Entender o jogo (tmwa + ManaVerse) | ✅ feito | (não é código, foi exploração) |
| 1 | Telemetria no servidor (C++) | ✅ feito, patch consolidado validado (`git apply --check`) | `tmwa/` |
| 1.5 | Bridge UDP → gRPC | ✅ feito, testado ponta a ponta | `telemetry/bridge/` |
| 2 | `telemetry.proto` + `TelemetryService` | ✅ feito, testado ponta a ponta | `telemetry/proto/`, `telemetry/service/` |
| 3 | Dataset sintético | ✅ feito, testado | `pipeline/generate_synthetic_data.py` |
| 4 | `build_dataset.py` (estado_t → ação_t+1) | ✅ feito, testado | `pipeline/build_dataset.py` |
| 5 | Modelo baseline (Random Forest / XGBoost vs. aleatório) | ✅ feito, testado (RF 55,9% vs. 51,3% aleatório) | `pipeline/train_model.py` |
| 6 | MLP, depois LSTM/GRU se ajudar | ✅ feito, testado (LSTM/GRU não superaram RF/MLP nos dados sintéticos -- achado válido) | `pipeline/train_model.py`, `pipeline/train_sequence_model.py` |
| 7 | Export para `model.onnx` | ✅ feito, testado (labels/probs idênticos ao sklearn, diff ~1e-7) | `pipeline/export_onnx.py` |
| 8 | Previsão client-side (ManaVerse + ONNX Runtime C++) | ✅ compilado e linkado; 14 testes de runtime + contrato ONNX vs sklearn passando; boot do cliente real verificado (abre o `model.onnx`, sem crash). **Falta**: criar o efeito visual 9001 nos dados e testar jogando de verdade | `manaverse/` (guia: `manaverse/FASE8_INTEGRACAO.md`, testes: `manaverse/tests/`) |
| 9 | Latência/jitter/perda artificiais | 🔲 a fazer | `experiments/latency_injection/` |
| 10 | 50-100 bots simultâneos | 🔲 a fazer | `experiments/bot_load/` |
| 11 | Multi-região (Foz → Edge SP → Costa Leste) | 🔲 a fazer | `experiments/` (pasta nova quando chegar lá) |

## Rodando o TelemetryService localmente

```bash
cd app   # se ainda nao estiver aqui
docker compose up --build
```

Isso sobe Postgres (porta **5433** no host) e o TelemetryService (porta
50051, gRPC). O schema é aplicado automaticamente na primeira subida
(`telemetry/service/schema.sql` via `docker-entrypoint-initdb.d`).

### Portas: por que o Postgres está na 5433 e não na 5432

| Quem conecta | Endereço |
|---|---|
| Scripts no host/WSL (`pipeline/*.py`, `psql`, TelemetryService fora do Docker) | `localhost:5433` (`host=localhost port=5433` no DSN) |
| TelemetryService dentro do compose | `postgres:5432` (rede interna do Docker, inalterada) |
| Bridge → TelemetryService | `localhost:50051` (inalterado) |

Motivo (19/10/2026): a máquina de desenvolvimento tem um PostgreSQL 18
nativo do Windows (serviço `postgresql-x64-18`) escutando em
`0.0.0.0:5432`. Depois que o WSL passou para `networkingMode=mirrored`
(necessário para os amigos acessarem o servidor tmwa pelo IP do Radmin VPN
durante a coleta), WSL e Windows passaram a compartilhar as mesmas portas.
Resultado observado: o Docker subia o container sem publicar a 5432
(`docker ps` mostrava só `5432/tcp`, sem `0.0.0.0:5432->`) e um
`localhost:5432` no WSL cairia no Postgres do Windows, não no banco
`telemetry` do TCC -- falha silenciosa e perigosa para o pipeline.

Optou-se por mover a porta do TCC para 5433 em vez de parar o Postgres do
Windows, para os dois poderem coexistir. Os dados não são afetados: ficam
no volume `pgdata`, independente da porta publicada.

## Licença

Código deste repositório: MIT (ajuste se preferir outra). Isso é
independente da licença do jogo em si — `tmwa` e `manaverse-mirror`
continuam GPL-2.0+ nos seus próprios clones; nada aqui é derivado do
código do jogo.
