import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:my_remote_controll/core/network/spki.dart';

Uint8List _derFromPem(String pem) {
  final body = pem
      .replaceAll('-----BEGIN CERTIFICATE-----', '')
      .replaceAll('-----END CERTIFICATE-----', '')
      .replaceAll(RegExp(r'\s'), '');
  return base64.decode(body);
}

void main() {
  test('o pin calculado no app é igual ao do OpenSSL e do agente (C#)', () {
    final pem = File('test/fixtures/pin_fixture.pem').readAsStringSync();
    final expected = File('test/fixtures/pin_fixture.expected').readAsStringSync().trim();

    expect(spkiPin(_derFromPem(pem)), expected);
  });

  test('DER truncado é recusado com FormatException', () {
    final pem = File('test/fixtures/pin_fixture.pem').readAsStringSync();
    final der = _derFromPem(pem);

    expect(() => spkiPin(Uint8List.sublistView(der, 0, 40)), throwsFormatException);
  });
}
