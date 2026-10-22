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

const _rotulos = ['', 'Muito ruim', 'Ruim', 'Regular', 'Boa', 'Muito boa'];

/// "2026-10-02" → "02/10" (data sem fuso: vale como está).
String _diaMes(String iso) {
  final p = iso.split('-');
  return p.length == 3 ? '${p[2]}/${p[1]}' : iso;
}

/// Hoje, em Brasília, como "aaaa-mm-dd" (o mesmo formato da data da viagem).
String _hoje() {
  final b = DateTime.now().toUtc().subtract(const Duration(hours: 3));
  return '${b.year}-${b.month.toString().padLeft(2, '0')}-${b.day.toString().padLeft(2, '0')}';
}

/// Avaliação do motorista (`AvaliarViagens.tsx`, decisão de 19/10): ao desembarcar na clínica o
/// paciente avalia o motorista da ida e, ao chegar em casa, o do resgate — até 5 estrelas, cada um
/// uma vez, até 30 dias depois. Fica no topo do Transporte e atualiza a cada minuto, para o pedido
/// aparecer logo depois do desembarque. Sem nada a avaliar, não aparece.
class AvaliarViagens extends ConsumerStatefulWidget {
  const AvaliarViagens({super.key});

  @override
  ConsumerState<AvaliarViagens> createState() => _AvaliarViagensState();
}

class _AvaliarViagensState extends ConsumerState<AvaliarViagens> {
  List<ViagemParaAvaliar>? _lista;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _carregar();
    _timer = Timer.periodic(const Duration(minutes: 1), (_) => _carregar());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _carregar() async {
    try {
      final lista = await ref.read(apiProvider).viagensParaAvaliar();
      if (mounted) setState(() => _lista = lista);
    } on Object catch (_) {
      // Avaliação é extra: falha aqui não atrapalha o resto da tela.
      if (mounted) setState(() => _lista = const []);
    }
  }

  @override
  Widget build(BuildContext context) {
    final pendentes = (_lista ?? const <ViagemParaAvaliar>[]).where((v) => v.nota == null).toList();
    if (pendentes.isEmpty) return const SizedBox.shrink();
    final hoje = _hoje();
    return Padding(
      padding: const EdgeInsets.only(bottom: 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(LucideIcons.star, size: 20, color: CoresMarica.marica),
              const SizedBox(width: 8),
              Text('Como foi o motorista?', style: Txt.display(18)),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'Sua nota ajuda a melhorar o transporte.',
            style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43),
          ),
          const SizedBox(height: 12),
          for (final v in pendentes)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Cartao(
                padding: const EdgeInsets.all(16),
                corBorda: v.data == hoje ? CoresMarica.lagoa : CoresMarica.areia,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      v.trecho == 'Resgate' ? 'Volta para casa · ${v.destino}' : 'Ida até ${v.destino}',
                      style: Txt.sans(16, peso: FontWeight.w600),
                    ),
                    Text(
                      [
                        if (v.data == hoje) 'Hoje' else _diaMes(v.data),
                        if (v.hora != null && v.hora!.length >= 5)
                          '${v.trecho == 'Resgate' ? 'chegou em casa' : 'chegou'} às ${v.hora!.substring(0, 5)}',
                        if (v.motorista != null) 'Motorista: ${v.motorista}',
                      ].join(' · '),
                      style: Txt.sans(14, cor: CoresMarica.tintaMute),
                    ),
                    const SizedBox(height: 12),
                    _AvaliarUma(viagem: v, aoAvaliar: _carregar),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _Estrelas extends StatelessWidget {
  const _Estrelas({required this.nota, this.aoEscolher});

  final int nota;
  final ValueChanged<int>? aoEscolher;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (var n = 1; n <= 5; n++)
          Semantics(
            label: '$n — ${_rotulos[n]}',
            selected: n == nota,
            button: aoEscolher != null,
            child: InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: aoEscolher == null ? null : () => aoEscolher!(n),
              child: Padding(
                padding: const EdgeInsets.all(4),
                child: Icon(
                  LucideIcons.star,
                  size: 32,
                  color: n <= nota ? CoresMarica.ambar300 : CoresMarica.areia,
                ),
              ),
            ),
          ),
      ],
    );
  }
}

class _AvaliarUma extends ConsumerStatefulWidget {
  const _AvaliarUma({required this.viagem, required this.aoAvaliar});

  final ViagemParaAvaliar viagem;
  final Future<void> Function() aoAvaliar;

  @override
  ConsumerState<_AvaliarUma> createState() => _AvaliarUmaState();
}

class _AvaliarUmaState extends ConsumerState<_AvaliarUma> {
  final _comentario = TextEditingController();
  int _nota = 0;
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _comentario.dispose();
    super.dispose();
  }

  Future<void> _enviar() async {
    setState(() {
      _erro = null;
      _enviando = true;
    });
    try {
      final texto = _comentario.text.trim();
      await ref
          .read(apiProvider)
          .avaliarViagem(widget.viagem.sessaoId, widget.viagem.trecho, _nota, texto.isEmpty ? null : texto);
      await widget.aoAvaliar();
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _Estrelas(nota: _nota, aoEscolher: (n) => setState(() => _nota = n)),
        if (_nota > 0) ...[
          const SizedBox(height: 4),
          Text(_rotulos[_nota], style: Txt.sans(14, cor: CoresMarica.tintaMute)),
          const SizedBox(height: 12),
          TextField(
            controller: _comentario,
            maxLines: 3,
            maxLength: 500,
            decoration: const InputDecoration(hintText: 'Quer contar algo? (opcional)'),
          ),
          if (_erro != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(_erro!, style: Txt.sans(14, cor: CoresMarica.vermelho700, altura: 1.43)),
            ),
          BotaoPrimario(rotulo: 'Enviar avaliação', carregando: _enviando, onPressed: _enviar),
        ],
      ],
    );
  }
}
