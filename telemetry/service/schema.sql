-- schema.sql
-- ---------------------------------------------------------------------------
-- Schema simplificado pro tmwa (ver telemetry.proto pro historico da
-- mudanca). Uma unica tabela de eventos: "snapshot" (periodico) e
-- "player_hurt"/"player_death" (pontuais) convivem na mesma tabela,
-- distinguidos por event_type -- nao ha mais necessidade de separar
-- frames/events como no schema antigo, ja que nao existe mais estado
-- aninhado (enemies/bullets) pra justificar colunas JSONB.

CREATE TABLE IF NOT EXISTS sessions (
    session_id      TEXT PRIMARY KEY,
    game_version    TEXT,
    recorded_at     TIMESTAMPTZ,
    bridge_host     TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS player_events (
    id                  BIGSERIAL PRIMARY KEY,
    session_id          TEXT NOT NULL REFERENCES sessions(session_id) ON DELETE CASCADE,
    batch_sequence      INT,
    bridge_recv_time    DOUBLE PRECISION NOT NULL,  -- epoch seconds, atribuido pela bridge

    event_type          TEXT NOT NULL,   -- 'snapshot' | 'player_hurt' | 'player_death' | 'player_move_cmd' | 'player_attack'
    char_id             BIGINT NOT NULL,
    x                   INT,             -- posicao atual (todos os event_type)
    y                   INT,
    hp                  INT,
    max_hp              INT,
    dead                BOOLEAN,
    extra               INT,             -- dano (hurt/death) ou 0 (demais)

    -- especificos de acao (NULL/sentinela quando nao se aplica ao event_type)
    dest_x              INT,             -- so em player_move_cmd
    dest_y              INT,
    target_id           BIGINT,          -- so em player_attack
    continuous          BOOLEAN,

    inserted_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_player_events_session_time ON player_events (session_id, bridge_recv_time);
CREATE INDEX IF NOT EXISTS idx_player_events_char ON player_events (char_id, bridge_recv_time);
CREATE INDEX IF NOT EXISTS idx_player_events_type ON player_events (event_type);
