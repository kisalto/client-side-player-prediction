"""
telemetry_service.py
---------------------------------------------------------------------------
Servidor gRPC que recebe telemetria da bridge (telemetry/bridge/) e grava no
PostgreSQL. Ver telemetry.proto pro contrato e docs/architecture.md pro
desenho geral (tmwa-map --UDP--> bridge --gRPC--> aqui --> Postgres).

Uso:
    python telemetry_service.py --port 50051 \
        --dsn "dbname=telemetry user=postgres password=postgres host=localhost"

Ou via variavel de ambiente TELEMETRY_DSN (mais pratico com Docker).
"""
import argparse
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

EVENT_COLUMNS = [
    'session_id', 'batch_sequence', 'bridge_recv_time',
    'event_type', 'char_id', 'x', 'y', 'hp', 'max_hp', 'dead', 'extra',
    'dest_x', 'dest_y', 'target_id', 'continuous',
]


def _event_row(session_id, batch_sequence, e: 'pb.PlayerEvent'):
    return (
        session_id, batch_sequence, e.bridge_recv_time,
        e.event_type, e.char_id, e.x, e.y, e.hp, e.max_hp, e.dead, e.extra,
        e.dest_x, e.dest_y, e.target_id, e.continuous,
    )


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
                INSERT INTO sessions (session_id, game_version, recorded_at, bridge_host)
                VALUES (%s, %s, NULLIF(%s, '')::timestamptz, %s)
                ON CONFLICT (session_id) DO UPDATE SET
                    game_version = EXCLUDED.game_version,
                    bridge_host = EXCLUDED.bridge_host
                """,
                (session.session_id, session.game_version, session.recorded_at, session.bridge_host),
            )
        conn.commit()

    def _insert_batch(self, conn, session_id, batch: 'pb.TelemetryBatch'):
        with conn.cursor() as cur:
            if batch.events:
                rows = [_event_row(session_id, batch.batch_sequence, e) for e in batch.events]
                psycopg2.extras.execute_values(
                    cur, f"INSERT INTO player_events ({', '.join(EVENT_COLUMNS)}) VALUES %s", rows,
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
                        session_id = 'unknown-session'
                        self._upsert_session(conn, pb.SessionInfo(session_id=session_id))
                        log.warning('batch recebido sem session_start previo -- usando "unknown-session"')

                    batch = msg.batch
                    try:
                        self._insert_batch(conn, session_id, batch)
                        yield pb.BatchAck(
                            batch_sequence=batch.batch_sequence,
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
