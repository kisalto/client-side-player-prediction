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

(Esse já é o seu próprio fork/mirror e já tem os hooks da primeira
versão commitados -- o passo 4 detecta isso e aplica só o que falta: o
telegraph em `Being::logic()` e o `init()` do modelo no boot.)

### 4. Copiar e aplicar os arquivos de integração

```bash
bash app/setup.sh
```

Esse script:
- copia `app/tmwa/{telemetry.hpp,telemetry.cpp}` para `TMWA/src/map/` e
  aplica `app/tmwa/pc.cpp.patch` (a menos que detecte que já está
  aplicado);
- copia `app/manaverse/{beingactionpredictor.h,beingactionpredictor.cpp}`
  para `manaverse-mirror/src/ml/` e aplica os 4 patches de
  `app/manaverse/` (`being.cpp`, `Makefile.am`, `being-telegraph`, `client.cpp`),
  cada um só se ainda não estiver aplicado (idem).

É seguro rodar mais de uma vez (idempotente).

### 5. Compilar e rodar o pipeline

A partir daqui, siga:
- `app/tmwa/README.md` -- compilar o servidor com telemetria
- `app/manaverse/FASE8_INTEGRACAO.md` -- compilar o cliente com ONNX Runtime
- `app/README.md` -- rodar o `TelemetryService`/Postgres e o pipeline Python (dataset, treino, export ONNX)

## Mudanças pendentes encontradas numa inspeção do repositório (29/09)

Isso foi corrigido nesta própria reorganização, mas fica registrado aqui
pra contexto:

1. **`pc.cpp` do fork pessoal (`kisalto/tmwa`) estava incompleto**: tinha
   os hooks de dano/morte/init, mas não os de comando de movimento
   (`pc_walktoxy`) nem de ataque (`pc_attack`) -- mesmo o
   `telemetry.hpp`/`.cpp` de lá já tendo essas funções prontas desde a
   Fase 4/6. Sem eles, nenhum dado de ação real seria coletado. Ver
   detalhes em `app/tmwa/README.md`.
2. **`app/manaverse/FASE8_INTEGRACAO.md` estava desatualizado**: ainda
   instruía a mexer num `CMakeLists.txt`, mas o build real do ManaVerse é
   via GNU Autotools (o próprio projeto marca o CMake como
   "unsupported"). Reescrito com os passos validados de verdade
   (incluindo a exigência de C++17, não C++11, para compilar com o ONNX
   Runtime).
3. **Faltavam os patches do lado do cliente** (`being.cpp.patch`,
   `Makefile.am.patch`) no repositório -- existia só o
   `beingactionpredictor.h/.cpp` solto. Gerados agora e validados com
   `git apply --check`.

## Problema encontrado e NÃO resolvido (decisão sua)

Há uma pasta `old project/` na raiz do repositório original (~133MB),
com binários de um projeto Unity completamente diferente (parece um jogo
chamado "[K]night City", com arquivos BepInEx) -- não relacionado ao
`tmwa`/ManaVerse. Não a movi para `app/` (não parece ser "o projeto
atual") nem a apaguei (não é uma decisão que me cabe tomar sozinho).
Ela **permanece intacta na raiz** deste repositório reorganizado, fora da
estrutura `app/`/`TMWA/`/`manaverse-mirror/` pedida. Recomendo decidir
entre: mover pra um repo separado, ou remover do histórico do git (ela
infla bastante o tamanho do clone).

Também notei, ao comparar `kisalto/tmwa` com o upstream, algumas mudanças
sem relação com a telemetria (um divisor de tempo de ataque, remoção de
duas chamadas a `pc_setstand`, remoção de uma checagem de limite
`skill_num < MAX_SKILL`, e uma conta de bitmask de quest) -- não mexi em
nenhuma delas, só registrei em `app/tmwa/README.md` pra você ter
ciência, já que a checagem de limite removida em especial vale uma
segunda olhada sua.

## Licença

Ver `app/README.md`.
