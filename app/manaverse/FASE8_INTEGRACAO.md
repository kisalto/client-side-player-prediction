# Fase 8 -- Integração no ManaVerse (cliente)

**Status: COMPILADO E LINKADO com sucesso** contra o build real do
ManaVerse, com ONNX Runtime de verdade. Esta versão substitui uma entrega
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

- `beingactionpredictor.h` / `.cpp` -- vão em `src/ml/` dentro do clone do ManaVerse
- `being.cpp.patch` -- 3 hooks em `src/being/being.cpp` (movimento, dano, ataque)
- `Makefile.am.patch` -- registra os 2 arquivos novos em `src/Makefile.am`

O script `app/setup.sh` (raiz do repo) já faz as cópias e aplica os
patches automaticamente -- ver README raiz. Esta seção documenta o que
ele faz, caso precise rodar manualmente.

```bash
cd manaverse-mirror
mkdir -p src/ml
cp /caminho/app/manaverse/beingactionpredictor.{h,cpp} src/ml/
git apply /caminho/app/manaverse/being.cpp.patch
git apply /caminho/app/manaverse/Makefile.am.patch
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

## 5. Rodar o modelo (falta fazer)

`BeingActionPredictor::instance().init("model.onnx")` precisa ser chamado
uma vez no boot -- não foi localizado o ponto exato de inicialização
geral do client (não é sobre `being.cpp`, é sobre o loop de boot geral,
que não foi explorado a fundo). Coloca o `model.onnx` (gerado por
`app/pipeline/export_onnx.py`) em algum caminho que o cliente consiga
achar em tempo de execução.

## 6. O que ainda falta decidir/testar

1. **Onde inicializar** (`init()`) -- ver item 5.
2. **A pista visual** do "telegraph": dentro de `Being::logic()`,
   throttlado, chamar `getAttackProbability(this)` e, acima de um limiar,
   disparar algum efeito. Não foi explorado o sistema de
   partículas/efeitos do ManaVerse a fundo pra sugerir qual usar -- isso é
   mais decisão de gosto/produto. O importante: precisa ser visualmente
   **distinto** da animação real de ataque, senão fica confuso quando a
   previsão erra.
3. **Não foi testado rodar o cliente de verdade** (sem display disponível
   no ambiente onde isso foi validado) -- só foi confirmado que compila e
   linka. Erros de runtime só aparecem jogando de verdade.
