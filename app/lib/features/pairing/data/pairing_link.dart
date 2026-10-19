/// Conteúdo do QR de pareamento, versão 1 (spec 002):
/// `mrc://pair?v=1&h=<hosts separados por vírgula>&p=<porta>&c=<código>&f=<pin SPKI>&n=<nome do PC>`.
class PairingLink {
  const PairingLink({
    required this.hosts,
    required this.port,
    required this.code,
    required this.pin,
    required this.pcName,
  });

  static const supportedVersion = '1';

  final List<String> hosts;
  final int port;
  final String code;
  final String pin;
  final String pcName;

  /// Nulo se o texto não for um link de pareamento válido desta versão.
  static PairingLink? tryParse(String raw) {
    final uri = Uri.tryParse(raw.trim());
    if (uri == null || uri.scheme != 'mrc' || uri.host != 'pair') {
      return null;
    }

    final query = uri.queryParameters;
    final hosts = (query['h'] ?? '').split(',').map((h) => h.trim()).where((h) => h.isNotEmpty).toList();
    final port = int.tryParse(query['p'] ?? '');
    final code = query['c'] ?? '';
    final pin = query['f'] ?? '';
    if (query['v'] != supportedVersion || hosts.isEmpty || port == null || port <= 0 || port > 65535) {
      return null;
    }

    if (!_base64Url.hasMatch(code) || !_base64Url.hasMatch(pin)) {
      return null;
    }

    return PairingLink(hosts: hosts, port: port, code: code, pin: pin, pcName: query['n'] ?? '');
  }

  static final _base64Url = RegExp(r'^[A-Za-z0-9_-]{16,64}$');
}
