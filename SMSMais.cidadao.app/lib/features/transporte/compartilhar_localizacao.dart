import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/localizacao/localizacao.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';
import 'package:url_launcher/url_launcher.dart';

const _explicacao = [
  'Só o motorista do carro que vai buscar você vê onde você está.',
  'Só enquanto você espera o embarque, e só com o app aberto.',
  'Para sozinho quando você embarcar. Você pode parar quando quiser.',
  'Guardamos só a última posição, e ela é apagada no embarque.',
];

/// O convite para compartilhar a localização com o motorista no dia da viagem (ADR-0082;
/// `CompartilharLocalizacao.tsx`). Aparece só enquanto o paciente espera um embarque (a ida, a partir
/// de 2 h antes da busca, ou o resgate) e ainda não compartilha. Antes do pedido de permissão do
/// aparelho, explica quem vê, quando e como parar.
class CompartilharLocalizacao extends ConsumerStatefulWidget {
  const CompartilharLocalizacao({this.margem = const EdgeInsets.only(bottom: 24), super.key});

  /// Espaço em volta do cartão (só existe quando ele aparece).
  final EdgeInsets margem;

  @override
  ConsumerState<CompartilharLocalizacao> createState() => _CompartilharLocalizacaoState();
}

class _CompartilharLocalizacaoState extends ConsumerState<CompartilharLocalizacao> {
  bool _explicando = false;
  bool _adiado = false;
  late final TapGestureRecognizer _politica = TapGestureRecognizer()
    ..onTap = () => launchUrl(Uri.parse('$appWebUrl/privacidade/'), mode: LaunchMode.externalApplication);

  @override
  void dispose() {
    _politica.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(localizacaoProvider);
    final espera = estado.espera;
    if (espera == null || espera.compartilhando || _adiado) return const SizedBox.shrink();

    final hora = espera.horaBusca;
    final quando = espera.trecho == 'Resgate'
        ? 'O carro vai buscar você em ${espera.unidade}.'
        : hora != null && hora.length >= 5
            ? 'O carro vai buscar você hoje. Busca prevista às ${hora.substring(0, 5)}.'
            : 'O carro vai buscar você hoje.';
    final texto = Txt.sans(14, altura: 1.43);
    final miudo = Txt.sans(12, cor: CoresMarica.tintaMute, altura: 1.4);

    return Padding(
      padding: widget.margem,
      child: Cartao(
        padding: const EdgeInsets.all(16),
        destaque: CoresMarica.lagoa.withValues(alpha: 0.4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(LucideIcons.mapPin, size: 20, color: CoresMarica.marica),
                const SizedBox(width: 8),
                Expanded(child: Text('Ajude o motorista a achar você', style: Txt.display(18))),
              ],
            ),
            const SizedBox(height: 4),
            Text(quando, style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43)),
            const SizedBox(height: 12),
            if (!_explicando) ...[
              Text('Compartilhe a sua localização: o motorista vê onde você está e chega mais fácil.', style: texto),
              const SizedBox(height: 12),
              BotaoPrimario(
                rotulo: 'Compartilhar minha localização',
                onPressed: () => setState(() => _explicando = true),
              ),
            ] else ...[
              for (final item in _explicacao)
                Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      SizedBox(width: 20, child: Text('•', textAlign: TextAlign.center, style: texto)),
                      Expanded(child: Text(item, style: texto)),
                    ],
                  ),
                ),
              const SizedBox(height: 4),
              Text.rich(
                TextSpan(
                  style: miudo,
                  children: [
                    const TextSpan(text: 'Depois de tocar em Compartilhar, o celular pede para usar a sua localização. '),
                    TextSpan(
                      text: 'Política de Privacidade',
                      style: miudo.copyWith(decoration: TextDecoration.underline, decorationColor: CoresMarica.tintaMute),
                      recognizer: _politica,
                    ),
                  ],
                ),
              ),
              if (estado.erro != null) ...[
                const SizedBox(height: 8),
                Semantics(
                  liveRegion: true,
                  child: Text(estado.erro!, style: Txt.sans(14, cor: CoresMarica.vermelho700, altura: 1.43)),
                ),
              ],
              const SizedBox(height: 12),
              BotaoPrimario(
                rotulo: 'Compartilhar',
                carregando: estado.iniciando,
                onPressed: () => ref.read(localizacaoProvider.notifier).compartilhar(),
              ),
              const SizedBox(height: 8),
              BotaoFantasma(rotulo: 'Agora não', onPressed: () => setState(() => _adiado = true)),
            ],
          ],
        ),
      ),
    );
  }
}
