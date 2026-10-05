# experiments/latency_injection/

**Status: a fazer (Fase 9)**

Testar o pipeline sob latência/jitter/perda artificiais: 20, 50, 100, 150,
200, 250, 300ms, com diferentes níveis de jitter e perda de pacotes.

Candidatos pra injeção de latência (Linux, na máquina da bridge ou do
`tmwa-map`):
- `tc netem` (traffic control do kernel) -- injeta delay/jitter/loss na
  interface de rede, sem precisar mexer em código
- Alternativa: um proxy TCP/UDP simples em Python que atrasa
  artificialmente antes de repassar

Métricas a coletar: precisão da previsão sob cada condição, taxa de
correção (quanto o servidor precisou "corrigir" a previsão do cliente), e
responsividade percebida.
