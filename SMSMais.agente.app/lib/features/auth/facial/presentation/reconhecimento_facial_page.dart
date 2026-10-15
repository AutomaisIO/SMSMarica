import 'dart:async';

import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/facial/data/servico_facial.dart';
import 'package:agente/features/auth/facial/presentation/captura_facial_page.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Uma tentativa feita nesta tela (para calibrar o limiar no tablet).
class _Tentativa {
  const _Tentativa({
    required this.cadastro,
    required this.ok,
    required this.prova,
    required this.processamento,
    this.score,
  });

  final bool cadastro;
  final bool ok;
  final double? score;
  final Duration prova;
  final Duration processamento;
}

/// Cadastro e teste do reconhecimento facial — **só acessível autenticado**,
/// para associar o rosto ao motorista logado. O cadastro gera o template no
/// tablet (MobileFaceNet) e grava no armazenamento cifrado.
///
/// Também serve para calibrar no aparelho: mostra o diagnóstico da câmera e
/// guarda a nota e os tempos de cada tentativa.
class ReconhecimentoFacialPage extends ConsumerStatefulWidget {
  const ReconhecimentoFacialPage({super.key});

  @override
  ConsumerState<ReconhecimentoFacialPage> createState() =>
      _ReconhecimentoFacialPageState();
}

class _ReconhecimentoFacialPageState
    extends ConsumerState<ReconhecimentoFacialPage> {
  bool _carregando = true;
  bool _cadastrado = false;
  bool _modelo = false;
  String? _mensagem;
  final List<_Tentativa> _tentativas = [];

  @override
  void initState() {
    super.initState();
    unawaited(_inicializar());
  }

  Future<void> _inicializar() async {
    final servico = ref.read(servicoFacialProvider);
    await servico.carregar();
    final sessao = ref.read(sessaoProvider);
    final cadastrado =
        sessao != null && await servico.estaCadastrado(sessao.motoristaId);
    if (!mounted) return;
    setState(() {
      _modelo = servico.matchDisponivel;
      _cadastrado = cadastrado;
      _carregando = false;
    });
  }

  Future<CapturaFacialResultado?> _capturar(FinalidadeCaptura f) =>
      Navigator.of(context).push<CapturaFacialResultado>(
        MaterialPageRoute(
          builder: (_) => CapturaFacialPage(finalidade: f, diagnostico: true),
        ),
      );

  Future<void> _cadastrar() async {
    final servico = ref.read(servicoFacialProvider);
    final sessao = ref.read(sessaoProvider);
    if (sessao == null || !_modelo) return;
    final captura = await _capturar(FinalidadeCaptura.cadastro);
    if (captura == null || !mounted) return;

    setState(() => _carregando = true);
    final relogio = Stopwatch()..start();
    final ok = await servico.cadastrar(
      motoristaId: sessao.motoristaId,
      cpf: sessao.motoristaId,
      nome: sessao.nome,
      caminhoFoto: captura.caminhoFoto,
      fotoHash: 'local',
    );
    if (!mounted) return;
    setState(() {
      _cadastrado = _cadastrado || ok;
      _carregando = false;
      _mensagem = ok
          ? 'Rosto cadastrado neste tablet.'
          : 'Não achei um rosto só na foto. Tente de novo.';
      _tentativas.insert(
        0,
        _Tentativa(
          cadastro: true,
          ok: ok,
          prova: captura.duracaoProva,
          processamento: relogio.elapsed,
        ),
      );
    });
  }

  Future<void> _testar() async {
    final servico = ref.read(servicoFacialProvider);
    final sessao = ref.read(sessaoProvider);
    if (sessao == null) return;
    final captura = await _capturar(FinalidadeCaptura.login);
    if (captura == null || !mounted) return;

    setState(() => _carregando = true);
    final relogio = Stopwatch()..start();
    final r = await servico.verificar(
      caminhoFoto: captura.caminhoFoto,
      motoristaId: sessao.motoristaId,
    );
    if (!mounted) return;
    setState(() {
      _carregando = false;
      _mensagem = r.motivo;
      _tentativas.insert(
        0,
        _Tentativa(
          cadastro: false,
          ok: r.aprovado,
          score: r.score,
          prova: captura.duracaoProva,
          processamento: relogio.elapsed,
        ),
      );
    });
  }

  @override
  Widget build(BuildContext context) {
    final sessao = ref.watch(sessaoProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Reconhecimento facial')),
      body: _carregando
          ? const Center(child: CircularProgressIndicator())
          : LayoutBuilder(
              builder: (context, c) {
                final painel = _Painel(
                  nome: sessao?.nome ?? '',
                  modelo: _modelo,
                  cadastrado: _cadastrado,
                  mensagem: _mensagem,
                  onCadastrar: _modelo ? _cadastrar : null,
                  onTestar: _modelo && _cadastrado ? _testar : null,
                );
                final historico = _Historico(tentativas: _tentativas);
                if (c.maxWidth > c.maxHeight) {
                  return Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Expanded(flex: 2, child: painel),
                      const VerticalDivider(width: 1),
                      Expanded(flex: 3, child: historico),
                    ],
                  );
                }
                return Column(
                  children: [
                    painel,
                    const Divider(height: 1),
                    Expanded(child: historico),
                  ],
                );
              },
            ),
    );
  }
}

class _Painel extends StatelessWidget {
  const _Painel({
    required this.nome,
    required this.modelo,
    required this.cadastrado,
    required this.mensagem,
    required this.onCadastrar,
    required this.onTestar,
  });

  final String nome;
  final bool modelo;
  final bool cadastrado;
  final String? mensagem;
  final VoidCallback? onCadastrar;
  final VoidCallback? onTestar;

  @override
  Widget build(BuildContext context) => SingleChildScrollView(
    padding: const EdgeInsets.all(24),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Icon(
          cadastrado ? Icons.verified_user : Icons.face,
          size: 56,
          color: MaricaTheme.vermelho,
        ),
        const SizedBox(height: 12),
        Text(
          nome,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w600),
        ),
        const SizedBox(height: 4),
        Text(
          !modelo
              ? 'Modelo de reconhecimento não carregou neste tablet'
              : cadastrado
              ? 'Rosto cadastrado neste tablet'
              : 'Nenhum rosto cadastrado ainda',
          textAlign: TextAlign.center,
          style: const TextStyle(color: MaricaTheme.cinzaTexto),
        ),
        const SizedBox(height: 24),
        ElevatedButton.icon(
          onPressed: onCadastrar,
          icon: const Icon(Icons.face_retouching_natural),
          label: Text(
            cadastrado ? 'Atualizar meu rosto' : 'Cadastrar meu rosto',
          ),
        ),
        const SizedBox(height: 12),
        OutlinedButton.icon(
          onPressed: onTestar,
          icon: const Icon(Icons.verified),
          label: const Text('Testar reconhecimento'),
        ),
        if (mensagem != null) ...[
          const SizedBox(height: 16),
          Text(
            mensagem!,
            textAlign: TextAlign.center,
            style: const TextStyle(fontWeight: FontWeight.w500),
          ),
        ],
      ],
    ),
  );
}

class _Historico extends StatelessWidget {
  const _Historico({required this.tentativas});

  final List<_Tentativa> tentativas;

  static String _s(Duration d) =>
      '${(d.inMilliseconds / 1000).toStringAsFixed(1).replaceAll('.', ',')} s';

  @override
  Widget build(BuildContext context) => ListView(
    padding: const EdgeInsets.all(16),
    children: [
      Text(
        'Teste várias vezes com você (deve passar) e peça para outra pessoa '
        'testar no seu cadastro (não deve passar). Passa com nota a partir '
        'de ${ServicoFacial.limiar.toStringAsFixed(2).replaceAll('.', ',')}.',
        style: const TextStyle(color: MaricaTheme.cinzaTexto),
      ),
      const SizedBox(height: 8),
      for (final t in tentativas)
        ListTile(
          dense: true,
          leading: Icon(
            t.cadastro
                ? Icons.face_retouching_natural
                : t.ok
                ? Icons.check_circle
                : Icons.cancel,
            color: t.ok ? Colors.green.shade700 : MaricaTheme.vermelho,
          ),
          title: Text(
            t.cadastro
                ? (t.ok ? 'Cadastro feito' : 'Cadastro falhou')
                : 'Nota ${t.score!.toStringAsFixed(2).replaceAll('.', ',')}'
                      ' — ${t.ok ? 'passou' : 'não passou'}',
          ),
          subtitle: Text(
            'prova de vida ${_s(t.prova)} · '
            'reconhecimento ${_s(t.processamento)}',
          ),
        ),
    ],
  );
}
