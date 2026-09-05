# telemetry/service/

TelemetryService: servidor gRPC que recebe telemetria (via `StreamTelemetry`
ou `SendBatch`) e grava no PostgreSQL. Testado ponta a ponta (ver
`docs/architecture.md` na raiz pro desenho geral).

## Rodando via Docker (recomendado)

Da raiz do repo:
```bash
docker compose up --build
```
Gera os stubs a partir do `.proto` DENTRO da imagem -- nunca ficam
desatualizados em relação ao `telemetry/proto/telemetry.proto`.

## Rodando localmente sem Docker (dev/debug)

```bash
pip install -r requirements.txt
python -m grpc_tools.protoc -I../proto --python_out=. --grpc_python_out=. ../proto/telemetry.proto
python telemetry_service.py --port 50051 --dsn "dbname=telemetry user=postgres password=postgres host=localhost"
```
Os arquivos `telemetry_pb2.py`/`telemetry_pb2_grpc.py` gerados por esse
comando são ignorados pelo git (ver `.gitignore` na raiz) -- regenere
sempre que mudar o `.proto`.
