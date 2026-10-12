#!/usr/bin/env bash
# Testes de runtime do BeingActionPredictor (sem precisar compilar/rodar o cliente inteiro).
#  A) contrato do model.onnx em C++ vs sklearn;  B) a classe REAL (beingactionpredictor.cpp,
#  sem modificar) contra um Being stub: init sem modelo, throttle, telegraph, forget, e
#  valores conferidos com o sklearn.
# Requisitos: g++ (C++17), python3 com scikit-learn/skl2onnx/onnxruntime/pandas/joblib,
#             ONNX Runtime C++ extraido (ver ../FASE8_INTEGRACAO.md).
# Uso: ONNXRUNTIME_DIR=/caminho/onnxruntime-linux-x64-1.20.1 bash run_tests.sh
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ORT="${ONNXRUNTIME_DIR:?defina ONNXRUNTIME_DIR apontando pra pasta do ONNX Runtime (com include/ e lib/)}"
BUILD="$(mktemp -d)"; trap 'rm -rf "$BUILD"' EXIT
cd "$BUILD"
CXX="g++ -std=gnu++17 -Wall -I$ORT/include"
LD="-L$ORT/lib -Wl,-rpath,$ORT/lib -lonnxruntime"

echo "== gerando model.onnx de teste (mesma estrutura do pipeline real) =="
python3 "$HERE/make_model.py"

echo; echo "== A) contrato do ONNX (C++ vs sklearn) =="
$CXX "$HERE/test_contract.cpp" $LD -o test_contract && ./test_contract

echo; echo "== B) BeingActionPredictor real =="
$CXX -I"$HERE/stub" -I"$HERE/.." "$HERE/test_predictor.cpp" "$HERE/../beingactionpredictor.cpp" $LD -o test_predictor
./test_predictor | tee out.txt

echo; echo "== B2) valores da classe C++ vs sklearn =="
python3 "$HERE/check_values.py" out.txt
echo; echo "TODOS OS TESTES PASSARAM"
