# client-side-player-prediction

Previsão de ações do jogador com Machine Learning para compensação de
latência em MMORPGs — estudo de caso no servidor `tmwa` (TheManaWorld) e
no cliente ManaVerse.

## Estrutura deste repositório

```
/
├── app/                   # todo o código/pipeline (Python + patches C++) -- ver app/README.md
├── TMWA/                  # clone do servidor (vazio no git -- so .gitkeep)
├── manaverse-mirror/      # clone do cliente (vazio no git -- so .gitkeep)
└── README.md              # este arquivo
```

`TMWA/` e `manaverse-mirror/` são **clones git de outros repositórios**
(o jogo em si, GPL-2.0+) -- não ficam versionados aqui dentro, só os
arquivos de integração (em `app/tmwa/` e `app/manaverse/`) que precisam
ser copiados/aplicados neles.

## Setup de um clone novo

### 1. Clonar este repositório

```bash
git clone https://github.com/kisalto/client-side-player-prediction.git
cd client-side-player-prediction
```

### 2. Clonar o TMWA (servidor) dentro de `TMWA/`

```bash
git clone https://github.com/themanaworld/tmwa.git TMWA
```

Usamos o **upstream** oficial aqui (não o fork pessoal
`kisalto/tmwa`), porque o patch em `app/tmwa/pc.cpp.patch` foi gerado e
validado contra ele -- aplicar num fork que já tem outras mudanças
pessoais pode gerar conflito. Se você quiser continuar usando seu fork
pessoal diretamente, veja a nota em `app/tmwa/README.md`.

### 3. Clonar o ManaVerse (cliente) dentro de `manaverse-mirror/`

```bash
git clone https://github.com/kisalto/manaverse-mirror.git manaverse-mirror
```

(Esse já é o seu próprio fork/mirror -- os hooks desta etapa já estão
commitados nele, então o passo 4 vai detectar isso e pular a reaplicação
automaticamente.)

### 4. Copiar e aplicar os arquivos de integração

```bash
bash app/setup.sh
```

Esse script:
- copia `app/tmwa/{telemetry.hpp,telemetry.cpp}` para `TMWA/src/map/` e
  aplica `app/tmwa/pc.cpp.patch` (a menos que detecte que já está
  aplicado);
- copia `app/manaverse/{beingactionpredictor.h,beingactionpredictor.cpp}`
  para `manaverse-mirror/src/ml/` e aplica
  `app/manaverse/{being.cpp.patch,Makefile.am.patch}` (idem).

É seguro rodar mais de uma vez (idempotente).

### 5. Compilar e rodar o pipeline

A partir daqui, siga:
- `app/tmwa/README.md` -- compilar o servidor com telemetria
- `app/manaverse/FASE8_INTEGRACAO.md` -- compilar o cliente com ONNX Runtime
- `app/README.md` -- rodar o `TelemetryService`/Postgres e o pipeline Python (dataset, treino, export ONNX)
