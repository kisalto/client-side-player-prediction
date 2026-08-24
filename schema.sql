-- schema.sql
-- ---------------------------------------------------------------------------
-- Schema do banco de telemetria. enemies/bullets ficam como JSONB porque sao
-- listas de tamanho variavel por frame (numero de inimigos/projeteis na tela
-- muda o tempo todo) -- normalizar isso em tabelas separadas so complicaria
-- consultas sem trazer beneficio real aqui, ja que a Fase 4 (build_dataset.py)
-- vai ler essas colunas inteiras pra fazer feature engineering em Python de
-- qualquer forma (mesma logica que ja usamos no toho-like-js com o campo
-- "bullets" do JSON).

CREATE TABLE IF NOT EXISTS sessions (
    session_id                 TEXT PRIMARY KEY,
    player_id                  TEXT,
    game_version                TEXT,
    recorded_at                 TIMESTAMPTZ,
    snapshot_interval_seconds  REAL,
    client_platform             TEXT,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS frames (
    id                  BIGSERIAL PRIMARY KEY,
    session_id          TEXT NOT NULL REFERENCES sessions(session_id) ON DELETE CASCADE,
    batch_sequence      INT,
    t                   REAL NOT NULL,

    -- jogador
    px REAL, py REAL, pvx REAL, pvy REAL,
    is_grounded BOOLEAN, is_climbing BOOLEAN, is_facing_right BOOLEAN,
    is_dashing BOOLEAN, can_dash BOOLEAN,
    is_attacking BOOLEAN, is_blocking BOOLEAN, is_blocking_up BOOLEAN, is_parrying BOOLEAN,
    current_health INT, max_health INT,
    is_invincible BOOLEAN, is_alive BOOLEAN,

    -- boss
    boss_active BOOLEAN, boss_x REAL, boss_y REAL,
    boss_health INT, boss_max_health INT,

    -- listas de tamanho variavel (ver comentario no topo do arquivo)
    enemies JSONB NOT NULL DEFAULT '[]'::jsonb,
    bullets JSONB NOT NULL DEFAULT '[]'::jsonb,

    inserted_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_frames_session_t ON frames (session_id, t);

CREATE TABLE IF NOT EXISTS events (
    id              BIGSERIAL PRIMARY KEY,
    session_id      TEXT NOT NULL REFERENCES sessions(session_id) ON DELETE CASCADE,
    batch_sequence  INT,
    t               REAL NOT NULL,
    type            TEXT NOT NULL,
    extra           TEXT,
    inserted_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_events_session_t ON events (session_id, t);
CREATE INDEX IF NOT EXISTS idx_events_type ON events (type);
