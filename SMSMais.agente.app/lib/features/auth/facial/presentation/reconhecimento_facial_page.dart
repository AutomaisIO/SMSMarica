import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/facial/data/servico_facial.dart';
import 'package:agente/features/auth/facial/presentation/captura_facial_page.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Cadastro e teste do reconhecimento facial — **só acessível autenticado**,
/// para associar o rosto ao motorista logado (associação justa). O cadastro
/// gera o template on-device (MobileFaceNet) e grava no store cifrado.
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
  String? _mensagem;

  @override
  void initState() {
    super.initState();
    _inicializar();
  }

  Future<void> _inicializar() async {
    final servico = ref.read(servicoFacialProvider);
    await servico.carregar();
    final sessao = ref.read(sessaoProvider);
    final cadastrado = sessao != null &&
        await servico.estaCadastrado(sessao.motoristaId);
    if (!mounted) return;
    setState(() {
      _cadastrado = cadastrado;
      _carregando = false;
    });
  }

  Future<CapturaFacialResultado?> _capturar(FinalidadeCaptura f) {
    return Navigator.of(context).push<CapturaFacialResultado>(
      MaterialPageRoute(builder: (_) => CapturaFacialPage(finalidade: f)),
    );
  }

  Future<void> _cadastrar() async {
    final servico = ref.read(servicoFacialProvider);
    final sessao = ref.read(sessaoProvider);
    if (sessao == null) return;
    if (!servico.matchDisponivel) {
      setState(() => _mensagem = 'Modelo facial ainda carregando…');
      return;
    }
    final captura = await _capturar(FinalidadeCaptura.cadastro);
    if (captura == null || !mounted) return;

    setState(() => _carregando = true);
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
      _mensagem = ok ? 'Rosto cadastrado e associado a você' : 'Rosto ausente';
    });
  }

  Future<void> _testar() async {
    final servico = ref.read(servicoFacialProvider);
    final sessao = ref.read(sessaoProvider);
    if (sessao == null) return;
    final captura = await _capturar(FinalidadeCaptura.login);
    if (captura == null || !mounted) return;

    setState(() => _carregando = true);
    final r = await servico.verificar(
      caminhoFoto: captura.caminhoFoto,
      motoristaId: sessao.motoristaId,
    );
    if (!mounted) return;
    setState(() {
      _carregando = false;
      _mensagem = '${r.motivo} (score ${r.score.toStringAsFixed(2)})';
    });
  }

  @override
  Widget build(BuildContext context) {
    final sessao = ref.watch(sessaoProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Reconhecimento facial')),
      body: _carregando
          ? const Center(child: CircularProgressIndicator())
          : Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Icon(
                    _cadastrado ? Icons.verified_user : Icons.face,
                    size: 64,
                    color: MaricaTheme.vermelho,
                  ),
                  const SizedBox(height: 16),
                  Text(
                    sessao?.nome ?? '',
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    _cadastrado
                        ? 'Rosto cadastrado neste tablet'
                        : 'Nenhum rosto cadastrado ainda',
                    textAlign: TextAlign.center,
                    style: const TextStyle(color: MaricaTheme.cinzaTexto),
                  ),
                  const SizedBox(height: 32),
                  ElevatedButton.icon(
                    onPressed: _cadastrar,
                    icon: const Icon(Icons.face_retouching_natural),
                    label: Text(
                      _cadastrado
                          ? 'Atualizar meu rosto'
                          : 'Cadastrar meu rosto',
                    ),
                  ),
                  const SizedBox(height: 12),
                  OutlinedButton.icon(
                    onPressed: _cadastrado ? _testar : null,
                    icon: const Icon(Icons.verified),
                    label: const Text('Testar reconhecimento'),
                  ),
                  if (_mensagem != null) ...[
                    const SizedBox(height: 24),
                    Text(
                      _mensagem!,
                      textAlign: TextAlign.center,
                      style: const TextStyle(fontWeight: FontWeight.w500),
                    ),
                  ],
                ],
              ),
            ),
    );
  }
}
