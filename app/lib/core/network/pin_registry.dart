import 'dart:io';

import 'spki.dart';

/// Pins aceitos por host:porta. Todo TLS do app com um PC passa por aqui (REST, negociação do Hub e WebSocket), e só o
/// certificado com o pin recebido no QR de pareamento é aceito para cada PC (ADR-0006, spec 002).
class PinRegistry {
  final Map<String, String> _pins = {};
  final Map<String, String> _mismatches = {};

  static String _key(String host, int port) => '${host.toLowerCase()}:$port';

  void register(Iterable<String> hosts, int port, String pin) {
    for (final host in hosts) {
      _pins[_key(host, port)] = pin;
      _mismatches.remove(_key(host, port));
    }
  }

  void unregisterPin(String pin) => _pins.removeWhere((_, value) => value == pin);

  bool isPinned(String host, int port) => _pins.containsKey(_key(host, port));

  /// Verdadeiro se o certificado tem o pin esperado para o host. Pin diferente fica registrado como divergência, para
  /// a conexão mostrar o alerta de possível ataque (spec 002, AC-3) em vez de um erro genérico.
  bool matches(X509Certificate certificate, String host, int port) {
    final key = _key(host, port);
    final expected = _pins[key];
    if (expected == null) {
      return false;
    }

    final presented = spkiPin(certificate.der);
    if (_constantTimeEquals(presented, expected)) {
      _mismatches.remove(key);
      return true;
    }

    _mismatches[key] = presented;
    return false;
  }

  bool hadMismatch(String host, int port) => _mismatches.containsKey(_key(host, port));

  static bool _constantTimeEquals(String a, String b) {
    if (a.length != b.length) {
      return false;
    }

    var diff = 0;
    for (var i = 0; i < a.length; i++) {
      diff |= a.codeUnitAt(i) ^ b.codeUnitAt(i);
    }
    return diff == 0;
  }
}

/// `HttpClient` que só fala com PCs pareados: o TLS é feito sem nenhuma raiz confiável, então todo certificado cai no
/// `badCertificateCallback` e só passa o que tem o pin do host:porta. Assim o pin vale sempre, mesmo que o sistema
/// confie no certificado (o Windows confia no certificado do agente no próprio PC, verificado no teste e2e).
///
/// Usado pelo REST (dio), pela negociação do Hub e pelo WebSocket do SignalR. Não use `HttpOverrides` para isso: o
/// `WebSocket` do `dart:io` guarda um `HttpClient` estático criado na primeira conexão do processo, e o override só
/// vale se for instalado antes dela (risco R4 do documento base).
HttpClient pinnedHttpClient(PinRegistry pins) =>
    HttpClient(context: SecurityContext(withTrustedRoots: false))..badCertificateCallback = pins.matches;
