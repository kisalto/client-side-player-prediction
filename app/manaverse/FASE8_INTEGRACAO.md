# Fase 8 -- Integração no ManaVerse (cliente)

**Status: COMPILADO, LINKADO, TESTADO em runtime (parcialmente) e BOOT
VERIFICADO.** Compilado e linkado contra o build real do ManaVerse com ONNX
Runtime; a lógica do preditor e o contrato com o `model.onnx` têm testes
automatizados (`tests/`); e o cliente real foi iniciado em modo headless
e comprovadamente abriu o `model.onnx` no boot sem crashar (detalhes na
seção 6). **Ainda não foi testado jogando de verdade** (conectado a um
servidor, com o efeito visual criado) -- ver seção 7. Esta versão substitui uma entrega
anterior que dizia pra mexer num `CMakeLists.txt` -- estava **errada**: o
próprio `INSTALL` do projeto diz que o CMake é "unsupported (not for
regular use)". O build de verdade é **GNU Autotools**
(`configure.ac` + `Makefile.am`). As instruções abaixo já refletem isso e
foram validadas compilando de verdade.

## 1. Dependências (via apt, Ubuntu/Debian)

```bash
sudo apt-get install -y automake autoconf libtool gettext autopoint \
  libsdl1.2-dev libsdl-mixer1.2-dev libsdl-image1.2-dev \
  libsdl-net1.2-dev libsdl-ttf2.0-dev libsdl-gfx1.2-dev \
  libxml2-dev zlib1g-dev libcurl4-openssl-dev libpng-dev
```

## 2. ONNX Runtime (C++, pré-compilado)

```bash
wget https://github.com/microsoft/onnxruntime/releases/download/v1.20.1/onnxruntime-linux-x64-1.20.1.tgz
tar xzf onnxruntime-linux-x64-1.20.1.tgz
# gera onnxruntime-linux-x64-1.20.1/{include,lib}/ -- anote o caminho absoluto
```

## 3. Arquivos deste pacote (`app/manaverse/`)

| Arquivo | Vai pra | O que faz |
|---|---|---|
| `beingactionpredictor.h` / `.cpp` | `src/ml/` | o preditor (ONNX Runtime, histórico por being, throttle 200ms, latch do telegraph) |
| `being.cpp.patch` | `src/being/being.cpp` | 3 hooks: movimento, dano, ataque (`onBeingMove/Hurt/Attack`) |
| `Makefile.am.patch` | `src/Makefile.am` | registra os 2 arquivos novos |
| `being-telegraph.patch` | `src/being/being.cpp` | em `Being::logic()`: dispara o efeito visual quando o modelo prevê ataque iminente (só beings remotas) |
| `client.cpp.patch` | `src/progs/manaverse/client.cpp` | em `Client::gameInit()`, logo após `PlayerInfo::init()`: carrega `<localDataDir>/model.onnx` |
| `tests/` | -- | testes de runtime (seção 6) |

Os patches são **incrementais e independentes** (cada um tem um marcador
próprio): `app/setup.sh` (raiz do repo) aplica só o que falta, então
funciona tanto num ManaVerse limpo quanto no `manaverse-mirror`, que já
tem os hooks da v1 commitados. Testado nos dois cenários; o resultado é
byte a byte idêntico à árvore que foi compilada e testada.

Manualmente, se preferir:

```bash
cd manaverse-mirror
mkdir -p src/ml
cp /caminho/app/manaverse/beingactionpredictor.{h,cpp} src/ml/
git apply /caminho/app/manaverse/being.cpp.patch        # (pule se ja tiver os hooks)
git apply /caminho/app/manaverse/Makefile.am.patch      # (idem)
git apply /caminho/app/manaverse/being-telegraph.patch
git apply /caminho/app/manaverse/client.cpp.patch
```

## 4. Compilar -- precisa de C++17, não o C++11 padrão do projeto

Achado real ao compilar: o `onnxruntime_cxx_api.h` usa `constexpr` de um
jeito que só existe a partir do C++14, e este código usa
`std::make_unique` (C++14) e `std::clamp` (C++17). O projeto compila em
C++11 por padrão -- forçar C++17 só nessa build resolveu, sem quebrar
nada do resto do código (C++11 é compatível "pra frente" com C++17 quase
sempre).

```bash
cd manaverse-mirror
autoreconf -i
./configure --without-opengl   # ou com OpenGL, se tiver os headers/driver
automake                       # regenera o Makefile apos o Makefile.am.patch

cd src
make CXXFLAGS="-std=gnu++17 -I/caminho/pro/onnxruntime/include" \
     LDFLAGS="-L/caminho/pro/onnxruntime/lib -Wl,-rpath,/caminho/pro/onnxruntime/lib" \
     LIBS="-lrt -lpng -lxml2 -lcurl -lz -lpthread -lSDL_net -lSDL_gfx -lSDL_mixer -lSDL_ttf -lSDL_image -lSDL -lX11 -lonnxruntime" \
     manaplus
```

**Atenção com `LIBS`**: se você só passar `-lonnxruntime` (em vez de
repetir todas as libs originais + `-lonnxruntime`), o `make` vai
SUBSTITUIR a lista de libs do projeto em vez de complementar -- isso gera
uma pilha de erros `undefined reference to SDL_...`. A lista de libs
acima veio direto do `Makefile` gerado pelo `./configure` deste projeto --
se você mudar opções do `configure` (habilitar OpenGL, trocar SDL1↔SDL2
etc.), a lista pode mudar; confira com `grep "^LIBS =" src/Makefile`
antes de linkar, caso o comando acima dê erro de link.

O binário final chama `manaplus` (nome interno herdado do fork), não
`manaverse` -- isso é normal, não é um erro.

## 5. Colocar o modelo e o efeito no lugar

- **`model.onnx`**: gerado por `app/pipeline/export_onnx.py`. O cliente procura
  em `<localDataDir>/model.onnx` (no Linux, tipicamente
  `~/.local/share/mana/` -- ou o que você passar em `-L/--localdata-dir`).
  Se o arquivo não existir ou falhar ao carregar, o preditor fica
  **desligado** e o cliente funciona normalmente (testado).
- **Efeito visual (você precisa criar)**: `being-telegraph.patch` chama
  `effectManager->trigger(9001, this, 0)`. O id **9001 é um placeholder**: o
  efeito em si (partícula/sprite/som) é dado do cliente -- não achei o
  arquivo de efeitos neste repo (provavelmente fica no pacote de dados do
  ManaVerse) e não criei um. Enquanto o id 9001 não existir nos dados,
  `EffectManager::trigger()` só retorna `false` e **não faz nada** (confirmado
  lendo `effectmanager.cpp`) -- seguro deixar assim. O efeito deve ser
  visualmente **distinto** da animação real de ataque, senão confunde quando
  a previsão erra. O limiar de disparo é 0,7 (`checkAndConsumeTelegraph`);
  o disparo acontece **uma vez** por "subida" acima do limiar (rearma quando
  cai abaixo).

## 6. O que foi testado de verdade

**Compilação/link**: cliente completo (`manaplus`) compilado com C++17 e
linkado com `libonnxruntime.so.1`; `nm` confirma todos os símbolos.

**Testes automatizados** (`tests/run_tests.sh`, não precisa do
cliente): `ONNXRUNTIME_DIR=/caminho/onnxruntime bash tests/run_tests.sh`

- **A) contrato do ONNX em C++ vs sklearn** -- entrada `input` `[N,13]`,
  saída `probabilities`, índice 0 = `attack`: 60 linhas, diferença máxima
  3,5e-7.
- **B) a classe real** (`beingactionpredictor.cpp` sem modificar, contra um
  `Being` stub, porque o `Being` verdadeiro puxa o cliente inteiro): 14
  checagens -- modelo inexistente não crasha, sem histórico = 0, throttle
  devolve o cacheado, recálculo após 200ms, telegraph dispara 1x/rearma,
  `forget()`.
- **B2)** os valores calculados pela classe C++ (0,675 e 0,200) batem com o
  sklearn para o mesmo vetor de features.

Esses testes acharam **um bug real** na minha lógica: com o modelo
desligado `getAttackProbability` devolve 0, e com limiar <= 0 o telegraph
disparava mesmo assim. Corrigido (e coberto pelo teste).

Ressalva: o modelo usado nos testes é um RandomForest pequeno treinado em
dados aleatórios, com **a mesma estrutura** do pipeline real (13 features
float, `Pipeline[FunctionTransformer -> RandomForest]`, classes
`['attack','move']`, `zipmap=False`) -- o teste valida o *contrato*, não a
qualidade preditiva. Para o seu `model.onnx` real, vale rodar a parte A
trocando o arquivo.

**Boot do cliente real (headless)**: `Xvfb` + `SDL_AUDIODRIVER=dummy`,
`./manaplus -d <repo>/data -L <pasta-com-model.onnx>`; 20-30s rodando sem
crash (o `timeout` é que encerra), e `strace -e openat` mostra o cliente
abrindo `<localdata>/model.onnx` e carregando a `libonnxruntime`. Sem o
`-d`, o cliente morre em `Font::Font` por não achar as fontes (dados não
instalados) -- isso é do ambiente de teste, não do preditor (acontece antes
do `init()`).

## 7. O que ainda NÃO foi testado

1. **Jogar de verdade**: conectar a um servidor, ver beings remotas agindo e
   confirmar que `getTileX()/getHP()` etc. se comportam como esperado fora do
   stub (ex.: HP de beings remotas pode não ser conhecido pelo cliente --
   nesse caso `hp_ratio` sai 0 e a previsão degrada).
2. **O efeito visual** (seção 5): criar o efeito 9001 nos dados e ver se
   fica bom/legível.
3. **Custo de desempenho** com muitas beings na tela (a inferência é
   throttlada a 1x/200ms por being, mas não medi com 50-100 beings).
4. **Qualidade preditiva real**: depende de telemetria real (hoje o
   `model.onnx` do pipeline foi treinado só com dados sintéticos).
