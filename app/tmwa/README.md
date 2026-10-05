# app/tmwa/

Arquivos de instrumentação de telemetria pro servidor `tmwa`
(github.com/themanaworld/tmwa), aplicados via fork pessoal em
github.com/kisalto/tmwa.

- `telemetry.hpp` / `telemetry.cpp` -- vão em `src/map/` dentro do clone do tmwa
- `pc.cpp.patch` -- **consolidado**: contém os 5 hooks completos (init,
  dano, morte, comando de movimento, ataque), gerado a partir do
  `pc.cpp` limpo do upstream `themanaworld/tmwa`. Validado com
  `git apply --check` contra um clone novo.

O script `app/setup.sh` (raiz do repo) já faz a cópia e aplica o patch
automaticamente -- ver README raiz.

## Achado da inspeção (29/09, antes desta reorganização)

O fork pessoal (`kisalto/tmwa`) tinha os hooks de **dano/morte/init**
aplicados, mas **não** os de **comando de movimento** (`pc_walktoxy`) e
**ataque** (`pc_attack`) -- mesmo o `telemetry.hpp`/`.cpp` de lá já tendo
as funções `telemetry_log_move_cmd`/`telemetry_log_attack` prontas desde
a Fase 4/6. Ou seja: o patch v2 nunca tinha sido aplicado de fato no
`pc.cpp`. Sem esses dois hooks, nenhum evento `player_move_cmd`/
`player_attack` seria gerado por telemetria real -- o que inviabilizaria
o dataset de ação (Fases 4-6) com dados de jogo de verdade.

**Isso já está corrigido** no `pc.cpp.patch` consolidado deste pacote
(contém os 5 hooks). Se você for continuar usando diretamente o seu fork
pessoal (que já tem dano/morte/init), basta aplicar só os 2 hooks que
faltavam -- peça o patch incremental se preferir esse caminho em vez de
recomeçar de um clone limpo do upstream.

## Outras mudanças encontradas no fork pessoal (não relacionadas, não mexidas)

Comparando `kisalto/tmwa` com o upstream, há diferenças que não têm
relação com a telemetria e que eu não alterei nem revertí:

- `pc.cpp`: um divisor numa conta de tempo de ataque mudou de `/2` para
  `/4` (parece um ajuste de jogabilidade seu).
- Duas chamadas `pc_setstand(sd)` antes de atacar foram removidas (também
  parece intencional, relacionado a permitir atacar sentado).
- Uma checagem de limite (`skill_num < MAX_SKILL`) foi removida num ponto
  do código de skills -- **vale uma olhada**, remover uma checagem de
  limite de array costuma ser arriscado (acesso fora dos limites), mas
  pode ter sido proposital; não mexi nisso, só avisando que notei.
- Uma conta de bitmask de quest também mudou.

Nenhuma dessas é do pipeline de ML -- são só registradas aqui pra você
saber que existem, caso não lembre de tê-las feito.
