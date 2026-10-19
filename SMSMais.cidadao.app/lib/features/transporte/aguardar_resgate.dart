import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

String? _hhmm(String? t) =>
    t == null || t.length < 5 ? null : t.substring(0, 5);

String? _horaDe(String? iso) {
  final d = iso == null ? null : DateTime.tryParse(iso)?.toLocal();
  if (d == null) return null;
  return '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
}

/// O resgate de hoje (`AguardarResgate.tsx`): depois do atendimento, o carro busca o paciente na
/// unidade e o leva para casa. Quando estoura a previsão do procedimento, pergunta "você já está
/// disponível para retorno?": "Sim" põe o paciente no topo da lista dos motoristas; "Não, ainda não
/// terminei" pergunta de novo em 10 minutos. Antes disso, quem terminou antes do previsto avisa
/// sozinho ("Já terminei, estou disponível para retorno").
/// Quando um carro marca, ele vê qual vai buscá-lo. Some quando não há resgate hoje.
class AguardarResgate extends ConsumerStatefulWidget {
  const AguardarResgate({super.key});

  @override
  ConsumerState<AguardarResgate> createState() => _AguardarResgateState();
}

class _AguardarResgateState extends ConsumerState<AguardarResgate> {
  ResgateDoPaciente? _resgate;
  Timer? _timer;
  bool _enviando = false;
  String? _erro;

  @override
  void initState() {
    super.initState();
    unawaited(_carregar());
    _timer = Timer.periodic(
      const Duration(minutes: 1),
      (_) => unawaited(_carregar()),
    );
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _carregar() async {
    try {
      final r = await ref.read(apiProvider).resgate();
      if (mounted) setState(() => _resgate = r);
    } on Object catch (_) {
      // Falha aqui não atrapalha o resto da tela.
      if (mounted) setState(() => _resgate = null);
    }
  }

  Future<void> _responder(
    ResgateDoPaciente r, {
    required bool disponivel,
  }) async {
    setState(() {
      _erro = null;
      _enviando = true;
    });
    try {
      await ref
          .read(apiProvider)
          .responderResgate(r.sessaoId, disponivel: disponivel);
      await _carregar();
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final r = _resgate;
    if (r == null) return const SizedBox.shrink();
    final mute = Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43);
    final texto = Txt.sans(14, altura: 1.43);
    return Padding(
      padding: const EdgeInsets.only(bottom: 24),
      child: Cartao(
        padding: const EdgeInsets.all(16),
        corBorda: CoresMarica.lagoa,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(
                  LucideIcons.car,
                  size: 20,
                  color: CoresMarica.marica,
                ),
                const SizedBox(width: 8),
                Text('Seu resgate de hoje', style: Txt.display(18)),
              ],
            ),
            const SizedBox(height: 4),
            Text(r.unidade, style: mute),
            const SizedBox(height: 12),
            if (r.embarcou)
              Text(
                'Você está a caminho de casa${r.veiculoPlaca != null ? ' no carro ${r.veiculoPlaca}' : ''}.',
                style: texto,
              )
            else ...[
              if (r.veiculoPlaca != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Text(
                    'O carro ${r.veiculoPlaca}${r.veiculoModelo != null ? ' (${r.veiculoModelo})' : ''} vai buscar você.',
                    style: Txt.sans(14, peso: FontWeight.w600),
                  ),
                ),
              if (r.disponibilidade == 'NoLocal')
                Text(
                  'Os motoristas já sabem que você está esperando'
                  '${_horaDe(r.noLocalDesde) != null ? ' (desde ${_horaDe(r.noLocalDesde)})' : ''}. Aguarde no local.',
                  style: texto,
                )
              else if (r.perguntaPendente) ...[
                Text(
                  'Você já está disponível para retorno?',
                  style: Txt.sans(16, peso: FontWeight.w600),
                ),
                if (_erro != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      _erro!,
                      style: Txt.sans(
                        14,
                        cor: CoresMarica.vermelho700,
                        altura: 1.43,
                      ),
                    ),
                  ),
                const SizedBox(height: 12),
                BotaoPrimario(
                  rotulo: 'Sim, já estou disponível',
                  carregando: _enviando,
                  onPressed: () => _responder(r, disponivel: true),
                ),
                const SizedBox(height: 8),
                BotaoFantasma(
                  rotulo: 'Não, ainda não terminei o procedimento',
                  onPressed: _enviando
                      ? null
                      : () => _responder(r, disponivel: false),
                ),
              ] else ...[
                if (_horaDe(r.proximaPerguntaAs) != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text(
                      'Perguntamos de novo às ${_horaDe(r.proximaPerguntaAs)}.',
                      style: mute,
                    ),
                  )
                else if (_hhmm(r.disponivelAs) != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text(
                      'Previsão de saída do atendimento: ${_hhmm(r.disponivelAs)}',
                      style: mute,
                    ),
                  ),
                Text(
                  'Terminou o atendimento? Avise, e os motoristas veem na hora que você está esperando.',
                  style: mute,
                ),
                if (_erro != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      _erro!,
                      style: Txt.sans(
                        14,
                        cor: CoresMarica.vermelho700,
                        altura: 1.43,
                      ),
                    ),
                  ),
                const SizedBox(height: 12),
                BotaoPrimario(
                  rotulo: 'Já terminei, estou disponível para retorno',
                  carregando: _enviando,
                  onPressed: () => _responder(r, disponivel: true),
                ),
              ],
            ],
          ],
        ),
      ),
    );
  }
}
