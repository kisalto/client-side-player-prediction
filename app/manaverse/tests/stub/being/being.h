#pragma once
// Stub minimo do Being so pra compilar o beingactionpredictor.cpp REAL
// (sem modificar) fora do cliente inteiro -- ver teste em app/manaverse/tests.
enum class BeingId : int {};
class Being {
public:
    int id = 0, x = 0, y = 0, hp = 100, maxHp = 100;
    bool alive = true;
    BeingId getId() const { return static_cast<BeingId>(id); }
    int getTileX() const { return x; }
    int getTileY() const { return y; }
    int getHP() const { return hp; }
    int getMaxHP() const { return maxHp; }
    bool isAlive() const { return alive; }
};
