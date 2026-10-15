import 'dart:async';

import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/application/entrada_motorista_service.dart';
import 'package:agente/features/auth/data/motorista_repository.dart';
import 'package:agente/features/auth/domain/entrada_motorista.dart';
import 'package:agente/features/auth/facial/data/servico_facial.dart';
import 'package:agente/features/auth/facial/presentation/captura_facial_page.dart';
import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:agente/features/ocorrencia/presentation/botao_report.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:agente/shared/gps/gps_ao_vivo.dart';
import 'package:agente/shared/permissoes/permissoes_localizacao.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

/// Etapas da tela de entrada.
enum _Passo { inicio, cpf, codigoWhats, termo, codigoCentral, ocupado }

/// Entrada do motorista no tablet (ADR-0073). Só entra quem está escalado
/// hoje neste veículo: pelo rosto; no primeiro acesso, CPF + código no
/// WhatsApp + termo + rosto; sem reconhecimento, código da central.
///
/// O REPORT fica disponível aqui também: um acidente não espera o login.
class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _cpf = TextEditingController();
  final _codigo = TextEditingController();

  _Passo _passo = _Passo.inicio;
  String? _ocupadoCom;
  EscalaDoDia? _escala;
  bool _carregandoEscala = false;
  String? _mensagem;
  CodigoEmitido? _emitido;
  PrimeiroAcessoLiberado? _liberado;
  int _falhasRosto = 0;
  String? _tokenCarregado;

  @override
  void dispose() {
    _cpf.dispose();
    _codigo.dispose();
    super.dispose();
  }

  EntradaMotoristaService get _servico =>
      ref.read(entradaMotoristaServiceProvider);

  String? get _token => ref.read(dispositivoProvider).token;

  Posicao? get _posicao {
    final p = ref.read(gpsAoVivoProvider);
    if (p == null) return null;
    return (
      latitude: p.latitude,
      longitude: p.longitude,
      precisaoM: p.accuracy > 0 ? p.accuracy : null,
    );
  }

  Future<void> _carregarEscala() async {
    final token = _token;
    if (token == null) return;
    setState(() => _carregandoEscala = true);
    try {
      final escala = await _servico.carregarEscala(token);
      if (mounted) setState(() => _escala = escala);
    } on TokenDispositivoInvalido {
      await _desvinculado();
    } on Object catch (_) {
      _avisar('Não consegui buscar a escala de hoje. Tente de novo.');
    } finally {
      if (mounted) setState(() => _carregandoEscala = false);
    }
  }

  /// Token recusado pelo servidor: o vínculo deste tablet acabou. Apaga o
  /// local para a tela oferecer "Vincular veículo" de novo.
  Future<void> _desvinculado() async {
    _escala = null;
    _tokenCarregado = null;
    await ref
        .read(dispositivoProvider.notifier)
        .desvincularLocal(
          'Este tablet foi desligado do veículo. Vincule de novo.',
        );
  }

  void _avisar(String? m) {
    if (mounted) setState(() => _mensagem = m);
  }

  void _ir(_Passo p, {String? mensagem}) => setState(() {
    _passo = p;
    _mensagem = mensagem;
  });

  Future<T?> _ocupado<T>(String texto, Future<T> Function() acao) async {
    final voltar = _passo;
    setState(() {
      _passo = _Passo.ocupado;
      _ocupadoCom = texto;
      _mensagem = null;
    });
    try {
      return await acao();
    } finally {
      if (mounted && _passo == _Passo.ocupado) setState(() => _passo = voltar);
    }
  }

  Future<void> _entrou(
    String motoristaId,
    String nome,
    MeioEntrada meio,
  ) async {
    await ref
        .read(sessaoProvider.notifier)
        .entrar(
          Sessao(
            motoristaId: motoristaId,
            nome: nome,
            meio: meio,
            entrouEm: DateTime.now().toUtc(),
          ),
        );
    unawaited(ref.read(conferenciasProvider.notifier).enviarPendentes());
    await const PermissoesLocalizacao().solicitarForeground();
    if (mounted) context.go('/deslocamento');
  }

  Future<CapturaFacialResultado?> _capturar(FinalidadeCaptura f) =>
      Navigator.of(context).push<CapturaFacialResultado>(
        MaterialPageRoute(builder: (_) => CapturaFacialPage(finalidade: f)),
      );

  // ---------------------------------------------------------------- rosto

  Future<void> _entrarPeloRosto() async {
    final escala = _escala;
    if (escala == null) return;
    final captura = await _capturar(FinalidadeCaptura.login);
    if (captura == null || !mounted) return;
    final r = await _ocupado('Conferindo…', () async {
      return _servico.conferirRosto(
        escala: escala,
        caminhoFoto: captura.caminhoFoto,
        momento: MomentoConferencia.login,
        posicao: _posicao,
      );
    });
    if (r == null || !mounted) return;
    unawaited(ref.read(conferenciasProvider.notifier).enviarPendentes());
    final quem = escala.porId(r.motoristaId);
    if (r.aprovado && quem != null) {
      await _entrou(quem.motoristaId, quem.nome, MeioEntrada.rosto);
      return;
    }
    _falhasRosto++;
    _avisar(
      _falhasRosto >= 3
          ? 'Não reconheci você. Peça à central um código para entrar.'
          : 'Não reconheci você. Tente de novo, de frente e com boa luz.',
    );
  }

  // ------------------------------------------------------- primeiro acesso

  Future<void> _pedirCodigo() async {
    final token = _token;
    final cpf = _cpf.text.trim();
    if (token == null) return;
    if (cpf.length != 11) {
      _avisar('O CPF tem 11 números.');
      return;
    }
    try {
      final e = await _ocupado(
        'Mandando o código…',
        () => _servico.pedirCodigo(token, cpf),
      );
      if (e == null || !mounted) return;
      if (!e.enviado) {
        _avisar(_motivoSemCodigo(e.situacao));
        return;
      }
      _emitido = e;
      _codigo.clear();
      _ir(_Passo.codigoWhats);
    } on Object catch (erro) {
      _avisar(_tratar(erro));
    }
  }

  Future<void> _confirmarCodigo() async {
    final token = _token;
    if (token == null) return;
    try {
      final l = await _ocupado(
        'Conferindo o código…',
        () => _servico.confirmarCodigo(token, _cpf.text.trim(), _codigo.text),
      );
      if (l == null || !mounted) return;
      _liberado = l;
      _ir(_Passo.termo);
    } on Object catch (erro) {
      _avisar(_tratar(erro));
    }
  }

  Future<void> _aceitarTermo() async {
    final token = _token;
    final liberado = _liberado;
    if (token == null || liberado == null) return;
    final captura = await _capturar(FinalidadeCaptura.cadastro);
    if (captura == null || !mounted) return;
    try {
      await _ocupado('Cadastrando seu rosto…', () async {
        await _servico.concluirPrimeiroAcesso(
          token,
          liberado,
          captura.caminhoFoto,
          posicao: _posicao,
        );
      });
      if (!mounted) return;
      await _entrou(
        liberado.motoristaId,
        liberado.nome,
        MeioEntrada.primeiroAcesso,
      );
    } on SemRostoNaFoto {
      _avisar(
        'Não achei seu rosto na foto. Toque em "Concordo" e tente de novo.',
      );
    } on Object catch (erro) {
      _avisar(_tratar(erro));
    }
  }

  // ------------------------------------------------------- código da central

  Future<void> _entrarComCodigoDaCentral() async {
    final token = _token;
    if (token == null) return;
    try {
      final m = await _ocupado(
        'Conferindo o código…',
        () => _servico.entrarComCodigoDaCentral(
          token,
          _codigo.text,
          posicao: _posicao,
        ),
      );
      if (m == null || !mounted) return;
      await _entrou(m.motoristaId, m.nome, MeioEntrada.codigoCentral);
    } on Object catch (erro) {
      _avisar(_tratar(erro));
    }
  }

  static String _motivoSemCodigo(String? situacao) => switch (situacao) {
    'nao-escalado' =>
      'Este CPF não está escalado hoje neste veículo. Confira o CPF ou fale '
          'com a central.',
    'ja-cadastrado' =>
      'Seu rosto já está cadastrado: use "Entrar pelo rosto". Para refazer o '
          'cadastro, peça à central para apagar o atual.',
    'sem-telefone' =>
      'Seu cadastro não tem celular para receber o código. Peça à central um '
          'código para entrar.',
    _ => 'Não consegui mandar o código. Fale com a central.',
  };

  /// Mensagem do erro; 401 também apaga o vínculo local.
  String _tratar(Object erro) {
    if (erro is TokenDispositivoInvalido) unawaited(_desvinculado());
    return _explicar(erro);
  }

  static String _explicar(Object erro) => switch (erro) {
    RecusaDaCentral(:final mensagem) => mensagem,
    TokenDispositivoInvalido() =>
      'Este tablet foi desligado do veículo. Vincule de novo.',
    _ => 'Sem conexão com a central agora. Tente de novo.',
  };

  // ---------------------------------------------------------------- tela

  @override
  Widget build(BuildContext context) {
    final dispositivo = ref.watch(dispositivoProvider);
    // Vínculo apareceu (ou trocou): busca a escala desse veículo.
    if (dispositivo.token != null && dispositivo.token != _tokenCarregado) {
      _tokenCarregado = dispositivo.token;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) unawaited(_carregarEscala());
      });
    }

    return Scaffold(
      body: SafeArea(
        child: Stack(
          children: [
            LayoutBuilder(
              builder: (context, area) {
                final paisagem =
                    area.maxWidth > area.maxHeight && area.maxWidth >= 720;
                final esquerda = _Lado(
                  escala: _escala,
                  vinculado: dispositivo.vinculado,
                );
                final direita = _acoes(dispositivo);
                return Center(
                  child: SingleChildScrollView(
                    padding: const EdgeInsets.fromLTRB(32, 24, 32, 24),
                    child: ConstrainedBox(
                      constraints: BoxConstraints(
                        maxWidth: paisagem ? 900 : 480,
                      ),
                      child: paisagem
                          ? Row(
                              children: [
                                Expanded(child: esquerda),
                                const SizedBox(width: 48),
                                Expanded(child: direita),
                              ],
                            )
                          : Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                esquerda,
                                const SizedBox(height: 32),
                                direita,
                              ],
                            ),
                    ),
                  ),
                );
              },
            ),
            const Positioned(right: 12, top: 8, child: BotaoReport()),
          ],
        ),
      ),
    );
  }

  Widget _acoes(EstadoDispositivo dispositivo) {
    if (dispositivo.carregando) {
      return const Center(child: CircularProgressIndicator());
    }
    if (!dispositivo.vinculado) {
      return _Coluna(
        mensagem: dispositivo.aviso,
        children: [
          const Text(
            'Este tablet ainda não está ligado a um veículo. A central gera '
            'o código de vínculo no painel, na tela do veículo.',
            style: TextStyle(fontSize: 16),
          ),
          const SizedBox(height: 24),
          ElevatedButton.icon(
            onPressed: () => context.push('/vincular'),
            icon: const Icon(Icons.link),
            label: const Text('Vincular veículo'),
          ),
        ],
      );
    }
    return switch (_passo) {
      _Passo.ocupado => _Coluna(
        children: [
          const Center(child: CircularProgressIndicator()),
          const SizedBox(height: 16),
          Text(
            _ocupadoCom ?? '',
            textAlign: TextAlign.center,
            style: const TextStyle(fontSize: 18),
          ),
        ],
      ),
      _Passo.inicio => _inicio(),
      _Passo.cpf => _Coluna(
        mensagem: _mensagem,
        children: [
          const _Titulo('Primeiro acesso'),
          const Text(
            'Digite o seu CPF. Vamos mandar um código para o WhatsApp do seu '
            'cadastro.',
          ),
          const SizedBox(height: 16),
          _CampoNumero(controller: _cpf, rotulo: 'CPF', digitos: 11),
          const SizedBox(height: 16),
          ElevatedButton(
            onPressed: _pedirCodigo,
            child: const Text('Receber código no WhatsApp'),
          ),
          TextButton(
            onPressed: () => _ir(_Passo.inicio),
            child: const Text('Voltar'),
          ),
        ],
      ),
      _Passo.codigoWhats => _Coluna(
        mensagem: _mensagem,
        children: [
          const _Titulo('Código do WhatsApp'),
          Text(
            _emitido?.canal == 'tela-teste'
                ? 'Modo de teste: o código é ${_emitido?.codigo}.'
                : 'Mandamos um código para o WhatsApp '
                      '${_emitido?.destinoMascarado ?? 'do seu cadastro'}.',
          ),
          const SizedBox(height: 16),
          _CampoNumero(controller: _codigo, rotulo: 'Código', digitos: 6),
          const SizedBox(height: 16),
          ElevatedButton(
            onPressed: _confirmarCodigo,
            child: const Text('Confirmar'),
          ),
          TextButton(
            onPressed: () => _ir(_Passo.cpf),
            child: const Text('Voltar'),
          ),
        ],
      ),
      _Passo.termo => _Coluna(
        mensagem: _mensagem,
        children: [
          Text(
            'Olá, ${_liberado?.nome ?? ''}',
            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 8),
          Container(
            constraints: const BoxConstraints(maxHeight: 220),
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              border: Border.all(color: MaricaTheme.cinzaTexto),
              borderRadius: BorderRadius.circular(8),
            ),
            child: SingleChildScrollView(
              child: Text(_liberado?.termoTexto ?? ''),
            ),
          ),
          const SizedBox(height: 16),
          ElevatedButton(
            onPressed: _aceitarTermo,
            child: const Text('Concordo — cadastrar meu rosto'),
          ),
          TextButton(
            onPressed: () => _ir(_Passo.inicio),
            child: const Text('Não concordo'),
          ),
        ],
      ),
      _Passo.codigoCentral => _Coluna(
        mensagem: _mensagem,
        children: [
          const _Titulo('Código da central'),
          const Text(
            'A central gera no painel um código de 6 números para este '
            'veículo. Ele vale por 15 minutos.',
          ),
          const SizedBox(height: 16),
          _CampoNumero(controller: _codigo, rotulo: 'Código', digitos: 6),
          const SizedBox(height: 16),
          ElevatedButton(
            onPressed: _entrarComCodigoDaCentral,
            child: const Text('Entrar'),
          ),
          TextButton(
            onPressed: () => _ir(_Passo.inicio),
            child: const Text('Voltar'),
          ),
        ],
      ),
    };
  }

  Widget _inicio() {
    final escala = _escala;
    if (_carregandoEscala && escala == null) {
      return const Center(child: CircularProgressIndicator());
    }
    final codigoCentral = TextButton.icon(
      onPressed: () {
        _codigo.clear();
        _ir(_Passo.codigoCentral);
      },
      icon: const Icon(Icons.pin_outlined),
      label: const Text('Tenho um código da central'),
    );
    if (escala == null || escala.motoristas.isEmpty) {
      return _Coluna(
        mensagem: _mensagem,
        children: [
          Text(
            escala == null
                ? 'Não consegui ver quem está escalado hoje.'
                : 'Ninguém está escalado hoje neste veículo. Peça à central '
                      'para escalar você ou um código para entrar.',
            style: const TextStyle(fontSize: 16),
          ),
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: _carregarEscala,
            icon: const Icon(Icons.refresh),
            label: const Text('Tentar de novo'),
          ),
          codigoCentral,
        ],
      );
    }
    return _Coluna(
      mensagem: _mensagem,
      children: [
        if (escala.algumComRosto)
          SizedBox(
            height: 72,
            child: ElevatedButton.icon(
              onPressed: _entrarPeloRosto,
              icon: const Icon(Icons.face, size: 32),
              label: const Text(
                'Entrar pelo rosto',
                style: TextStyle(fontSize: 20),
              ),
            ),
          ),
        if (escala.algumSemRosto) ...[
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: () {
              _cpf.clear();
              _ir(_Passo.cpf);
            },
            icon: const Icon(Icons.badge_outlined),
            label: const Text('Primeiro acesso (cadastrar meu rosto)'),
          ),
        ],
        const SizedBox(height: 8),
        codigoCentral,
      ],
    );
  }
}

/// Logo, título e quem está escalado hoje.
class _Lado extends StatelessWidget {
  const _Lado({required this.escala, required this.vinculado});

  final EscalaDoDia? escala;
  final bool vinculado;

  @override
  Widget build(BuildContext context) {
    final e = escala;
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.local_hospital, size: 64, color: MaricaTheme.vermelho),
        const SizedBox(height: 12),
        const Text(
          'SMS Maricá',
          textAlign: TextAlign.center,
          style: TextStyle(
            fontSize: 28,
            fontWeight: FontWeight.bold,
            color: MaricaTheme.vermelho,
          ),
        ),
        const Text(
          'Agente de transporte sanitário',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 14, color: MaricaTheme.cinzaTexto),
        ),
        if (vinculado && e != null && e.motoristas.isNotEmpty) ...[
          const SizedBox(height: 20),
          const Text(
            'Escalados hoje neste veículo',
            style: TextStyle(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 4),
          for (final m in e.motoristas)
            Text(
              m.temReferencia ? m.nome : '${m.nome} (primeiro acesso)',
              textAlign: TextAlign.center,
            ),
          if (!e.atualizada)
            const Padding(
              padding: EdgeInsets.only(top: 8),
              child: Text(
                'Sem conexão: usando a escala guardada de hoje.',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 12, color: MaricaTheme.cinzaTexto),
              ),
            ),
        ],
        const SizedBox(height: 8),
        Text(
          'Versão de teste — reconhecimento facial '
          '(limiar ${ServicoFacial.limiar.toStringAsFixed(2)})',
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 11, color: Colors.grey),
        ),
      ],
    );
  }
}

class _Coluna extends StatelessWidget {
  const _Coluna({required this.children, this.mensagem});

  final List<Widget> children;
  final String? mensagem;

  @override
  Widget build(BuildContext context) => Column(
    mainAxisSize: MainAxisSize.min,
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      ...children,
      if (mensagem != null) ...[
        const SizedBox(height: 16),
        Text(
          mensagem!,
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: MaricaTheme.vermelho,
            fontWeight: FontWeight.w600,
          ),
        ),
      ],
    ],
  );
}

class _Titulo extends StatelessWidget {
  const _Titulo(this.texto);

  final String texto;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Text(
      texto,
      style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
    ),
  );
}

class _CampoNumero extends StatelessWidget {
  const _CampoNumero({
    required this.controller,
    required this.rotulo,
    required this.digitos,
  });

  final TextEditingController controller;
  final String rotulo;
  final int digitos;

  @override
  Widget build(BuildContext context) => TextField(
    controller: controller,
    keyboardType: TextInputType.number,
    autofocus: true,
    style: const TextStyle(fontSize: 22, letterSpacing: 2),
    inputFormatters: [
      FilteringTextInputFormatter.digitsOnly,
      LengthLimitingTextInputFormatter(digitos),
    ],
    decoration: InputDecoration(labelText: rotulo),
  );
}
