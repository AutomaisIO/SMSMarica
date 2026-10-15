import 'package:agente/app/theme.dart';
import 'package:agente/features/ocorrencia/application/ocorrencias_controller.dart';
import 'package:agente/features/ocorrencia/domain/ocorrencia.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Abre a janela do REPORT. Tocar no botão sem querer não envia nada: o
/// envio só sai depois de escolher o tipo e tocar em "Enviar para a central".
Future<void> abrirReport(BuildContext context) => showModalBottomSheet<void>(
  context: context,
  isScrollControlled: true,
  backgroundColor: Colors.transparent,
  builder: (_) => const ReportSheet(),
);

/// Escolher o tipo → (texto) → enviar → resultado. Feita para o tablet em
/// paisagem, com botões grandes; rola quando o teclado sobe.
class ReportSheet extends ConsumerStatefulWidget {
  const ReportSheet({super.key});

  @override
  ConsumerState<ReportSheet> createState() => _ReportSheetState();
}

class _ReportSheetState extends ConsumerState<ReportSheet> {
  final _descricao = TextEditingController();
  TipoOcorrencia? _tipo;
  bool _enviando = false;
  ResultadoReport? _resultado;

  @override
  void initState() {
    super.initState();
    _descricao.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _descricao.dispose();
    super.dispose();
  }

  bool get _podeEnviar =>
      !_enviando &&
      _tipo != null &&
      (!_tipo!.exigeDescricao || _descricao.text.trim().isNotEmpty);

  Future<void> _enviar() async {
    final tipo = _tipo;
    if (tipo == null) return;
    setState(() => _enviando = true);
    final resultado = await ref
        .read(ocorrenciasProvider.notifier)
        .reportar(tipo, descricao: _descricao.text);
    if (!mounted) return;
    setState(() {
      _enviando = false;
      _resultado = resultado;
    });
  }

  @override
  Widget build(BuildContext context) {
    final teclado = MediaQuery.viewInsetsOf(context).bottom;
    return Padding(
      padding: EdgeInsets.only(bottom: teclado),
      child: Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
        ),
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 20),
        child: SafeArea(
          top: false,
          child: SingleChildScrollView(
            child: switch (_resultado) {
              final r? => _Resultado(resultado: r),
              null => _tipo == null ? _escolherTipo() : _detalhar(_tipo!),
            },
          ),
        ),
      ),
    );
  }

  Widget _escolherTipo() => Column(
    mainAxisSize: MainAxisSize.min,
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const _Titulo('O que aconteceu?'),
      const SizedBox(height: 16),
      Row(
        children: [
          for (final (i, t) in TipoOcorrencia.values.indexed) ...[
            if (i > 0) const SizedBox(width: 12),
            Expanded(
              child: _BotaoTipo(
                tipo: t,
                onTap: () => setState(() => _tipo = t),
              ),
            ),
          ],
        ],
      ),
      const SizedBox(height: 12),
      TextButton(
        onPressed: () => Navigator.of(context).pop(),
        child: const Text('Cancelar'),
      ),
    ],
  );

  Widget _detalhar(TipoOcorrencia tipo) => Column(
    mainAxisSize: MainAxisSize.min,
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Row(
        children: [
          Icon(tipo.icone, color: MaricaTheme.vermelho, size: 36),
          const SizedBox(width: 12),
          Expanded(child: _Titulo(tipo.rotulo)),
          TextButton(
            onPressed: _enviando ? null : () => setState(() => _tipo = null),
            child: const Text('Trocar'),
          ),
        ],
      ),
      const SizedBox(height: 12),
      TextField(
        controller: _descricao,
        maxLength: 500,
        minLines: 1,
        maxLines: 3,
        enabled: !_enviando,
        textInputAction: TextInputAction.done,
        decoration: InputDecoration(
          labelText: tipo.exigeDescricao
              ? 'Descreva o que aconteceu (obrigatório)'
              : 'Detalhes (opcional)',
          filled: true,
          fillColor: MaricaTheme.cinzaFundo,
        ),
      ),
      const SizedBox(height: 8),
      SizedBox(
        height: 64,
        child: ElevatedButton.icon(
          onPressed: _podeEnviar ? _enviar : null,
          icon: _enviando
              ? const SizedBox(
                  width: 22,
                  height: 22,
                  child: CircularProgressIndicator(
                    strokeWidth: 2.5,
                    color: Colors.white,
                  ),
                )
              : const Icon(Icons.campaign, size: 30),
          label: const Text(
            'ENVIAR PARA A CENTRAL',
            style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
          ),
        ),
      ),
      const SizedBox(height: 4),
      TextButton(
        onPressed: _enviando ? null : () => Navigator.of(context).pop(),
        child: const Text('Cancelar'),
      ),
    ],
  );
}

class _Titulo extends StatelessWidget {
  const _Titulo(this.texto);

  final String texto;

  @override
  Widget build(BuildContext context) => Text(
    texto,
    style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
  );
}

class _BotaoTipo extends StatelessWidget {
  const _BotaoTipo({required this.tipo, required this.onTap});

  final TipoOcorrencia tipo;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => SizedBox(
    height: 132,
    child: OutlinedButton(
      onPressed: onTap,
      style: OutlinedButton.styleFrom(
        foregroundColor: MaricaTheme.vermelho,
        side: const BorderSide(color: MaricaTheme.vermelho, width: 2),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        padding: const EdgeInsets.all(8),
      ),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(tipo.icone, size: 44),
          const SizedBox(height: 8),
          Text(
            tipo.rotulo,
            textAlign: TextAlign.center,
            style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700),
          ),
        ],
      ),
    ),
  );
}

class _Resultado extends StatelessWidget {
  const _Resultado({required this.resultado});

  final ResultadoReport resultado;

  @override
  Widget build(BuildContext context) {
    final recebida = resultado.recebidaEm?.toLocal();
    final (icone, cor, titulo, texto) = switch (resultado) {
      ResultadoReport(enviado: true) => (
        Icons.check_circle,
        Colors.green.shade700,
        'Central avisada',
        'Aviso recebido às ${_hora(recebida!)}.',
      ),
      ResultadoReport(semVinculo: true) => (
        Icons.link_off,
        Colors.orange.shade900,
        'Aviso guardado no tablet',
        'Este tablet não está vinculado a um veículo. Vincule o tablet para '
            'a central receber o aviso.',
      ),
      _ => (
        Icons.cloud_off,
        Colors.orange.shade900,
        'Sem conexão agora',
        'O aviso ficou guardado e sai sozinho assim que a rede voltar.',
      ),
    };
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icone, color: cor, size: 64),
        const SizedBox(height: 8),
        Text(
          titulo,
          style: TextStyle(
            fontSize: 22,
            fontWeight: FontWeight.w800,
            color: cor,
          ),
        ),
        const SizedBox(height: 6),
        Text(
          texto,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 16),
        ),
        const SizedBox(height: 16),
        SizedBox(
          width: 320,
          child: ElevatedButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Fechar'),
          ),
        ),
      ],
    );
  }

  static String _hora(DateTime t) =>
      '${t.hour.toString().padLeft(2, '0')}:'
      '${t.minute.toString().padLeft(2, '0')}';
}
