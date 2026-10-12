// Teste B: BeingActionPredictor REAL (arquivo sem modificacao) com Being stub
// (stub/being/being.h) -- o Being de verdade puxa o cliente inteiro.
#include "beingactionpredictor.h"
#include "being/being.h"
#include <chrono>
#include <cstdio>
#include <thread>
static int fails = 0;
#define CHECK(cond, msg) do { bool ok_=(cond); std::printf("[%s] %s\n", ok_?"OK":"FALHA", msg); if(!ok_) ++fails; } while(0)
static void sleepMs(int ms){ std::this_thread::sleep_for(std::chrono::milliseconds(ms)); }
int main() {
    auto &P = BeingActionPredictor::instance();

    // 1) modelo inexistente: nao pode crashar, deve ficar desligado (prob 0)
    P.init("/caminho/que/nao/existe/model.onnx");
    Being ghost; ghost.id = 1;
    P.onBeingMove(&ghost); sleepMs(30);
    CHECK(P.getAttackProbability(&ghost) == 0.0F, "init com modelo inexistente nao crasha e prob = 0");
    CHECK(P.checkAndConsumeTelegraph(&ghost, 0.0F) == false, "telegraph desligado sem modelo");

    // 2) modelo real
    P.init("model.onnx");
    Being b; b.id = 7; b.x = 50; b.y = 60; b.hp = 35; b.maxHp = 100;
    CHECK(P.getAttackProbability(&b) == 0.0F, "sem historico -> prob 0");

    P.onBeingMove(&b); sleepMs(50);
    float p1 = P.getAttackProbability(&b);
    CHECK(p1 >= 0.0F && p1 <= 1.0F, "prob em [0,1] apos 1 acao registrada");
    std::printf("      p1 = %.6f (hp 35/100, ultima acao = move ~50ms atras)\nP1 %.6f\n", p1, p1);

    b.hp = 99; // mudar o estado: se NAO houvesse throttle, o valor mudaria
    float p2 = P.getAttackProbability(&b);
    CHECK(p2 == p1, "throttle: chamada imediata devolve o valor cacheado");

    sleepMs(260); // passa de 200ms -> recalcula
    float p3 = P.getAttackProbability(&b);
    CHECK(p3 >= 0.0F && p3 <= 1.0F, "apos o throttle expirar, recalcula (em [0,1])");
    std::printf("      p3 = %.6f (hp 99/100 -- recalculado)\nP3 %.6f\n", p3, p3);

    // 3) logica de telegraph (borda de subida) -- deterministica via limiar
    Being c; c.id = 8; c.hp = 50;
    P.onBeingAttack(&c); sleepMs(30);
    CHECK(P.checkAndConsumeTelegraph(&c, 2.0F) == false, "limiar impossivel (2.0) nunca dispara");
    CHECK(P.checkAndConsumeTelegraph(&c, 0.0F) == true,  "cruza o limiar -> dispara 1x");
    CHECK(P.checkAndConsumeTelegraph(&c, 0.0F) == false, "continua acima -> NAO dispara de novo (latch)");
    CHECK(P.checkAndConsumeTelegraph(&c, 0.0F) == false, "idem, 3a chamada");
    CHECK(P.checkAndConsumeTelegraph(&c, 2.0F) == false, "cai abaixo do limiar -> rearma");
    CHECK(P.checkAndConsumeTelegraph(&c, 0.0F) == true,  "sobe de novo -> dispara outra vez");

    // 4) forget
    P.forget(&c);
    CHECK(P.getAttackProbability(&c) == 0.0F, "forget() limpa o historico (prob volta a 0)");

    std::printf("\n%s (%d falha(s))\n", fails ? "ALGUM TESTE FALHOU" : "TODOS OS TESTES PASSARAM", fails);
    return fails ? 1 : 0;
}
