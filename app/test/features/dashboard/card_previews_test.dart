import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';
import 'package:my_remote_controll/core/widgets/previews.dart';
import 'package:my_remote_controll/features/dashboard/ui/dashboard_card_previews.dart';

/// Cada preview do painel monta sem erro nem overflow (o card ocupa 320 × 200, perto do tamanho real na grade).
void main() {
  final previews = <String, (Widget Function(), Widget Function(Widget))>{
    'CPU normal': (cpuCardNormal, darkPreviewWrapper),
    'CPU atenção': (cpuCardAttention, darkPreviewWrapper),
    'CPU crítico': (cpuCardCritical, darkPreviewWrapper),
    'CPU carregando': (cpuCardLoading, darkPreviewWrapper),
    'CPU sem permissão': (cpuCardNoPermission, darkPreviewWrapper),
    'CPU offline': (cpuCardOffline, darkPreviewWrapper),
    'CPU contador indisponível': (cpuCardUnavailable, darkPreviewWrapper),
    'Memória': (memoryCardNormal, darkPreviewWrapper),
    'Rede': (networkCardNormal, darkPreviewWrapper),
    'Discos crítico': (disksCardCritical, darkPreviewWrapper),
    'GPU': (gpuCardNormal, darkPreviewWrapper),
    'Bateria atenção': (batteryCardAttention, darkPreviewWrapper),
    'Energia ações': (powerCardActions, darkPreviewWrapper),
    'Energia contagem': (powerCardCountdown, darkPreviewWrapper),
    'Energia somente leitura': (powerCardReadOnly, darkPreviewWrapper),
    'PC online': (pcCardOnline, darkPreviewWrapper),
    'PC offline': (pcCardOffline, darkPreviewWrapper),
    'PC claro': (pcCardLight, lightPreviewWrapper),
  };

  for (final MapEntry(key: name, value: (preview, wrapper)) in previews.entries) {
    testWidgets('Dado o preview "$name", Quando monta, Então não há erro nem overflow', (tester) async {
      tester.view.physicalSize = const Size(400, 400);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(wrapper(preview()));
      await tester.pump(const Duration(milliseconds: 300));

      expect(tester.takeException(), isNull);
    });
  }
}
