# telemetry/bridge/

**Status: a fazer (Fase 1.5)**

Processo local que:
1. Escuta UDP na porta 9999 (mesma que `tmwa-map` manda via `telemetry.cpp`)
2. Empacota as linhas JSON recebidas em `TelemetryBatch` (protobuf)
3. Reenvia pro `TelemetryService` via `StreamTelemetry` (gRPC), usando os
   stubs gerados de `../proto/telemetry.proto`

Roda na mesma máquina que o `tmwa-map` (ou próximo, na mesma região) --
é o ponto natural pra medir/injetar latência artificial na Fase 9, já que
fica entre o jogo e o resto do pipeline.

Provavelmente Python (`asyncio` + `socket` UDP + o gRPC client stub), pra
reaproveitar os mesmos `telemetry_pb2`/`telemetry_pb2_grpc` do
`telemetry_service.py`.
