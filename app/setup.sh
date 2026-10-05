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

    mkdir -p "$MANAVERSE_DIR/src/ml"
    cp "$SCRIPT_DIR/manaverse/beingactionpredictor.h" "$SCRIPT_DIR/manaverse/beingactionpredictor.cpp" "$MANAVERSE_DIR/src/ml/"
    log "beingactionpredictor.h/.cpp copiados pra manaverse-mirror/src/ml/"

    if grep -q "BeingActionPredictor" "$MANAVERSE_DIR/src/being/being.cpp" 2>/dev/null; then
        log "being.cpp ja parece ter os hooks -- pulando git apply."
    else
        ( cd "$MANAVERSE_DIR" && git apply --check "$SCRIPT_DIR/manaverse/being.cpp.patch" && git apply "$SCRIPT_DIR/manaverse/being.cpp.patch" )
        log "being.cpp.patch aplicado."
    fi

    if grep -q "ml/beingactionpredictor" "$MANAVERSE_DIR/src/Makefile.am" 2>/dev/null; then
        log "Makefile.am ja parece registrar os arquivos novos -- pulando git apply."
    else
        ( cd "$MANAVERSE_DIR" && git apply --check "$SCRIPT_DIR/manaverse/Makefile.am.patch" && git apply "$SCRIPT_DIR/manaverse/Makefile.am.patch" )
        log "Makefile.am.patch aplicado."
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
