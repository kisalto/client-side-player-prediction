// beingactionpredictor.cpp
// ---------------------------------------------------------------------------
// Ver beingactionpredictor.h para o design geral e o aviso honesto sobre
// nao ter sido compilado contra o build de verdade do ManaVerse.
//
// FORMATO DE ENTRADA DO model.onnx (confirmado inspecionando o arquivo
// real gerado por export_onnx.py -- nao adivinhado):
//   input:  1 tensor float32, shape [N, 9], nesta ordem exata:
//     x, y, velocity_x, velocity_y, speed, time_since_last_action,
//     time_since_last_hurt, last_damage_taken, prev_action_was_attack
//     (hp/max_hp/hp_ratio/dead sairam: o cliente nao recebe o HP de
//     jogadores remotos)
//   output usado: "probabilities", shape [N, 2], float --
//     coluna 0 = P(attack), coluna 1 = P(move) (ordem alfabetica das
//     classes do sklearn -- classes_ = ['attack', 'move']). O output
//     "label" (string) existe mas nao e usado aqui de proposito -- pedir
//     so "probabilities" evita lidar com string tensor no ONNX Runtime
//     C++ (mais uma fonte de erro que dava pra cortar).
//
// Se o nome dos outputs mudar numa reexportacao futura, rode o mesmo
// scriptzinho de inspecao usado pra confirmar isto (ver o comentario no
// export_onnx.py) antes de mexer aqui.

#include "beingactionpredictor.h"

#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <vector>

#include <onnxruntime_cxx_api.h>

#include "being/being.h"

namespace
{
    constexpr int NUM_FEATURES = 9;
    constexpr double MAX_SPEED = 500.0;  // mesmo teto de seguranca do build_dataset.py (Fase 4)
    constexpr double MIN_DT_S = 0.05;    // mesmo piso de dt do build_dataset.py
    constexpr double NO_HISTORY_SENTINEL = -1.0;
    // roda inferencia ONNX no maximo a cada INFERENCE_THROTTLE_S por being
    // (200ms) -- o resto das chamadas devolve o valor cacheado. Evita
    // rodar o modelo toda frame pra toda being visivel.
    constexpr double INFERENCE_THROTTLE_S = 0.2;

    // relogio de parede (epoch, s) so pro log de avaliacao -- as features
    // continuam no steady_clock (nowSeconds), que nao pula com ajuste de hora
    double wallSeconds()
    {
        using namespace std::chrono;
        return duration<double>(system_clock::now().time_since_epoch()).count();
    }

    // TCC_PREDICTOR_THRESHOLD sobrescreve o limiar sem recompilar -- o valor
    // calibrado sai de train_model.py (metrics.json -> attack_logo.limiar_cliente).
    // <= 0 = nao definido.
    float envThreshold()
    {
        static const float v = []
        {
            const char *s = std::getenv("TCC_PREDICTOR_THRESHOLD");
            return s != nullptr ? std::strtof(s, nullptr) : -1.0F;
        }();
        return v;
    }
}  // namespace

struct BeingActionPredictor::Impl
{
    Ort::Env env{ORT_LOGGING_LEVEL_WARNING, "BeingActionPredictor"};
    std::unique_ptr<Ort::Session> session;
    Ort::MemoryInfo memoryInfo = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);
    std::FILE *log = nullptr;  // TCC_PREDICTOR_LOG (ver logRow)

    ~Impl()
    {
        if (log != nullptr)
            std::fclose(log);
    }
};

BeingActionPredictor &BeingActionPredictor::instance()
{
    static BeingActionPredictor inst;
    return inst;
}

BeingActionPredictor::BeingActionPredictor() :
    mImpl(std::make_unique<Impl>())
{
}

BeingActionPredictor::~BeingActionPredictor() = default;

double BeingActionPredictor::nowSeconds()
{
    using namespace std::chrono;
    return duration<double>(steady_clock::now().time_since_epoch()).count();
}

void BeingActionPredictor::init(const std::string &modelPath)
{
    try
    {
        Ort::SessionOptions options;
        options.SetIntraOpNumThreads(1);  // modelo pequeno, nao vale gastar mais threads nisso
        mImpl->session = std::make_unique<Ort::Session>(mImpl->env, modelPath.c_str(), options);
        mReady = true;
        std::fprintf(stderr, "[predictor] modelo carregado: %s\n", modelPath.c_str());
        if (envThreshold() > 0.0F)
            std::fprintf(stderr, "[predictor] limiar do telegraph = %.3f (TCC_PREDICTOR_THRESHOLD)\n",
                         static_cast<double>(envThreshold()));

        const char *logPath = std::getenv("TCC_PREDICTOR_LOG");
        if (logPath != nullptr && mImpl->log == nullptr)
        {
            // "a": varias execucoes do cliente vao pro mesmo arquivo; o
            // cabecalho so entra se o arquivo estiver vazio
            mImpl->log = std::fopen(logPath, "a");
            if (mImpl->log == nullptr)
            {
                std::fprintf(stderr, "[predictor] nao consegui abrir TCC_PREDICTOR_LOG=%s -- sem log\n", logPath);
            }
            else
            {
                std::setvbuf(mImpl->log, nullptr, _IOLBF, 0);  // linha a linha: nada se perde se o cliente fechar
                std::fseek(mImpl->log, 0, SEEK_END);  // posicao inicial em modo "a" depende da libc
                if (std::ftell(mImpl->log) == 0)
                {
                    std::fputs("wall_time,event,being_id,name,p_attack,threshold,x,y,velocity_x,velocity_y,"
                               "speed,time_since_last_action,time_since_last_hurt,last_damage_taken,"
                               "prev_action_was_attack\n", mImpl->log);
                }
                std::fprintf(stderr, "[predictor] log de avaliacao: %s\n", logPath);
                logRow("init", nullptr, -1.0F, envThreshold(), nullptr);
            }
        }
    }
    catch (const Ort::Exception &e)
    {
        // nunca deixa a falha de carregar o modelo derrubar o cliente --
        // so fica desligado (getAttackProbability sempre retorna 0).
        mReady = false;
        std::fprintf(stderr, "[predictor] modelo NAO carregado (%s): %s -- predictor desligado\n",
                     modelPath.c_str(), e.what());
    }
}

// So preve ACOES DE JOGADORES -- e o que o modelo foi treinado pra prever.
// NPCs e monstros nao tem esse padrao de comportamento (achado no teste:
// o telegraph disparava ao chegar perto de NPCs). TCC_PREDICTOR_ALL=1
// libera todos os tipos de being, so pra depuracao.
static bool isEligible(const Being *being)
{
    static const bool all = std::getenv("TCC_PREDICTOR_ALL") != nullptr;
    return all || (being != nullptr && being->getType() == ActorType::Player);
}

void BeingActionPredictor::logRow(const char *event, const Being *being, const float p,
                                  const float threshold, const float *features)
{
    if (mImpl->log == nullptr)
        return;
    std::FILE *f = mImpl->log;
    std::fprintf(f, "%.3f,%s,", wallSeconds(), event);
    if (being != nullptr)
    {
        // nome entre aspas (pode ter virgula/espaco); aspas internas dobradas, como no CSV padrao
        std::fprintf(f, "%d,\"", static_cast<int>(being->getId()));
        for (const char ch : being->getName())
        {
            if (ch == '"')
                std::fputc('"', f);
            std::fputc(ch, f);
        }
        std::fputs("\",", f);
    }
    else
    {
        std::fputs(",,", f);
    }
    if (p >= 0.0F)
        std::fprintf(f, "%.5f", static_cast<double>(p));
    std::fputc(',', f);
    if (threshold > 0.0F)
        std::fprintf(f, "%.4f", static_cast<double>(threshold));
    for (int i = 0; i < NUM_FEATURES; ++i)
    {
        std::fputc(',', f);
        if (features != nullptr && !std::isnan(features[i]))
            std::fprintf(f, "%.6g", static_cast<double>(features[i]));
    }
    std::fputc('\n', f);
}

// linha "move"/"attack"/"hurt": so posicao (e o dano, no hurt) -- o resto das
// features nao se aplica a um evento observado
static std::array<float, NUM_FEATURES> observedRow(const Being *being, const float damage)
{
    std::array<float, NUM_FEATURES> r;
    r.fill(NAN);
    r[0] = static_cast<float>(being->getTileX());
    r[1] = static_cast<float>(being->getTileY());
    r[7] = damage;
    return r;
}

BeingActionPredictor::History &BeingActionPredictor::historyFor(const Being *being)
{
    return mHistory[static_cast<int>(being->getId())];
}

void BeingActionPredictor::forget(const Being *being)
{
    mHistory.erase(static_cast<int>(being->getId()));
}

void BeingActionPredictor::onBeingMove(const Being *being)
{
    if (!isEligible(being))
        return;
    History &h = historyFor(being);
    h.lastActionTime = nowSeconds();
    h.prevAttack = false;
    h.hasHistory = true;
    logRow("move", being, -1.0F, -1.0F, observedRow(being, NAN).data());
}

void BeingActionPredictor::onBeingAttack(const Being *being)
{
    if (!isEligible(being))
        return;
    History &h = historyFor(being);
    h.lastActionTime = nowSeconds();
    h.prevAttack = true;
    h.hasHistory = true;
    logRow("attack", being, -1.0F, -1.0F, observedRow(being, NAN).data());
}

void BeingActionPredictor::onBeingHurt(const Being *being, int damage)
{
    if (!isEligible(being))
        return;
    History &h = historyFor(being);
    h.lastHurtTime = nowSeconds();
    h.lastDamage = damage;
    logRow("hurt", being, -1.0F, -1.0F, observedRow(being, static_cast<float>(damage)).data());
}

float BeingActionPredictor::getAttackProbability(const Being *being)
{
    if (!mReady || being == nullptr)
        return 0.0F;

    const int id = static_cast<int>(being->getId());
    auto it = mHistory.find(id);
    if (it == mHistory.end() || !it->second.hasHistory)
        return 0.0F;  // sem historico suficiente ainda (equivalente ao "drop first row" do build_dataset.py)

    History &h = it->second;
    const double t = nowSeconds();

    if (h.lastInferenceTime > 0.0 && (t - h.lastInferenceTime) < INFERENCE_THROTTLE_S)
        return h.cachedProb;

    const int x = being->getTileX();
    const int y = being->getTileY();
    const int hp = being->getHP();
    const int maxHp = being->getMaxHP();

    double velocityX = 0.0;
    double velocityY = 0.0;
    if (h.lastPosTime > 0.0)
    {
        const double dt = std::max(t - h.lastPosTime, MIN_DT_S);
        velocityX = std::clamp((x - h.lastX) / dt, -MAX_SPEED, MAX_SPEED);
        velocityY = std::clamp((y - h.lastY) / dt, -MAX_SPEED, MAX_SPEED);
    }
    const double speed = std::hypot(velocityX, velocityY);

    const double timeSinceLastAction = (h.lastActionTime > 0.0) ? (t - h.lastActionTime) : NO_HISTORY_SENTINEL;
    const double timeSinceLastHurt = (h.lastHurtTime > 0.0) ? (t - h.lastHurtTime) : NO_HISTORY_SENTINEL;

    // SO features que o CLIENTE consegue observar de um jogador remoto (sem
    // hp/maxHp/dead: o servidor nao envia). Ordem EXATA de
    // export_onnx.py::ALL_FEATURES -- nao reordenar sem atualizar os dois lados.
    std::array<float, NUM_FEATURES> features = {
        static_cast<float>(x),
        static_cast<float>(y),
        static_cast<float>(velocityX),
        static_cast<float>(velocityY),
        static_cast<float>(speed),
        static_cast<float>(timeSinceLastAction),
        static_cast<float>(timeSinceLastHurt),
        static_cast<float>(h.lastDamage),
        h.prevAttack ? 1.0F : 0.0F,
    };

    h.lastX = x;
    h.lastY = y;
    h.lastPosTime = t;

    try
    {
        const std::array<int64_t, 2> inputShape = {1, NUM_FEATURES};
        Ort::Value inputTensor = Ort::Value::CreateTensor<float>(
            mImpl->memoryInfo, features.data(), features.size(), inputShape.data(), inputShape.size());

        const char *inputNames[] = {"input"};
        const char *outputNames[] = {"probabilities"};  // ver nota no topo do arquivo sobre o output "label"

        auto outputTensors = mImpl->session->Run(
            Ort::RunOptions{nullptr}, inputNames, &inputTensor, 1, outputNames, 1);

        const float *probs = outputTensors[0].GetTensorData<float>();
        h.cachedProb = probs[0];  // P(attack) -- classes_ = ['attack', 'move'], indice 0 = attack
        static const bool debugOn = std::getenv("TCC_PREDICTOR_DEBUG") != nullptr;
        if (debugOn)
        {
            std::fprintf(stderr,
                "[predictor-debug] id=%d x=%d y=%d hp=%d maxhp=%d vel=(%.1f,%.1f) dt_acao=%.2f dt_dano=%.2f prevAtk=%d p_attack=%.3f\n",
                id, x, y, hp, maxHp, velocityX, velocityY, timeSinceLastAction, timeSinceLastHurt,
                h.prevAttack ? 1 : 0, static_cast<double>(h.cachedProb));
        }
        h.lastInferenceTime = t;
        logRow("infer", being, h.cachedProb, -1.0F, features.data());
        return h.cachedProb;
    }
    catch (const Ort::Exception &e)
    {
        static bool logged = false;
        if (!logged)
        {
            logged = true;
            std::fprintf(stderr, "[predictor] falha na inferencia (so o 1o erro e logado): %s\n", e.what());
        }
        return 0.0F;
    }
}

bool BeingActionPredictor::checkAndConsumeTelegraph(const Being *being, const float threshold)
{
    // "sem previsao disponivel" (modelo desligado / sem historico) NAO e o
    // mesmo que "previsao = 0": getAttackProbability devolve 0 nos dois
    // casos, entao sem essa guarda um limiar <= 0 dispararia mesmo sem
    // modelo. (Bug achado pelo teste com modelo inexistente.)
    if (!mReady || being == nullptr)
        return false;

    auto it = mHistory.find(static_cast<int>(being->getId()));
    if (it == mHistory.end() || !it->second.hasHistory)
        return false;
    History &h = it->second;

    const float p = getAttackProbability(being);

    const float limit = envThreshold() > 0.0F ? envThreshold() : threshold;

    if (p >= limit)
    {
        if (h.telegraphActive)
            return false;  // ja disparou nesta "subida" -- espera cair abaixo do limiar antes de rearmar
        h.telegraphActive = true;
        std::fprintf(stderr, "[predictor] telegraph id=%d p=%.2f\n",
                     static_cast<int>(being->getId()), static_cast<double>(p));
        logRow("telegraph", being, p, limit, nullptr);
        return true;
    }

    h.telegraphActive = false;  // abaixo do limiar: rearma pro proximo cruzamento
    return false;
}
