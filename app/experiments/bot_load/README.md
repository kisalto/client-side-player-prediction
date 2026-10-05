# experiments/bot_load/

**Status: a fazer (Fase 10)**

Pergunta a responder: o sistema continua funcionando com uma quantidade
grande de jogadores simultâneos (50-100 bots)?

Precisa de um cliente "bot" simples que fale o protocolo do `tmwa`
(conecta, anda, ataca) -- não necessariamente o ManaVerse completo, pode
ser um script mínimo só pra gerar carga realista no servidor e na
telemetria.

Métricas: throughput da bridge/TelemetryService sob carga, taxa de perda
de pacotes UDP local, latência de gravação no Postgres, uso de CPU/memória
do `tmwa-map` com a telemetria ligada vs. desligada (pra confirmar que o
overhead continua desprezível em escala).
