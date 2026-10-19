import 'dart:convert';
import 'dart:typed_data';

import 'package:crypto/crypto.dart';

/// Pin do certificado do agente: SHA-256 da SubjectPublicKeyInfo em base64url sem `=` (ADR-0006).
///
/// O mesmo cálculo do agente (`CertificatePin` em C#) e do `openssl pkey -pubin -outform der | sha256`.
String spkiPin(Uint8List certificateDer) {
  final digest = sha256.convert(extractSubjectPublicKeyInfo(certificateDer));
  return base64Url.encode(digest.bytes).replaceAll('=', '');
}

/// Extrai a SubjectPublicKeyInfo (DER) de um certificado X.509 em DER.
///
/// Certificate ::= SEQUENCE { tbsCertificate SEQUENCE { [0] version OPCIONAL, serialNumber, signature, issuer,
/// validity, subject, subjectPublicKeyInfo, ... }, ... }
Uint8List extractSubjectPublicKeyInfo(Uint8List der) {
  final certificate = _DerElement.read(der, 0)..expectTag(_sequence);
  final tbs = _DerElement.read(der, certificate.contentStart)..expectTag(_sequence);

  var offset = tbs.contentStart;
  if (_DerElement.read(der, offset).tag == _explicitVersion) {
    offset = _DerElement.read(der, offset).end;
  }

  // serialNumber, signature, issuer, validity, subject.
  for (var i = 0; i < 5; i++) {
    offset = _DerElement.read(der, offset).end;
  }

  final spki = _DerElement.read(der, offset)..expectTag(_sequence);
  return Uint8List.sublistView(der, spki.start, spki.end);
}

const _sequence = 0x30;
const _explicitVersion = 0xA0;

final class _DerElement {
  _DerElement(this.tag, this.start, this.contentStart, this.end);

  final int tag;
  final int start;
  final int contentStart;
  final int end;

  static _DerElement read(Uint8List der, int offset) {
    if (offset + 2 > der.length) {
      throw const FormatException('Certificado DER truncado.');
    }

    final tag = der[offset];
    var length = der[offset + 1];
    var contentStart = offset + 2;
    if (length & 0x80 != 0) {
      final lengthBytes = length & 0x7F;
      if (lengthBytes == 0 || lengthBytes > 4 || contentStart + lengthBytes > der.length) {
        throw const FormatException('Comprimento DER inválido.');
      }

      length = 0;
      for (var i = 0; i < lengthBytes; i++) {
        length = (length << 8) | der[contentStart + i];
      }
      contentStart += lengthBytes;
    }

    final end = contentStart + length;
    if (end > der.length) {
      throw const FormatException('Certificado DER truncado.');
    }

    return _DerElement(tag, offset, contentStart, end);
  }

  void expectTag(int expected) {
    if (tag != expected) {
      throw FormatException('Estrutura DER inesperada (tag 0x${tag.toRadixString(16)}).');
    }
  }
}
