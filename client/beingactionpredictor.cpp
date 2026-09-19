// beingactionpredictor.cpp
// ---------------------------------------------------------------------------
// Ver beingactionpredictor.h para o design geral e o aviso honesto sobre
// nao ter sido compilado contra o build de verdade do ManaVerse.
//
// FORMATO DE ENTRADA DO model.onnx (confirmado inspecionando o arquivo
// real gerado por export_onnx.py -- nao adivinhado):
//   input:  1 tensor float32, shape [N, 13], nesta ordem exata:
//     x, y, hp, max_hp, hp_ratio, velocity_x, velocity_y, speed,
//     time_since_last_action, time_since_last_hurt, last_damage_taken,
//     dead, prev_action_was_attack
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
#include <vector>

#include <onnxruntime_cxx_api.h>

#include "being/being.h"

namespace
{
    constexpr int NUM_FEATURES = 13;
    constexpr double MAX_SPEED = 500.0;  // mesmo teto de seguranca do build_dataset.py (Fase 4)
    constexpr double MIN_DT_S = 0.05;    // mesmo piso de dt do build_dataset.py
    constexpr double NO_HISTORY_SENTINEL = -1.0;
}  // namespace

struct BeingActionPredictor::Impl
{
    Ort::Env env{ORT_LOGGING_LEVEL_WARNING, "BeingActionPredictor"};
    std::unique_ptr<Ort::Session> session;
    Ort::MemoryInfo memoryInfo = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);
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
    }
    catch (const Ort::Exception &e)
    {
        // nunca deixa a falha de carregar o modelo derrubar o cliente --
        // so fica desligado (getAttackProbability sempre retorna 0).
        mReady = false;
    }
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
    History &h = historyFor(being);
    h.lastActionTime = nowSeconds();
    h.prevAttack = false;
    h.hasHistory = true;
}

void BeingActionPredictor::onBeingAttack(const Being *being)
{
    History &h = historyFor(being);
    h.lastActionTime = nowSeconds();
    h.prevAttack = true;
    h.hasHistory = true;
}

void BeingActionPredictor::onBeingHurt(const Being *being, int damage)
{
    History &h = historyFor(being);
    h.lastHurtTime = nowSeconds();
    h.lastDamage = damage;
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

    // ordem EXATA de export_onnx.py::ALL_FEATURES -- nao reordenar sem
    // atualizar os dois lados.
    std::array<float, NUM_FEATURES> features = {
        static_cast<float>(x),
        static_cast<float>(y),
        static_cast<float>(hp),
        static_cast<float>(maxHp),
        maxHp > 0 ? static_cast<float>(hp) / static_cast<float>(maxHp) : 0.0F,
        static_cast<float>(velocityX),
        static_cast<float>(velocityY),
        static_cast<float>(speed),
        static_cast<float>(timeSinceLastAction),
        static_cast<float>(timeSinceLastHurt),
        static_cast<float>(h.lastDamage),
        being->isAlive() ? 0.0F : 1.0F,
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
        return probs[0];  // P(attack) -- classes_ = ['attack', 'move'], indice 0 = attack
    }
    catch (const Ort::Exception &)
    {
        return 0.0F;
    }
}
