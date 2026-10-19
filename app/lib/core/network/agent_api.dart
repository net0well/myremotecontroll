import 'dart:io';

import 'package:dio/dio.dart';
import 'package:dio/io.dart';

import '../contracts/contracts.dart';
import '../errors/agent_exception.dart';
import 'pin_registry.dart';

/// Cliente REST de um PC (`https://<host>:<porta>/api/v1`). Cada conexão TLS é validada pelo pin do PC.
class AgentApi {
  AgentApi({
    required this.host,
    required this.port,
    required PinRegistry pins,
    String? token,
    Duration connectTimeout = const Duration(seconds: 5),
    Duration receiveTimeout = const Duration(seconds: 20),
  }) : _pins = pins,
       _dio = Dio(
         BaseOptions(
           baseUrl: 'https://$host:$port',
           connectTimeout: connectTimeout,
           receiveTimeout: receiveTimeout,
           headers: {if (token != null) 'Authorization': 'Bearer $token'},
         ),
       ) {
    _dio.httpClientAdapter = IOHttpClientAdapter(
      createHttpClient: () => pinnedHttpClient(pins),
      // Chamado em toda conexão, inclusive com certificado válido: o pin vale sempre, não só para autoassinado.
      validateCertificate: (certificate, host, port) => certificate != null && pins.matches(certificate, host, port),
    );
  }

  final String host;
  final int port;
  final PinRegistry _pins;
  final Dio _dio;

  Future<AgentInfo> getInfo() async =>
      AgentInfo.fromJson(await _send(() => _dio.get<Map<String, dynamic>>('/api/v1/info')));

  /// O agente segura esta requisição até a confirmação no PC (até 60 s).
  Future<Map<String, dynamic>> completePairing({
    required String code,
    required String deviceName,
    required DevicePlatform platform,
  }) => _send(
    () => _dio.post<Map<String, dynamic>>(
      '/api/v1/pairing/complete',
      data: {'code': code, 'deviceName': deviceName, 'platform': platform.name},
      options: Options(receiveTimeout: const Duration(seconds: 75)),
    ),
  );

  /// Novo token; o anterior continua válido por 24 h.
  Future<String> rotateToken() async =>
      (await _send(() => _dio.post<Map<String, dynamic>>('/api/v1/devices/me/rotate-token')))['token'] as String;

  Future<void> revokeDevice(String deviceId) =>
      _send(() => _dio.delete<Map<String, dynamic>>('/api/v1/devices/$deviceId'));

  Future<Map<String, dynamic>> getAudit({String? cursor, int limit = 50}) => _send(
    () => _dio.get<Map<String, dynamic>>('/api/v1/audit', queryParameters: {'limit': limit, 'cursor': ?cursor}),
  );

  void close() => _dio.close(force: true);

  Future<Map<String, dynamic>> _send(Future<Response<Map<String, dynamic>>> Function() request) async {
    try {
      final response = await request();
      return response.data ?? const {};
    } on DioException catch (error) {
      throw _translate(error);
    }
  }

  AgentException _translate(DioException error) {
    final response = error.response;
    if (response != null) {
      final body = response.data;
      final code = body is Map<String, dynamic> ? body['code'] as String? : null;
      final title = body is Map<String, dynamic> ? body['title'] as String? : null;
      return AgentException(
        response.statusCode == HttpStatus.unauthorized ? AgentFailureKind.unauthorized : AgentFailureKind.rejected,
        code: code,
        statusCode: response.statusCode,
        serverMessage: title,
      );
    }

    if (_pins.hadMismatch(host, port)) {
      return const AgentException(AgentFailureKind.certificateMismatch);
    }

    return const AgentException(AgentFailureKind.unreachable);
  }
}
