# telemetry/bridge/

**Status: feito, testado ponta a ponta** (Postgres + TelemetryService + bridge
+ simulação de pacotes UDP no formato real do `telemetry.cpp` -- 60
snapshots + eventos de dano/morte, todos gravados corretamente).

`udp_bridge.py`: escuta UDP (mesma porta que `telemetry.cpp` usa), acumula
os eventos recebidos e manda em lotes (`TelemetryBatch`) pro
`TelemetryService` via stream gRPC. Reconecta sozinho com backoff se o
stream cair.

## Rodando localmente (dev/debug)

```bash
pip install -r requirements.txt
python -m grpc_tools.protoc -I../proto --python_out=. --grpc_python_out=. ../proto/telemetry.proto
python udp_bridge.py --udp-port 9999 --grpc-target localhost:50051
```

Mesma observação do `telemetry/service/README.md`: os `telemetry_pb2*.py`
gerados são ignorados pelo git -- regenere sempre que mudar o `.proto`.

## Rodando ao lado do `tmwa-map`

Em produção (ou nos testes de latência da Fase 9), esta bridge roda na
mesma máquina (ou região) que o `tmwa-map`, escutando `127.0.0.1:9999` --
exatamente o endereço/porta que `telemetry.cpp` já usa por padrão. Não
precisa mudar nada no fork do `tmwa` pra isso funcionar.

## Opções úteis

- `--flush-interval` (default 1.0s): intervalo entre lotes enviados. Baixar
  isso reduz a latência ponta a ponta da telemetria, ao custo de mais
  overhead de rede/gRPC -- relevante pra calibrar na Fase 9.
- `--max-batch-size` (default 500): teto de eventos por lote, pra não
  crescer sem limite sob carga alta (ex: Fase 10, muitos bots).
