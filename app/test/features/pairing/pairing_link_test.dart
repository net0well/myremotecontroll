import 'package:flutter_test/flutter_test.dart';
import 'package:my_remote_controll/features/pairing/data/pairing_link.dart';

void main() {
  test('lê o formato v1 gerado pelo agente (hosts, porta, código, pin e nome codificado)', () {
    final link = PairingLink.tryParse(
      'mrc://pair?v=1&h=192.168.0.10,100.101.1.2,pc.tail1234.ts.net&p=47800'
      '&c=Qk1fY29kaWdvX2RlX3Rlc3Rl&f=q2V1CbPFlOaouT0Ez2evXTKt7GWz25n7UvP3lWn5PXw&n=PC%20DO%20NETO',
    );

    expect(link, isNotNull);
    expect(link!.hosts, ['192.168.0.10', '100.101.1.2', 'pc.tail1234.ts.net']);
    expect(link.port, 47800);
    expect(link.code, 'Qk1fY29kaWdvX2RlX3Rlc3Rl');
    expect(link.pin, 'q2V1CbPFlOaouT0Ez2evXTKt7GWz25n7UvP3lWn5PXw');
    expect(link.pcName, 'PC DO NETO');
  });

  test('recusa outros esquemas, versões e campos ausentes ou malformados', () {
    const pin = 'q2V1CbPFlOaouT0Ez2evXTKt7GWz25n7UvP3lWn5PXw';
    const code = 'Qk1fY29kaWdvX2RlX3Rlc3Rl';
    for (final raw in [
      'https://pc/pair?v=1&h=10.0.0.1&p=47800&c=$code&f=$pin',
      'mrc://pair?v=2&h=10.0.0.1&p=47800&c=$code&f=$pin',
      'mrc://pair?v=1&p=47800&c=$code&f=$pin',
      'mrc://pair?v=1&h=10.0.0.1&p=70000&c=$code&f=$pin',
      'mrc://pair?v=1&h=10.0.0.1&p=47800&c=curto&f=$pin',
      'mrc://pair?v=1&h=10.0.0.1&p=47800&c=$code&f=pin+com/barras',
      'texto qualquer',
    ]) {
      expect(PairingLink.tryParse(raw), isNull, reason: raw);
    }
  });
}
