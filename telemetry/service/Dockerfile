# Dockerfile
# ---------------------------------------------------------------------------
# Gera os stubs Python a partir do telemetry.proto DENTRO da imagem, assim o
# .proto (fonte da verdade) e os arquivos gerados nunca ficam fora de sincronia.
# Build context = raiz do repo (ver docker-compose.yml: dockerfile aponta pra
# cá, mas o "context" e a raiz, por causa do telemetry/proto/telemetry.proto).
FROM python:3.12-slim

WORKDIR /app

COPY telemetry/service/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY telemetry/proto/telemetry.proto .
RUN python -m grpc_tools.protoc -I. --python_out=. --grpc_python_out=. telemetry.proto

COPY telemetry/service/telemetry_service.py .

EXPOSE 50051
ENV TELEMETRY_DSN="dbname=telemetry user=postgres password=postgres host=postgres"

CMD ["python", "telemetry_service.py", "--port", "50051"]
