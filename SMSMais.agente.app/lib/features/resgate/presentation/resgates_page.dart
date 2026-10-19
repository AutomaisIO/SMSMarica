import 'dart:async';

import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/data/motorista_repository.dart';
import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/resgate/data/resgate_repository.dart';
import 'package:agente/features/resgate/domain/resgate.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// "Pacientes disponíveis para resgate": a mesma lista para todos os carros.
/// O motorista marca quem vai buscar — mesmo quem não levou —, e a central
/// só aceita se o paciente e os acompanhantes couberem no carro agora. Quem
/// já está esperando no local vem primeiro. Fecha devolvendo `true` quando
/// a marcação mudou (a navegação recarrega a rota).
class ResgatesPage extends ConsumerStatefulWidget {
  const ResgatesPage({
    required this.rotaId,
    this.aoDesembarcar = false,
    super.key,
  });

  final String rotaId;

  /// Aberta logo depois do desembarque na unidade.
  final bool aoDesembarcar;

  @override
  ConsumerState<ResgatesPage> createState() => _ResgatesPageState();
}

class _ResgatesPageState extends ConsumerState<ResgatesPage> {
  static const _intervalo = Duration(seconds: 20);

  ResgatesDaRota? _dados;
  String? _erro;
  bool _enviando = false;
  bool _mudou = false;
  final Set<String> _escolhidos = {};
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    unawaited(_carregar());
    // A lista muda sozinha: outros carros marcam, pacientes avisam.
    _timer = Timer.periodic(_intervalo, (_) => unawaited(_carregar()));
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  String? get _token => ref.read(dispositivoProvider).token;

  Future<void> _carregar() async {
    final token = _token;
    if (token == null) {
      setState(() => _erro = 'Tablet sem vínculo com um veículo.');
      return;
    }
    try {
      final dados = await ref
          .read(resgateRepositoryProvider)
          .listar(token, widget.rotaId);
      if (!mounted) return;
      setState(() {
        _dados = dados;
        _escolhidos.removeWhere(
          (id) => dados.disponiveis.every((d) => d.sessaoId != id),
        );
      });
    } on Object catch (e) {
      if (mounted) setState(() => _erro = _mensagem(e));
    }
  }

  Future<void> _executar(
    Future<void> Function(String token) acao, {
    String? feito,
  }) async {
    final token = _token;
    if (token == null || _enviando) return;
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      await acao(token);
      _mudou = true;
      if (feito != null && mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(feito)));
      }
    } on Object catch (e) {
      if (mounted) setState(() => _erro = _mensagem(e));
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
    await _carregar();
  }

  Future<void> _marcar() async {
    final ids = [..._escolhidos];
    await _executar((token) async {
      await ref
          .read(resgateRepositoryProvider)
          .marcar(
            token,
            widget.rotaId,
            ids,
            motoristaId: ref.read(sessaoProvider)?.motoristaId,
          );
      _escolhidos.clear();
    }, feito: 'Marcado. A busca e a entrega em casa entram na navegação.');
  }

  static String _mensagem(Object e) => e is RecusaDaCentral
      ? e.mensagem
      : 'Sem conexão com a central. A lista de resgate precisa de rede: '
            'tente de novo em instantes.';

  void _fechar() => Navigator.of(context).pop(_mudou);

  @override
  Widget build(BuildContext context) {
    final dados = _dados;
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (podeSair, _) {
        if (!podeSair) _fechar();
      },
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Pacientes disponíveis para resgate'),
          leading: IconButton(
            icon: const Icon(Icons.arrow_back),
            onPressed: _fechar,
          ),
          actions: [
            IconButton(
              icon: const Icon(Icons.refresh),
              tooltip: 'Atualizar',
              onPressed: _carregar,
            ),
          ],
        ),
        // Tablet em paisagem: a lista à esquerda e, à direita, os lugares
        // livres, quem este carro já marcou e o botão de marcar.
        body: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Expanded(child: _lista(dados)),
            const VerticalDivider(width: 1),
            SizedBox(width: 340, child: _painel(dados)),
          ],
        ),
      ),
    );
  }

  Widget _lista(ResgatesDaRota? dados) {
    if (dados == null && _erro == null) {
      return const Center(child: CircularProgressIndicator());
    }
    final itens = dados?.disponiveis ?? const <ResgateDisponivel>[];
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (widget.aoDesembarcar)
          const Padding(
            padding: EdgeInsets.only(bottom: 12),
            child: Text(
              'Desembarque feito. Quer resgatar alguém daqui ou de outra '
              'unidade? Marque e a navegação segue para a busca.',
              style: TextStyle(fontSize: 15),
            ),
          ),
        if (_erro != null)
          Container(
            margin: const EdgeInsets.only(bottom: 12),
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: Colors.red.shade50,
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: Colors.red.shade200),
            ),
            child: Text(
              _erro!,
              style: TextStyle(color: Colors.red.shade800, fontSize: 15),
            ),
          ),
        if (dados != null && itens.isEmpty)
          const Padding(
            padding: EdgeInsets.all(32),
            child: Text(
              'Ninguém esperando resgate agora.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 16, color: Colors.black54),
            ),
          ),
        for (final p in itens)
          _CartaoDisponivel(
            paciente: p,
            escolhido: _escolhidos.contains(p.sessaoId),
            ocupado: _enviando,
            aoEscolher: (sim) => setState(() {
              if (sim) {
                _escolhidos.add(p.sessaoId);
              } else {
                _escolhidos.remove(p.sessaoId);
              }
            }),
            aoAvisarNoLocal: () => _executar(
              (token) => ref
                  .read(resgateRepositoryProvider)
                  .avisarNoLocal(token, p.sessaoId),
              feito: 'Avisado: ${p.pacienteNome} está esperando no local.',
            ),
          ),
      ],
    );
  }

  Widget _painel(ResgatesDaRota? dados) {
    final livres = dados?.lugaresLivres;
    final cadeirante = dados?.lugaresCadeiranteLivres ?? 0;
    final marcados = dados?.marcados ?? const <ResgateMarcado>[];
    final n = _escolhidos.length;
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Lugares livres agora',
            style: TextStyle(fontSize: 13, color: Colors.black54),
          ),
          Text(
            livres == null ? '–' : '$livres',
            style: const TextStyle(fontSize: 34, fontWeight: FontWeight.w700),
          ),
          if (cadeirante > 0)
            Text(
              cadeirante == 1 ? '1 de cadeirante' : '$cadeirante de cadeirante',
              style: const TextStyle(fontSize: 13),
            ),
          const SizedBox(height: 16),
          const Text(
            'Marcados por você',
            style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
          ),
          const SizedBox(height: 6),
          Expanded(
            child: marcados.isEmpty
                ? const Text(
                    'Ninguém ainda.',
                    style: TextStyle(color: Colors.black54),
                  )
                : ListView(
                    children: [
                      for (final m in marcados)
                        ListTile(
                          contentPadding: EdgeInsets.zero,
                          title: Text(m.pacienteNome),
                          subtitle: Text(
                            m.acompanhantes == 0
                                ? m.unidadeNome
                                : '${m.unidadeNome} · +${m.acompanhantes}',
                          ),
                          trailing: TextButton(
                            onPressed: _enviando
                                ? null
                                : () => _executar(
                                    (token) => ref
                                        .read(resgateRepositoryProvider)
                                        .desmarcar(
                                          token,
                                          widget.rotaId,
                                          m.sessaoId,
                                        ),
                                    feito:
                                        '${m.pacienteNome} voltou para a '
                                        'lista.',
                                  ),
                            child: const Text('Desmarcar'),
                          ),
                        ),
                    ],
                  ),
          ),
          ElevatedButton.icon(
            onPressed: n == 0 || _enviando ? null : _marcar,
            icon: _enviando
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.check),
            label: Text(
              n <= 1 ? 'Marcar para resgate' : 'Marcar $n para resgate',
            ),
          ),
          const SizedBox(height: 8),
          OutlinedButton(
            style: OutlinedButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
            onPressed: _fechar,
            child: const Text('Voltar'),
          ),
        ],
      ),
    );
  }
}

class _CartaoDisponivel extends StatelessWidget {
  const _CartaoDisponivel({
    required this.paciente,
    required this.escolhido,
    required this.ocupado,
    required this.aoEscolher,
    required this.aoAvisarNoLocal,
  });

  final ResgateDisponivel paciente;
  final bool escolhido;
  final bool ocupado;
  final ValueChanged<bool> aoEscolher;
  final VoidCallback aoAvisarNoLocal;

  @override
  Widget build(BuildContext context) {
    final p = paciente;
    final naoCabe = p.cabe == false;
    final (fundo, texto) = switch (p.disponibilidade) {
      DisponibilidadeResgate.noLocal => (
        Colors.green.shade50,
        Colors.green.shade800,
      ),
      DisponibilidadeResgate.pelaPrevisao => (
        Colors.amber.shade50,
        Colors.amber.shade900,
      ),
      _ => (MaricaTheme.cinzaFundo, Colors.black87),
    };
    final linha = [
      if (p.foraDoMunicipio) 'fora do município',
      if (p.quemAvisou != null) p.quemAvisou!,
      if (p.distanciaMetros != null) _distancia(p.distanciaMetros!),
      if (p.veiculoDaIda != null) 'levado por ${p.veiculoDaIda}',
    ].join(' · ');
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: InkWell(
        onTap: naoCabe || ocupado ? null : () => aoEscolher(!escolhido),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              Checkbox(
                value: escolhido,
                onChanged: naoCabe || ocupado
                    ? null
                    : (v) => aoEscolher(v ?? false),
              ),
              const SizedBox(width: 6),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Paciente · unidade, e o tempo aguardando em destaque.
                    Row(
                      children: [
                        Flexible(
                          child: Text.rich(
                            TextSpan(
                              children: [
                                TextSpan(
                                  text: p.pacienteNome,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w700,
                                    fontSize: 17,
                                  ),
                                ),
                                const TextSpan(text: '  ·  '),
                                TextSpan(
                                  text: p.unidadeNome,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w600,
                                    fontSize: 15,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                        if (p.cadeirante) ...[
                          const SizedBox(width: 6),
                          const Icon(Icons.accessible, size: 20),
                        ],
                        if (p.acompanhantes > 0) ...[
                          const SizedBox(width: 6),
                          const Icon(Icons.people, size: 18),
                          Text(' +${p.acompanhantes}'),
                        ],
                        if (p.isolamento) ...[
                          const SizedBox(width: 6),
                          const Icon(Icons.shield, size: 18),
                        ],
                      ],
                    ),
                    if (linha.isNotEmpty) ...[
                      const SizedBox(height: 2),
                      Text(
                        linha,
                        style: const TextStyle(
                          fontSize: 13,
                          color: Colors.black54,
                        ),
                      ),
                    ],
                    if (p.aindaEmAtendimentoEm != null &&
                        p.disponibilidade != DisponibilidadeResgate.noLocal)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          'Disse às ${hora(p.aindaEmAtendimentoEm!)} que '
                          'ainda não terminou.',
                          style: const TextStyle(fontSize: 13),
                        ),
                      ),
                    if (p.naoEstavaNoLocalEm != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          'Não estava no local às '
                          '${hora(p.naoEstavaNoLocalEm!)}.',
                          style: TextStyle(
                            fontSize: 13,
                            color: Colors.amber.shade900,
                          ),
                        ),
                      ),
                    if (naoCabe && p.motivoNaoCabe != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(
                          p.motivoNaoCabe!,
                          style: TextStyle(
                            fontSize: 13,
                            color: Colors.red.shade800,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 12,
                  vertical: 6,
                ),
                decoration: BoxDecoration(
                  color: fundo,
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Text(
                  p.textoDisponibilidade,
                  style: TextStyle(
                    color: texto,
                    fontWeight: FontWeight.w700,
                    fontSize: 15,
                  ),
                ),
              ),
              if (p.disponibilidade != DisponibilidadeResgate.noLocal) ...[
                const SizedBox(width: 8),
                OutlinedButton(
                  onPressed: ocupado ? null : aoAvisarNoLocal,
                  child: const Text('Está aqui no local'),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  static String _distancia(int m) => m < 1000
      ? '$m m'
      : '${(m / 1000).toStringAsFixed(1).replaceAll('.', ',')} km';
}
