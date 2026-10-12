#!/usr/bin/env bash
# setup.sh
# ---------------------------------------------------------------------------
# Copia os arquivos de integracao deste repo (app/tmwa/, app/manaverse/) para
# dentro dos clones de TMWA/ e manaverse-mirror/ (na raiz do repo, irmaos de
# app/), e aplica os patches correspondentes.
#
# Pre-requisito: TMWA/ e manaverse-mirror/ ja devem ser clones git validos
# (ver README.md na raiz do repo pros comandos de clone). Este script NAO
# clona nada sozinho -- so copia+aplica.
#
# Uso (a partir da raiz do repo):
#   bash app/setup.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
TMWA_DIR="$REPO_ROOT/TMWA"
MANAVERSE_DIR="$REPO_ROOT/manaverse-mirror"

log() { echo "[setup] $*"; }
err() { echo "[setup] ERRO: $*" >&2; }

check_is_repo() {
    local dir="$1"
    local name="$2"
    if [ ! -d "$dir/.git" ]; then
        err "$name ($dir) nao parece um clone git valido -- rode o clone primeiro (ver README.md)."
        return 1
    fi
}

# ---------------------------------------------------------------------------
# TMWA
# ---------------------------------------------------------------------------
setup_tmwa() {
    log "== TMWA =="
    check_is_repo "$TMWA_DIR" "TMWA" || return 1

    mkdir -p "$TMWA_DIR/src/map"
    cp "$SCRIPT_DIR/tmwa/telemetry.hpp" "$SCRIPT_DIR/tmwa/telemetry.cpp" "$TMWA_DIR/src/map/"
    log "telemetry.hpp/.cpp copiados pra TMWA/src/map/"

    if grep -q "telemetry_log_move_cmd" "$TMWA_DIR/src/map/pc.cpp" 2>/dev/null; then
        log "pc.cpp ja parece ter os hooks de telemetria -- pulando git apply (evita erro de patch duplicado)."
    else
        ( cd "$TMWA_DIR" && git apply --check "$SCRIPT_DIR/tmwa/pc.cpp.patch" && git apply "$SCRIPT_DIR/tmwa/pc.cpp.patch" )
        log "pc.cpp.patch aplicado."
    fi
}

# ---------------------------------------------------------------------------
# manaverse-mirror
# ---------------------------------------------------------------------------
setup_manaverse() {
    log "== manaverse-mirror =="
    check_is_repo "$MANAVERSE_DIR" "manaverse-mirror" || return 1

    # .h/.cpp sempre sobrescritos (fonte da verdade = app/manaverse/)
    mkdir -p "$MANAVERSE_DIR/src/ml"
    cp "$SCRIPT_DIR/manaverse/beingactionpredictor.h" "$SCRIPT_DIR/manaverse/beingactionpredictor.cpp" "$MANAVERSE_DIR/src/ml/"
    log "beingactionpredictor.h/.cpp copiados pra manaverse-mirror/src/ml/"

    # Cada patch tem um marcador proprio -- assim funciona tanto num ManaVerse
    # "limpo" (aplica os 4, em ordem) quanto no seu manaverse-mirror, que ja
    # tem os hooks da v1 commitados (so aplica o que falta).
    apply_if_missing "$MANAVERSE_DIR" "src/being/being.cpp"          "onBeingMove"               "being.cpp.patch"          "hooks de movimento/dano/ataque"
    apply_if_missing "$MANAVERSE_DIR" "src/Makefile.am"              "ml/beingactionpredictor"   "Makefile.am.patch"        "registro dos arquivos novos"
    apply_if_missing "$MANAVERSE_DIR" "src/being/being.cpp"          "checkAndConsumeTelegraph"  "being-telegraph.patch"    "telegraph em Being::logic()"
    apply_if_missing "$MANAVERSE_DIR" "src/progs/manaverse/client.cpp" "BeingActionPredictor"    "client.cpp.patch"         "init() do modelo no boot"
}

# apply_if_missing <repo> <arquivo-relativo> <marcador> <patch> <descricao>
apply_if_missing() {
    local repo="$1" file="$2" marker="$3" patch="$4" desc="$5"
    if grep -q "$marker" "$repo/$file" 2>/dev/null; then
        log "$desc: ja aplicado em $file -- pulando $patch."
    else
        ( cd "$repo" && git apply --check "$SCRIPT_DIR/manaverse/$patch" && git apply "$SCRIPT_DIR/manaverse/$patch" )
        log "$desc: $patch aplicado."
    fi
}

main() {
    local failed=0
    setup_tmwa || failed=1
    setup_manaverse || failed=1

    if [ "$failed" -eq 0 ]; then
        log "Setup concluido. Proximo passo: compilar -- ver app/tmwa/README.md e app/manaverse/FASE8_INTEGRACAO.md."
    else
        err "Setup incompleto -- corrija os erros acima e rode de novo (e seguro rodar mais de uma vez)."
        exit 1
    fi
}

main
