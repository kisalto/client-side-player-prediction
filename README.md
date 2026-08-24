# Projeto Secreto

## Sumário
- [Descrição Geral](#descrição-geral)
- [Tecnologias Utilizadas](#tecnologias-utilizadas)
- [Estrutura do Repositório](#estrutura-do-repositório)
- [Funcionamento](#funcionamento)
- [Configuração](#configuração)
- [Execução](#execução)

---

## Descrição Geral
Este projeto coleta telemetria do jogo [K]night City e oferece um serviço gRPC para persistir os dados em PostgreSQL. A coleta atual grava sessões em arquivos JSON; o contrato gRPC e o serviço ficam preparados para ingestão por um cliente compatível.

O fluxo atual é:
1. O plugin carregado pelo BepInEx injeta o `TelemetryLogger` nas cenas jogáveis.
2. O logger captura snapshots do jogador, inimigos, chefes, projéteis e eventos, exportando-os em JSON com F9.
3. O serviço gRPC recebe lotes conforme `telemetry.proto` e grava sessões, frames e eventos no PostgreSQL.
4. A análise e o treinamento de modelos ainda estão representados pelos scripts históricos em `old/`.

---

## Tecnologias Utilizadas
- **C# / Unity**: coleta de telemetria dentro do jogo
- **BepInEx**: carregamento do mod
- **Python**: serviço gRPC
- **gRPC / Protocol Buffers**: comunicação entre o jogo e o serviço
- **PostgreSQL**: armazenamento de sessões, frames e eventos em tabelas relacionais, com inimigos e projéteis em JSONB

---

## Estrutura do Repositório
- `training/Phase 1/TelemetryLogger.cs`: captura e exporta a telemetria em JSON.
- `training/Phase 1/Plugin.cs`: injeta o logger através do BepInEx.
- `training/Phase 2/telemetry.proto`: contrato das mensagens e RPCs gRPC.
- `training/Phase 2/telemetry_service.py`: servidor gRPC que persiste a telemetria.
- `training/Phase 2/telemetry_pb2.py` e `telemetry_pb2_grpc.py`: stubs Python gerados pelo Protobuf.
- `schema.sql`: cria as tabelas do PostgreSQL.
- `mod/Assembly-CSharp.csproj`: projeto C# usado para compilar o plugin.
- `old/`: scripts antigos mantidos apenas como registro da pipeline experimental de Machine Learning; não são a pipeline principal atual.
- `BepInEx/`: instalação e configuração do carregador de mods.
- `game/`: arquivos locais do jogo e seus dados gerados.

---

## Funcionamento
O `TelemetryLogger` registra snapshots periódicos e eventos como dano, morte, ataques, parries e dash. Ao pressionar F9, salva uma sessão JSON em `Application.persistentDataPath/telemetry`.

O contrato Protobuf organiza uma mensagem inicial de sessão seguida por lotes de frames e eventos.

O `TelemetryService` expõe dois RPCs:
- `StreamTelemetry`: stream bidirecional para receber lotes durante uma sessão e devolver confirmações (`BatchAck`)
- `SendBatch`: envio de um lote isolado, útil para testes e reprocessamento.

---

## Configuração
### 1. Pré-requisitos
```
Python 3.12+
PostgreSQL instalado e em execução
.NET SDK ou Visual Studio para compilar o mod
Jogo com BepInEx configurado
```

### 2. Instalação das dependências Python
```
python -m pip install -r requirements.txt
```

### 3. Gerar os arquivos Protobuf
Os arquivos gerados já estão versionados no diretório `training/`. Para regenerá-los após alterar o contrato:
```
python -m grpc_tools.protoc -I "training/Phase 2" --python_out="training/Phase 2" --grpc_python_out="training/Phase 2" "training/Phase 2/telemetry.proto"
```

### 4. Criar o banco de dados
Crie um banco PostgreSQL e execute o schema:
```
psql -U postgres -d telemetry -f schema.sql
```

O serviço também pode receber a conexão pela variável `TELEMETRY_DSN`:
```
TELEMETRY_DSN="dbname=telemetry user=postgres password=postgres host=localhost"
```

---

## Execução
### Iniciar o serviço gRPC
```
python "training/Phase 2/telemetry_service.py" --port 50051 --dsn "dbname=telemetry user=postgres password=postgres host=localhost"
```

### Usar Docker
O `docker compose` inicia PostgreSQL, cria o schema e executa o serviço gRPC:
```
docker compose up --build
```

### Scripts históricos
Os scripts em `old/` não fazem parte da execução principal. Eles são mantidos como registro da pipeline experimental de treinamento e podem exigir dependências adicionais, como pandas, NumPy, scikit-learn, joblib, skl2onnx e onnx.
```
# 1. Gera dados sintéticos
python "old/1 - generate_synthetic_data.py" --sessions 50 --out data/raw

# 2. Constrói o dataset com engenharia de features
python "old/2 - build_dataset.py" --raw-dir data/raw --out dataset.csv --horizon-ms 5000

# 3. Treina o Random Forest
python "old/3 - train_model.py" --dataset dataset.csv --out model.pkl

# 4. Exporta o modelo para ONNX
python "old/4 - export_onnx.py" --model model.pkl --dataset dataset.csv --out model.onnx
```

### Compilar e instalar o mod
Compile `mod/Assembly-CSharp.csproj` e copie a DLL gerada para:
```
game/BepInEx/plugins/
```
