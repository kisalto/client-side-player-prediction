"""
udp_bridge.py
---------------------------------------------------------------------------
Fase 1.5: ponte entre o tmwa-map (UDP, telemetry.cpp) e o TelemetryService
(gRPC, telemetry_service.py). Ver docs/architecture.md na raiz do repo pro
desenho geral e a justificativa de por que essa ponte existe (gRPC nao
entra direto no processo do jogo).

O QUE FAZ
1. Escuta UDP numa thread (padrao fire-and-forget do telemetry.cpp: cada
   datagrama e uma linha JSON, sem esperar resposta).
2. Cada datagrama recebido vira um PlayerEvent, com bridge_recv_time =
   agora (o tmwa nao manda timestamp -- esse E o timestamp de verdade
   usado no resto do pipeline).
3. Acumula eventos num buffer; a cada `--flush-interval` segundos (ou ao
   atingir `--max-batch-size`, o que vier primeiro) empacota tudo num
   TelemetryBatch e manda pro TelemetryService via stream gRPC
   (StreamTelemetry).
4. Se o stream cair (TelemetryService reiniciando, rede instavel), tenta
   reconectar com backoff. Eventos que ainda nao foram enviados continuam
   no buffer -- so se perdem se o processo da bridge morrer antes de
   conseguir reconectar (aceitavel: mesma filosofia best-effort do UDP
   vindo do jogo).

Uso:
    python udp_bridge.py --udp-port 9999 --grpc-target localhost:50051
"""
import argparse
import json
import logging
import queue
import socket
import threading
import time
import uuid
from datetime import datetime, timezone

import grpc

import telemetry_pb2 as pb
import telemetry_pb2_grpc as pb_grpc

logging.basicConfig(level=logging.INFO, format='%(asctime)s [%(levelname)s] %(message)s')
log = logging.getLogger('udp_bridge')


def udp_listener(sock, buffer, buffer_lock, stop_event):
    """Roda numa thread separada: recebe datagramas UDP e empilha no buffer."""
    while not stop_event.is_set():
        try:
            data, _addr = sock.recvfrom(4096)
        except OSError:
            if stop_event.is_set():
                break
            raise

        try:
            payload = json.loads(data.decode('utf-8'))
        except (UnicodeDecodeError, json.JSONDecodeError):
            log.warning('datagrama UDP invalido, ignorando: %r', data[:100])
            continue

        event = pb.PlayerEvent(
            event_type=payload.get('event', ''),
            char_id=payload.get('char_id', 0),
            x=payload.get('x', 0),
            y=payload.get('y', 0),
            hp=payload.get('hp', 0),
            max_hp=payload.get('max_hp', 0),
            dead=bool(payload.get('dead', False)),
            extra=payload.get('extra', 0),
            bridge_recv_time=time.time(),
            dest_x=payload.get('dest_x', -1),
            dest_y=payload.get('dest_y', -1),
            target_id=payload.get('target_id', 0),
            continuous=bool(payload.get('continuous', False)),
        )
        with buffer_lock:
            buffer.append(event)


def flush_loop(buffer, buffer_lock, out_queue, flush_interval, max_batch_size, stop_event):
    """Roda numa thread separada: periodicamente esvazia o buffer em TelemetryBatch."""
    batch_sequence = 0
    while not stop_event.is_set():
        time.sleep(flush_interval)
        with buffer_lock:
            if not buffer:
                continue
            events, buffer[:] = buffer[:max_batch_size], buffer[max_batch_size:]
        if events:
            out_queue.put(pb.TelemetryBatch(events=events, batch_sequence=batch_sequence))
            batch_sequence += 1


def request_generator(session_info, out_queue, stop_event):
    """Gerador consumido pelo grpc: primeiro session_start, depois os batches
    que forem chegando na fila (bloqueia esperando, com timeout pra poder
    checar stop_event periodicamente)."""
    yield pb.TelemetryStreamMessage(session_start=session_info)
    while not stop_event.is_set():
        try:
            batch = out_queue.get(timeout=1.0)
        except queue.Empty:
            continue
        yield pb.TelemetryStreamMessage(batch=batch)


def run_bridge(udp_host, udp_port, grpc_target, flush_interval, max_batch_size, reconnect_delay):
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((udp_host, udp_port))
    log.info('UDP escutando em %s:%d', udp_host, udp_port)

    buffer = []
    buffer_lock = threading.Lock()
    out_queue = queue.Queue()
    stop_event = threading.Event()

    session_info = pb.SessionInfo(
        session_id=str(uuid.uuid4()),
        recorded_at=datetime.now(timezone.utc).isoformat(),
        bridge_host=socket.gethostname(),
    )
    log.info('sessao da bridge: %s', session_info.session_id)

    threading.Thread(target=udp_listener, args=(sock, buffer, buffer_lock, stop_event), daemon=True).start()
    threading.Thread(
        target=flush_loop,
        args=(buffer, buffer_lock, out_queue, flush_interval, max_batch_size, stop_event),
        daemon=True,
    ).start()

    try:
        while True:
            try:
                with grpc.insecure_channel(grpc_target) as channel:
                    stub = pb_grpc.TelemetryServiceStub(channel)
                    log.info('conectado ao TelemetryService em %s', grpc_target)
                    for ack in stub.StreamTelemetry(request_generator(session_info, out_queue, stop_event)):
                        if not ack.ok:
                            log.warning('batch %d rejeitado: %s', ack.batch_sequence, ack.message)
            except grpc.RpcError as exc:
                log.warning('stream gRPC caiu (%s) -- reconectando em %.1fs', exc.code(), reconnect_delay)
                time.sleep(reconnect_delay)
    except KeyboardInterrupt:
        pass
    finally:
        stop_event.set()
        sock.close()


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--udp-host', default='0.0.0.0')
    ap.add_argument('--udp-port', type=int, default=9999)
    ap.add_argument('--grpc-target', default='localhost:50051')
    ap.add_argument('--flush-interval', type=float, default=1.0, help='segundos entre flushes do buffer')
    ap.add_argument('--max-batch-size', type=int, default=500, help='eventos maximos por TelemetryBatch')
    ap.add_argument('--reconnect-delay', type=float, default=2.0, help='segundos antes de tentar reconectar ao gRPC')
    args = ap.parse_args()
    run_bridge(args.udp_host, args.udp_port, args.grpc_target, args.flush_interval,
               args.max_batch_size, args.reconnect_delay)


if __name__ == '__main__':
    main()
