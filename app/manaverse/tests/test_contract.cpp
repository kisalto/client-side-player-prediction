// Teste A: contrato do model.onnx (input "input" [N,13] float, output
// "probabilities" [N,2], indice 0 = attack) vs. sklearn, linha a linha.
#include <onnxruntime_cxx_api.h>
#include <array>
#include <cmath>
#include <fstream>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>
int main() {
    Ort::Env env(ORT_LOGGING_LEVEL_WARNING, "t");
    Ort::SessionOptions so;
    Ort::Session s(env, "model.onnx", so);
    Ort::AllocatorWithDefaultOptions al;
    std::cout << "input name:  " << s.GetInputNameAllocated(0, al).get() << "\n";
    for (size_t i = 0; i < s.GetOutputCount(); ++i)
        std::cout << "output " << i << ":    " << s.GetOutputNameAllocated(i, al).get() << "\n";
    std::ifstream f("rows.csv"); std::string line; std::getline(f, line);
    auto mem = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);
    double maxdiff = 0; int n = 0;
    while (std::getline(f, line)) {
        std::stringstream ss(line); std::string c; std::vector<double> v;
        while (std::getline(ss, c, ',')) v.push_back(std::stod(c));
        std::array<float,13> feat; for (int i=0;i<13;++i) feat[i]=(float)v[i];
        double expected = v[13];
        std::array<int64_t,2> shape{1,13};
        auto t = Ort::Value::CreateTensor<float>(mem, feat.data(), feat.size(), shape.data(), 2);
        const char* in[]={"input"}; const char* out[]={"probabilities"};
        auto r = s.Run(Ort::RunOptions{nullptr}, in, &t, 1, out, 1);
        double got = r[0].GetTensorData<float>()[0];
        maxdiff = std::max(maxdiff, std::fabs(got-expected)); ++n;
    }
    std::cout << "linhas: " << n << " | diff maxima vs sklearn (P(attack)): " << maxdiff << "\n";
    return maxdiff < 1e-4 ? 0 : 1;
}
