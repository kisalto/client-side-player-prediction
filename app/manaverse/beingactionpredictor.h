// beingactionpredictor.h
// ---------------------------------------------------------------------------
// Fase 8: usa o model.onnx (treinado no repo client-side-player-prediction,
// Fase 5-7) pra estimar a probabilidade de uma Being REMOTA (nao o
// jogador local -- esse ja tem client-side prediction otimista de verdade,
// ver localplayer.cpp) atacar em seguida, ANTES do pacote de dano real
// confirmar isso. Usado so pra disparar uma pista visual antecipada
// (telegraph) -- NUNCA aplica dano nem altera qualquer estado de jogo:
// a autoridade continua 100% do servidor, isso e cosmetico.
//
// PIMPL de proposito: esconde os tipos do ONNX Runtime (Ort::Env,
// Ort::Session etc.) daqui, pra nao vazar o include/dependencia de
// onnxruntime pra todo arquivo que inclua este header.
//
// AVISO HONESTO: escrevi isto lendo being.h/being.cpp/beingid.h/
// actorsprite.h diretamente (mesmo rigor do resto do projeto), mas NAO
// consegui compilar contra o build de verdade do ManaVerse -- e um
// cliente grafico grande (SDL/OpenGL, 720 arquivos .cpp), inviavel de
// compilar no sandbox no tempo disponivel (diferente do tmwa, que
// compilei de verdade). Trate isto com mais cautela que o resto do
// projeto: teste local antes de confiar.

#ifndef ML_BEINGACTIONPREDICTOR_H
#define ML_BEINGACTIONPREDICTOR_H

#include <memory>
#include <string>
#include <unordered_map>

class Being;

class BeingActionPredictor final
{
    public:
        static BeingActionPredictor &instance();

        BeingActionPredictor(const BeingActionPredictor &) = delete;
        BeingActionPredictor &operator=(const BeingActionPredictor &) = delete;

        // chamar uma vez no boot do cliente (ex: perto de onde outros
        // sistemas graficos/config sao inicializados). modelPath = caminho
        // pro model.onnx (ver export_onnx.py). Se falhar (arquivo nao
        // existe, ONNX Runtime nao disponivel etc.), fica desligado e
        // getAttackProbability sempre retorna 0 -- nunca quebra o cliente
        // por causa disso.
        void init(const std::string &modelPath);

        // --- hooks: chamar destes pontos exatos (ver comentarios) ---

        // Being::setDestination(x, y), ANTES de atualizar mX/mY (ou
        // guarde a posicao atual antes de chamar) -- registra que essa
        // being comandou um movimento agora.
        void onBeingMove(const Being *being);

        // Being::handleAttack(victim, damage, attackId) -- "being" aqui e
        // o ATACANTE (this dentro de handleAttack), nao a vitima.
        void onBeingAttack(const Being *being);

        // Being::takeDamage(attacker, damage, ...) -- "being" aqui e a
        // VITIMA (this dentro de takeDamage).
        void onBeingHurt(const Being *being, int damage);

        // chamar periodicamente por being visivel (ex: dentro de
        // Being::logic(), joga throttling ali -- nao chamar toda frame
        // pra toda being, o overhead de inferencia nao compensa nesse
        // ritmo). Retorna P(proxima acao = ataque) em [0,1], ou 0 se o
        // modelo nao carregou ou a being nao tem historico suficiente
        // ainda (precisa de pelo menos 1 acao anterior registrada).
        float getAttackProbability(const Being *being);

        // limpa o historico de uma being (chamar quando ela sai de
        // alcance/e destruida, pra nao vazar memoria indefinidamente).
        void forget(const Being *being);

    private:
        BeingActionPredictor();
        ~BeingActionPredictor();

        struct History
        {
            int lastX = 0;
            int lastY = 0;
            double lastPosTime = -1.0;
            double lastActionTime = -1.0;
            double lastHurtTime = -1.0;
            int lastDamage = 0;
            bool prevAttack = false;   // ultima acao foi ataque? (senao, foi move)
            bool hasHistory = false;   // ja teve pelo menos 1 acao registrada?
        };

        History &historyFor(const Being *being);
        static double nowSeconds();

        struct Impl;                  // esconde Ort::Env/Ort::Session daqui
        std::unique_ptr<Impl> mImpl;
        std::unordered_map<int, History> mHistory;
        bool mReady = false;
};

#endif  // ML_BEINGACTIONPREDICTOR_H
