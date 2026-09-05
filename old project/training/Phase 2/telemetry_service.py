"""
telemetry_service.py
---------------------------------------------------------------------------
Fase 2: servidor gRPC que recebe telemetria do jogo (via telemetry.proto) e
grava no PostgreSQL.

Implementa os dois RPCs definidos em TelemetryService:
- StreamTelemetry: stream bidirecional. Primeira mensagem = SessionInfo
  (grava/atualiza a sessao). Mensagens seguintes = TelemetryBatch (grava
  frames + eventos em lote, responde um BatchAck por batch).
- SendBatch: RPC unario, manda SessionInfo + TelemetryBatch de uma vez so
  (usado por scripts/CLIs que nao querem manter um stream aberto).

Uso:
    python telemetry_service.py --port 50051 \
        --dsn "dbname=telemetry user=postgres password=postgres host=localhost"

Ou via variavel de ambiente TELEMETRY_DSN (mais pratico com Docker).
"""
import argparse
import json
import logging
import os
from concurrent import futures

import grpc
import psycopg2
import psycopg2.extras
import psycopg2.pool

import telemetry_pb2 as pb
import telemetry_pb2_grpc as pb_grpc

logging.basicConfig(level=logging.INFO, format='%(asctime)s [%(levelname)s] %(message)s')
log = logging.getLogger('telemetry_service')

FRAME_COLUMNS = [
    'session_id', 'batch_sequence', 't',
    'px', 'py', 'pvx', 'pvy',
    'is_grounded', 'is_climbing', 'is_facing_right',
    'is_dashing', 'can_dash',
    'is_attacking', 'is_blocking', 'is_blocking_up', 'is_parrying',
    'current_health', 'max_health',
    'is_invincible', 'is_alive',
    'boss_active', 'boss_x', 'boss_y', 'boss_health', 'boss_max_health',
    'enemies', 'bullets',
]

EVENT_COLUMNS = ['session_id', 'batch_sequence', 't', 'type', 'extra']


def _entity_to_dict(e):
    return {
        'x': e.x, 'y': e.y, 'health': e.health, 'max_health': e.max_health,
        'is_attacking': e.is_attacking, 'has_detected_player': e.has_detected_player,
        'distance_to_player': e.distance_to_player, 'is_being_knocked_back': e.is_being_knocked_back,
    }


def _bullet_to_dict(b):
    return {'tag': b.tag, 'x': b.x, 'y': b.y, 'vx': b.vx, 'vy': b.vy, 'w': b.w, 'h': b.h}


def _frame_row(session_id, batch_sequence, f):
    return (
        session_id, batch_sequence, f.t,
        f.px, f.py, f.pvx, f.pvy,
        f.is_grounded, f.is_climbing, f.is_facing_right,
        f.is_dashing, f.can_dash,
        f.is_attacking, f.is_blocking, f.is_blocking_up, f.is_parrying,
        f.current_health, f.max_health,
        f.is_invincible, f.is_alive,
        f.boss_active, f.boss_x, f.boss_y, f.boss_health, f.boss_max_health,
        json.dumps([_entity_to_dict(e) for e in f.enemies]),
        json.dumps([_bullet_to_dict(b) for b in f.bullets]),
    )


def _event_row(session_id, batch_sequence, e):
    return (session_id, batch_sequence, e.t, e.type, e.extra)


class TelemetryServiceServicer(pb_grpc.TelemetryServiceServicer):
    def __init__(self, dsn):
        self.pool = psycopg2.pool.ThreadedConnectionPool(1, 16, dsn=dsn)

    def _conn(self):
        return self.pool.getconn()

    def _release(self, conn):
        self.pool.putconn(conn)

    def _upsert_session(self, conn, session: 'pb.SessionInfo'):
        with conn.cursor() as cur:
            cur.execute(
                """
                INSERT INTO sessions (session_id, player_id, game_version, recorded_at,
                                       snapshot_interval_seconds, client_platform)
                VALUES (%s, %s, %s, NULLIF(%s, '')::timestamptz, %s, %s)
                ON CONFLICT (session_id) DO UPDATE SET
                    player_id = EXCLUDED.player_id,
                    game_version = EXCLUDED.game_version,
                    client_platform = EXCLUDED.client_platform
                """,
                (session.session_id, session.player_id, session.game_version,
                 session.recorded_at, session.snapshot_interval_seconds, session.client_platform),
            )
        conn.commit()

    def _insert_batch(self, conn, session_id, batch: 'pb.TelemetryBatch'):
        with conn.cursor() as cur:
            if batch.frames:
                rows = [_frame_row(session_id, batch.batch_sequence, f) for f in batch.frames]
                psycopg2.extras.execute_values(
                    cur, f"INSERT INTO frames ({', '.join(FRAME_COLUMNS)}) VALUES %s", rows,
                )
            if batch.events:
                rows = [_event_row(session_id, batch.batch_sequence, e) for e in batch.events]
                psycopg2.extras.execute_values(
                    cur, f"INSERT INTO events ({', '.join(EVENT_COLUMNS)}) VALUES %s", rows,
                )
        conn.commit()

    # -- RPC 1: stream bidirecional -------------------------------------------
    def StreamTelemetry(self, request_iterator, context):
        conn = self._conn()
        session_id = None
        try:
            for msg in request_iterator:
                kind = msg.WhichOneof('payload')
                if kind == 'session_start':
                    session_id = msg.session_start.session_id
                    self._upsert_session(conn, msg.session_start)
                    log.info('sessao iniciada: %s', session_id)
                    continue

                if kind == 'batch':
                    if session_id is None:
                        # cliente mandou um batch sem mandar session_start antes --
                        # aceitamos mesmo assim com um session_id provisorio, mas
                        # avisamos, porque isso normalmente indica bug no cliente.
                        session_id = 'unknown-session'
                        self._upsert_session(conn, pb.SessionInfo(session_id=session_id))
                        log.warning('batch recebido sem session_start previo -- usando "unknown-session"')

                    batch = msg.batch
                    try:
                        self._insert_batch(conn, session_id, batch)
                        yield pb.BatchAck(
                            batch_sequence=batch.batch_sequence,
                            frames_received=len(batch.frames),
                            events_received=len(batch.events),
                            ok=True,
                        )
                    except Exception as exc:  # nao derruba o stream por causa de 1 batch ruim
                        conn.rollback()
                        log.exception('falha ao gravar batch %s da sessao %s', batch.batch_sequence, session_id)
                        yield pb.BatchAck(
                            batch_sequence=batch.batch_sequence, ok=False, message=str(exc),
                        )
        finally:
            self._release(conn)

    # -- RPC 2: unario ----------------------------------------------------------
    def SendBatch(self, request: 'pb.SendBatchRequest', context):
        conn = self._conn()
        try:
            self._upsert_session(conn, request.session)
            self._insert_batch(conn, request.session.session_id, request.batch)
            return pb.BatchAck(
                batch_sequence=request.batch.batch_sequence,
                frames_received=len(request.batch.frames),
                events_received=len(request.batch.events),
                ok=True,
            )
        except Exception as exc:
            conn.rollback()
            log.exception('falha ao gravar SendBatch da sessao %s', request.session.session_id)
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details(str(exc))
            return pb.BatchAck(ok=False, message=str(exc))
        finally:
            self._release(conn)


def serve(port, dsn, max_workers=8):
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=max_workers))
    pb_grpc.add_TelemetryServiceServicer_to_server(TelemetryServiceServicer(dsn), server)
    server.add_insecure_port(f'[::]:{port}')
    server.start()
    log.info('TelemetryService rodando na porta %s', port)
    server.wait_for_termination()


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--port', type=int, default=50051)
    ap.add_argument('--dsn', default=os.environ.get(
        'TELEMETRY_DSN', 'dbname=telemetry user=postgres password=postgres host=localhost'))
    ap.add_argument('--max-workers', type=int, default=8)
    args = ap.parse_args()
    serve(args.port, args.dsn, args.max_workers)


if __name__ == '__main__':
    main()
