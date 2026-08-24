# Dockerfile
# ---------------------------------------------------------------------------
# Gera os stubs Python a partir do telemetry.proto DENTRO da imagem, assim o
# .proto (fonte da verdade) e os arquivos gerados nunca ficam fora de sincronia.
FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY ["training/Phase 2/telemetry.proto", "."]
RUN python -m grpc_tools.protoc -I. --python_out=. --grpc_python_out=. telemetry.proto

COPY ["training/Phase 2/telemetry_service.py", "."]

EXPOSE 50051
ENV TELEMETRY_DSN="dbname=telemetry user=postgres password=postgres host=postgres"

CMD ["python", "telemetry_service.py", "--port", "50051"]
