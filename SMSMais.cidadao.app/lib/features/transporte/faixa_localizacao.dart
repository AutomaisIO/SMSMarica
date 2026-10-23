import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/localizacao/gps.dart';
import 'package:sms_mais_cidadao/shared/localizacao/localizacao.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';

String _haQuanto(DateTime quando) {
  final min = DateTime.now().difference(quando).inMinutes;
  return min < 1 ? 'agora há pouco' : 'há $min min';
}

/// O estado do GPS em uma frase e, quando o aparelho travou, o que tocar para destravar.
(String, String?) _detalhe(EstadoLocalizacao e) {
  if (e.situacao == SituacaoGps.bloqueado) {
    return switch (e.acesso) {
      AcessoGps.negado => ('O celular não deixou usar a localização.', 'Permitir'),
      AcessoGps.negadoDeVez => (
          'O celular não deixou usar a localização. Libere a localização do app nas configurações do celular.',
          'Abrir configurações',
        ),
      AcessoGps.desligado => ('A localização do celular está desligada.', 'Ligar a localização'),
      _ => ('Este celular não informa a localização.', null),
    };
  }
  if (e.situacao == SituacaoGps.pedindoPermissao) return ('Permita o acesso à localização no aviso do celular.', null);
  final ultimo = e.ultimoEnvio;
  if (ultimo == null) return ('Procurando a sua posição…', null);
  if (e.acesso == AcessoGps.aproximado) {
    return ('Enviada ${_haQuanto(ultimo)}, aproximada: o motorista vê só a região.', 'Usar a localização exata');
  }
  return ('Enviada ${_haQuanto(ultimo)}.', null);
}

/// A faixa fixa enquanto o paciente compartilha a localização com o motorista (ADR-0082;
/// `FaixaLocalizacao.tsx`), em todas as telas logadas, logo abaixo da barra de cima: o estado do GPS,
/// o "Parar" e o lembrete de deixar o app aberto.
class FaixaLocalizacao extends ConsumerStatefulWidget {
  const FaixaLocalizacao({super.key});

  @override
  ConsumerState<FaixaLocalizacao> createState() => _FaixaLocalizacaoState();
}

class _FaixaLocalizacaoState extends ConsumerState<FaixaLocalizacao> {
  Timer? _tique;

  @override
  void initState() {
    super.initState();
    // Reescreve o "há X min" sem esperar a próxima posição.
    _tique = Timer.periodic(const Duration(seconds: 15), (_) {
      if (mounted && ref.read(localizacaoProvider).compartilhando) setState(() {});
    });
  }

  @override
  void dispose() {
    _tique?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(localizacaoProvider);
    if (!estado.compartilhando) return const SizedBox.shrink();
    final controle = ref.read(localizacaoProvider.notifier);
    final (detalhe, acao) = _detalhe(estado);

    return Semantics(
      container: true,
      liveRegion: true,
      child: Container(
        width: double.infinity,
        decoration: BoxDecoration(
          color: CoresMarica.lagoaClaro,
          border: Border(bottom: BorderSide(color: CoresMarica.lagoa.withValues(alpha: 0.3))),
        ),
        padding: const EdgeInsets.fromLTRB(16, 4, 4, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(LucideIcons.mapPin, size: 16, color: CoresMarica.lagoa),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Compartilhando sua localização com o motorista',
                    style: Txt.sans(14, peso: FontWeight.w600, altura: 1.3),
                  ),
                ),
                _BotaoFaixa(rotulo: 'Parar', onTap: controle.parar),
              ],
            ),
            Padding(
              padding: const EdgeInsets.only(right: 12),
              child: Text(
                '$detalhe Deixe o app aberto: fechado, ele não envia.',
                style: Txt.sans(12, cor: CoresMarica.tintaMute, altura: 1.4),
              ),
            ),
            if (acao != null)
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: _BotaoFaixa(rotulo: acao, onTap: controle.permitir, recuo: -12),
              ),
          ],
        ),
      ),
    );
  }
}

/// Botão de texto da faixa (`text-sm font-semibold text-marica`), com área de toque de 44 px.
class _BotaoFaixa extends StatelessWidget {
  const _BotaoFaixa({required this.rotulo, required this.onTap, this.recuo = 0});

  final String rotulo;
  final Future<void> Function() onTap;

  /// Puxa o botão para a esquerda, para o texto alinhar com o da faixa.
  final double recuo;

  @override
  Widget build(BuildContext context) {
    return Transform.translate(
      offset: Offset(recuo, 0),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: () => unawaited(onTap()),
          borderRadius: BorderRadius.circular(RaiosMarica.xl),
          highlightColor: CoresMarica.marica.withValues(alpha: 0.1),
          child: ConstrainedBox(
            constraints: const BoxConstraints(minHeight: 44),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Center(
                widthFactor: 1,
                child: Text(rotulo, style: Txt.sans(14, peso: FontWeight.w600, cor: CoresMarica.marica)),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
