# Fase 8 -- Integração no ManaVerse (cliente)

**Status: escrito e revisado com cuidado, mas NÃO compilado.** Diferente do
`tmwa` (servidor, que compilei de verdade -- ver `docs/architecture.md`),
o ManaVerse é um cliente gráfico grande (SDL/OpenGL, 720 arquivos `.cpp`),
inviável de compilar no sandbox no tempo disponível. Trate este código com
mais cautela: compile localmente e me manda o primeiro erro se algo não
bater.

## O que os dois arquivos fazem

`beingactionpredictor.h`/`.cpp` (nesta pasta): carrega o `model.onnx` (da
Fase 7) via ONNX Runtime C++, mantém um histórico leve por `Being` (posição,
última ação, último dano -- os mesmos campos que `build_dataset.py` usa),
e expõe `getAttackProbability(Being*)` -- a probabilidade de aquela
entidade atacar em seguida, ANTES do pacote de dano real confirmar isso.
**Nunca aplica dano nem muda qualquer estado de jogo** -- é só para
disparar uma pista visual antecipada opcional (telegraph). Autoridade
continua 100% do servidor.

## 1. Onde colocar os arquivos

Criar `src/ml/` e colocar os dois arquivos lá (paralelo a `src/being/`,
`src/net/` etc.).

## 2. CMakeLists.txt -- adicionar o ONNX Runtime

O projeto usa CMake. Duas formas de trazer o ONNX Runtime C++ (escolha uma):

**Opção A -- pacote pré-compilado (mais simples)**: baixar o release
oficial (`onnxruntime-linux-x64-<versão>.tgz` em
github.com/microsoft/onnxruntime/releases), e no `CMakeLists.txt`:

```cmake
set(ONNXRUNTIME_ROOT "/caminho/pra/onnxruntime-linux-x64-1.20.0" CACHE PATH "ONNX Runtime root")
include_directories(${ONNXRUNTIME_ROOT}/include)
link_directories(${ONNXRUNTIME_ROOT}/lib)
# ... no target do executavel principal:
target_link_libraries(manaverse onnxruntime)
```

**Opção B -- vcpkg/conan**, se o projeto já usar um gerenciador de pacotes
C++ (não confirmei se usa -- não vi nada no `CMakeLists.txt` que sugerisse
isso, então provavelmente é a Opção A mesmo).

Adicionar `src/ml/beingactionpredictor.cpp` à lista de fontes (o mesmo
`GLOB_RECURSE` que o `tmwa` usa pode já pegar isso automaticamente -- se o
ManaVerse listar arquivos manualmente em vez de globbing, precisa
adicionar a linha à mão).

## 3. Inicialização (uma vez, no boot do cliente)

Em algum lugar do startup (onde outros sistemas são inicializados -- não
encontrei o ponto exato porque não é sobre `being.cpp`, é sobre o loop de
boot geral do client, que não explorei a fundo):

```cpp
#include "ml/beingactionpredictor.h"

BeingActionPredictor::instance().init("model.onnx");  // caminho do arquivo exportado na Fase 7
```

## 4. Hooks -- 3 pontos em `src/being/being.cpp`

```cpp
#include "ml/beingactionpredictor.h"
```

**a) `Being::setDestination(x, y)`** -- no topo da função, ANTES de `mX`/
`mY` mudarem (a posição "atual" que o preditor vai usar depois é a de
antes deste comando):

```cpp
void Being::setDestination(const int dstX, const int dstY) restrict2
{
    BeingActionPredictor::instance().onBeingMove(this);
    // ... resto da funcao como ja esta
```

**b) `Being::handleAttack(...)`**, perto da linha que já existe
`setAction(BeingAction::ATTACK, attackId);` (por volta da linha 996 na
versão que usei):

```cpp
    setAction(BeingAction::ATTACK, attackId);
    BeingActionPredictor::instance().onBeingAttack(this);
```

**c) `Being::takeDamage(...)`** (por volta da linha 670) -- perto do
início, onde `damage` já é conhecido:

```cpp
void Being::takeDamage(Being *restrict const attacker,
                        const int damage, ...)
{
    BeingActionPredictor::instance().onBeingHurt(this, damage);
    // ... resto da funcao
```

## 5. Chamada periódica + a pista visual (decisão sua)

Dentro de `Being::logic()` (por volta da linha 1860), throttlada -- **não**
chamar toda frame:

```cpp
// dentro de logic(), com algum controle de intervalo (ex: a cada 200-300ms
// por being, nao a cada frame -- reaproveite algum contador que a funcao
// ja tenha, ou adicione um novo tipo Timer/int igual mMoveTime)
if (this != localPlayer && /* throttle */)
{
    const float p = BeingActionPredictor::instance().getAttackProbability(this);
    if (p > 0.7F)  // limiar -- ajuste ao gosto, teste com dados reais depois
    {
        // AQUI: disparar a pista visual antecipada. Não sei qual sistema
        // de efeito/partícula do ManaVerse fica melhor pra isso -- não
        // explorei essa parte do código a fundo, e é mais uma escolha de
        // gosto/produto sua do que uma decisão técnica. Ideias: um brilho
        // sutil na arma, um leve squash-and-stretch no sprite, um
        // partícula pequena. O importante: precisa ser visualmente
        // DISTINTO da animação real de ataque (que ainda vai tocar
        // normalmente quando o pacote de dano confirmar), senão fica
        // confuso quando a previsão erra.
    }
}
```

## Resumo do que falta você decidir/testar

1. Qual sistema de efeito usar pro telegraph visual (item 5 acima).
2. Onde exatamente fica o boot/init geral do cliente (item 3).
3. Compilar e me mandar o primeiro erro, se houver.
