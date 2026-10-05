// telemetry.hpp
// ---------------------------------------------------------------------------
// Fase 1 (tmwa/servidor): telemetria leve via UDP, nao-bloqueante.
//
// NAO usa a maquinaria de rede/socket do proprio tmwa (Session/SessionData) --
// de proposito. E um socket UDP comum, plain POSIX, aberto uma vez e
// reaproveitado. UDP local e fire-and-forget: se a bridge (processo Python
// que escuta essa porta e reenvia por gRPC pro TelemetryService) estiver
// fora do ar, send() simplesmente falha silenciosamente e o jogo continua
// normal -- ZERO risco de travar o map-server por causa de telemetria.
//
// v2: alem de estado (posicao/vida/morte), agora tambem loga ACOES do
// jogador -- comando de movimento (pc_walktoxy) e ataque (pc_attack) --
// necessario pro dataset de "prever a proxima acao" (ver Fase 4). Ver
// telemetry/proto/telemetry.proto no repo tmwa-ml-pipeline pro schema
// completo (campos dest_x/dest_y/target_id/continuous).

#pragma once

#include "fwd.hpp"

namespace tmwa
{
namespace map
{

// chamar uma vez, em do_init_pc() (ou onde preferir no boot do map-server)
void telemetry_init();

// eventos de ESTADO pontuais (morte, dano). extra_int = quantidade de dano.
void telemetry_log_event(dumb_ptr<map_session_data> sd, const char *event_type, int extra_int);

// evento de AÇÃO: comando de movimento (destino clicado, nao a posicao
// atual -- essa ja vai junto em x/y). Chamar em pc_walktoxy, com o (x,y)
// recebido como PARAMETRO da funcao (o destino), nao sd->bl_x/bl_y (posicao
// atual, que fica em outro par de campos automaticamente).
void telemetry_log_move_cmd(dumb_ptr<map_session_data> sd, int dest_x, int dest_y);

// evento de AÇÃO: ataque iniciado contra target_id (0 se o alvo nao existir
// mais no momento do log -- ainda assim vale registrar a intencao).
// continuous = true quando e ataque "segurando" (DamageType != NORMAL).
void telemetry_log_attack(dumb_ptr<map_session_data> sd, uint32_t target_id, bool continuous);

// registra o timer periodico de snapshot (chamar uma vez, junto do
// telemetry_init(), em do_init_pc()). Nao precisa mexer em mais nada --
// o timer se re-agenda sozinho, igual ao pc_natural_heal.
void telemetry_start_snapshot_timer();

} // namespace map
} // namespace tmwa
