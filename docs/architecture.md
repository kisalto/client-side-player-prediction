# Arquitetura de telemetria

```
tmwa-map (C++)  --UDP, fire-and-forget-->  Bridge local  --gRPC (StreamTelemetry)-->  TelemetryService  -->  PostgreSQL
                                                                                            |
                                                                                            v
                                                                                    Pipeline Python (dataset + treino)
                                                                                            |
                                                                                            v
                                                                                        model.onnx
```

## Por que não gRPC direto no `tmwa-map`

`tmwa-map` roda um loop de eventos single-threaded clássico (padrão eAthena:
`add_timer_func_list`, sem async I/O). Um cliente gRPC C++ quer conexão
HTTP/2 persistente com retry/backoff -- colar isso no loop principal
arrisca travar o servidor inteiro (todos os jogadores online) se o
`TelemetryService` cair ou atrasar. Isso seria uma regressão grave num
servidor multiplayer real.

## Decisão: UDP local + bridge

- **No `tmwa-map`**: só `sendto()` de UDP nos pontos que já tinham
  `MAP_LOG_PC` (dano, morte). UDP é fire-and-forget -- não bloqueia, não
  espera resposta, perder um pacote ocasional é aceitável pra telemetria
  (não seria aceitável pro protocolo de gameplay, que continua TCP).
- **Bridge local** (`telemetry/bridge/`, a fazer): processo separado na
  mesma máquina, escuta o UDP, empacota em `TelemetryBatch` e reenvia pro
  `TelemetryService` via `StreamTelemetry` (gRPC).

Isso isola 100% da complexidade de gRPC/retry/HTTP2 pra fora do processo do
jogo, e prepara terreno pra Fase 9/11: dá pra rodar `tmwa-map` numa região e
o `TelemetryService`/Postgres em outra, com a bridge local como ponto
natural de medição/injeção de latência.

## Decisão: server-side é suficiente até a Fase 8

Como o `tmwa-map` já vê tanto o input bruto (movimento/ataque que o jogador
manda) quanto o resultado (estado aplicado), dá pra montar
`X = estado_t, Y = ação_t+1` só com dados do servidor -- suficiente pras
Fases 3 a 7 inteiras. O cliente (ManaVerse) só entra na Fase 8 (previsão
local pra esconder latência), quando `being.cpp`/`localplayer.cpp`/
`net/tmwa/beinghandler.cpp` precisam comparar "o que eu previ" com "o que o
servidor confirmou".

## Pontos de hook confirmados no tmwa (`src/map/`)

- `pc.cpp::pc_damage()` -- dano e morte do jogador (já instrumentado)
- `map.hpp::map_get_first_session()` / `map_get_next_session()` -- iteração
  de todos os jogadores online (usado no snapshot periódico)
- Padrão `Timer(tick, callback, interval).detach()` -- mesmo usado por
  `pc_natural_heal` -- para o snapshot periódico (500ms)
