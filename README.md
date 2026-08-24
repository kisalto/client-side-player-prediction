# **Projeto Secreto**

## **Sumário**
- [Descrição Geral](#descrição-geral)
- [Tecnologias Utilizadas](#tecnologias-utilizadas)
- [Estrutura do Repositório](#estrutura-do-repositório)
- [Funcionamento](#funcionamento)
- [Configuração](#configuração)
- [Execução](#execução)

---

## **Descrição Geral**
Este projeto coleta telemetria do jogo [K]night City, envia os dados para um serviço gRPC e prepara modelos de Machine Learning para estimar o risco de morte do jogador.

O fluxo principal é:
1. O mod carregado pelo BepInEx injeta o `TelemetryLogger` no jogo.
2. O logger captura snapshots do jogador, inimigos, chefes e projéteis.
3. O serviço gRPC recebe os lotes definidos em `telemetry.proto` e grava tudo no PostgreSQL.
4. Os scripts de treinamento geram features, treinam um Random Forest e exportam o modelo para ONNX.

---

## **Tecnologias Utilizadas**
- **C# / Unity**: coleta de telemetria dentro do jogo
- **BepInEx**: carregamento do mod
- **Python**: serviço gRPC e pipeline de Machine Learning
- **gRPC / Protocol Buffers**: comunicação entre o jogo e o serviço
- **PostgreSQL**: armazenamento de sessões, frames e eventos
- **pandas / NumPy**: preparação e engenharia de features
- **scikit-learn**: treinamento do Random Forest
- **skl2onnx**: exportação do modelo para ONNX

---

## **Estrutura do Repositório**
- **training/**:
	- `1_TelemetryLogger.cs`: captura snapshots e eventos do jogo
	- `2_Plugin.cs`: injeta o logger através do BepInEx
	- `3_schema.sql`: cria as tabelas do PostgreSQL
	- `4_telemetry_service.py`: servidor gRPC que persiste a telemetria
	- `telemetry.proto`: contrato das mensagens e RPCs
	- `telemetry_pb2.py` e `telemetry_pb2_grpc.py`: código Python gerado pelo Protobuf
- **old/**:
	- `1 - generate_synthetic_data.py`: gera sessões falsas para testes
	- `2 - build_dataset.py`: constrói o dataset e calcula as features
	- `3 - train_model.py`: treina e salva o Random Forest
	- `4 - export_onnx.py`: converte o modelo para ONNX
- **mod/**: projeto C# usado para compilar o plugin
- **BepInEx/**: instalação e configuração do carregador de mods
- **game/**: arquivos locais do jogo e seus dados gerados

---

## **Funcionamento**
O `TelemetryLogger` registra snapshots periódicos e eventos como dano, morte, ataques, parries e dash. O contrato Protobuf organiza esses dados em uma mensagem inicial de sessão seguida por lotes de frames e eventos.

O `TelemetryService` expõe dois RPCs:
- `StreamTelemetry`: stream bidirecional para receber lotes durante uma sessão e devolver confirmações (`BatchAck`)
- `SendBatch`: envio de um lote isolado, útil para testes e reprocessamento

Os scripts em `old/` usam arquivos JSON no formato da coleta sintética para validar a pipeline de treinamento antes da integração completa com o serviço gRPC.

---

## **Configuração**
### 1. Pré-requisitos
```
Python 3.10+
PostgreSQL instalado e em execução
.NET SDK ou Visual Studio para compilar o mod
Jogo com BepInEx configurado
```

### 2. Instalação das dependências Python
```
python -m pip install pandas numpy scikit-learn joblib skl2onnx onnx grpcio grpcio-tools psycopg2-binary protobuf
```

### 3. Gerar os arquivos Protobuf
Os arquivos gerados já estão versionados no diretório `training/`. Para regenerá-los após alterar o contrato:
```
python -m grpc_tools.protoc -I training --python_out=training --grpc_python_out=training training/telemetry.proto
```

### 4. Criar o banco de dados
Crie um banco PostgreSQL e execute o schema:
```
psql -U postgres -d telemetry -f training/3_schema.sql
```

O serviço também pode receber a conexão pela variável `TELEMETRY_DSN`:
```
TELEMETRY_DSN="dbname=telemetry user=postgres password=postgres host=localhost"
```

---

## **Execução**
### Iniciar o serviço gRPC
```
python training/4_telemetry_service.py --port 50051 \
	--dsn "dbname=telemetry user=postgres password=postgres host=localhost"
```

### Executar a **antiga** pipeline de treinamento
Os scripts de treinamento ficam em `old/`:
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
