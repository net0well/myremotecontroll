import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:signalr_netcore/signalr_client.dart';

/// Negociação do Hub (`POST /hubs/remote/negotiate`) feita pelo `HttpClient` com pinning, no lugar do cliente padrão
/// do `signalr_netcore`.
class PinnedSignalRHttpClient extends SignalRHttpClient {
  PinnedSignalRHttpClient(this._client);

  final HttpClient _client;

  @override
  Future<SignalRHttpResponse> send(SignalRHttpRequest request) async {
    final timeout = Duration(milliseconds: request.timeout ?? 15000);
    final httpRequest = await _client.openUrl(request.method!, Uri.parse(request.url!)).timeout(timeout);
    request.headers?.asMap?.forEach(httpRequest.headers.set);

    final content = request.content;
    if (content is String && content.isNotEmpty) {
      httpRequest.headers.contentType = ContentType.text;
      httpRequest.write(content);
    } else if (content is List<int>) {
      httpRequest.add(content);
    }

    final response = await httpRequest.close().timeout(timeout);
    final body = await response.transform(utf8.decoder).join().timeout(timeout);
    return SignalRHttpResponse(response.statusCode, statusText: response.reasonPhrase, content: body);
  }
}

/// Transporte WebSocket do Hub sobre o `HttpClient` com pinning (`WebSocket.connect(customClient:)`). O transporte
/// padrão do `signalr_netcore` usa o cliente estático do `dart:io`, que não passa pelo pin (risco R4).
///
/// No celular o token vai no header `Authorization` do upgrade, como no transporte padrão fora da web.
class PinnedWebSocketTransport implements ITransport {
  PinnedWebSocketTransport({required this._client, required this._accessToken});

  final HttpClient _client;
  final Future<String> Function() _accessToken;
  WebSocket? _socket;
  StreamSubscription<Object?>? _subscription;
  bool _closed = false;

  @override
  OnReceive? onReceive;

  @override
  OnClose? onClose;

  @override
  Future<void> connect(String? url, TransferFormat transferFormat) async {
    final token = await _accessToken();
    _closed = false;
    // O socket é fechado em stop() ou quando o servidor encerra (_close).
    // ignore: close_sinks
    final socket = await WebSocket.connect(
      url!.replaceFirst('http', 'ws'),
      headers: {'Authorization': 'Bearer $token'},
      customClient: _client,
    );
    _socket = socket;
    _subscription = socket.listen(
      _receive,
      onError: (Object error) => _close(error is Exception ? error : Exception('$error')),
      onDone: () => _close(
        socket.closeCode == null || socket.closeCode == WebSocketStatus.normalClosure
            ? null
            : Exception('WebSocket fechado pelo servidor (código ${socket.closeCode}).'),
      ),
      cancelOnError: true,
    );
  }

  @override
  Future<void> send(Object data) async {
    final socket = _socket;
    if (socket == null || socket.readyState != WebSocket.open) {
      throw StateError('O WebSocket do Hub não está aberto.');
    }

    socket.add(data);
  }

  @override
  Future<void> stop() async {
    await _subscription?.cancel();
    await _socket?.close(WebSocketStatus.normalClosure);
    _close(null);
  }

  void _receive(Object? message) {
    try {
      onReceive?.call(message);
    } on Object catch (error) {
      unawaited(_subscription?.cancel());
      unawaited(_socket?.close(WebSocketStatus.internalServerError));
      _close(error is Exception ? error : Exception('$error'));
    }
  }

  /// O `HttpConnection` só termina o `stop()` quando o transporte avisa que fechou, então o aviso sai uma vez,
  /// em qualquer caminho.
  void _close(Exception? error) {
    if (_closed) {
      return;
    }

    _closed = true;
    _socket = null;
    _subscription = null;
    onClose?.call(error: error);
  }
}
