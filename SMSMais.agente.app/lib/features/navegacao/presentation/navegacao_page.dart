import 'dart:async';

import 'package:agente/app/theme.dart';
import 'package:agente/features/navegacao/data/navegacao_repository.dart';
import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:agente/features/navegacao/presentation/widgets/confirmacao_parada_sheet.dart';
import 'package:agente/features/navegacao/presentation/widgets/painel_passageiro.dart';
import 'package:agente/shared/permissoes/permissoes_localizacao.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_navigation_flutter/google_navigation_flutter.dart';

/// Tela de navegação assistida do translado, com o Google Navigation SDK.
///
/// O mapa turn-by-turn ocupa a tela cheia; o **rodapé do SDK é desligado** para
/// dar lugar ao **painel compacto do passageiro** (nome, confirmação do
/// paciente e do acompanhante, recados, "Cheguei"), mantendo o cabeçalho de
/// manobras do SDK no topo. A rota é **uma só sessão** com as paradas como
/// waypoints; a cada chegada o app comuta para a confirmação (assentos) e
/// **continua a mesma sessão** para o próximo waypoint (1 rota = 1 cobrança).
class NavegacaoPage extends ConsumerStatefulWidget {
  const NavegacaoPage({super.key});

  @override
  ConsumerState<NavegacaoPage> createState() => _NavegacaoPageState();
}

class _NavegacaoPageState extends ConsumerState<NavegacaoPage> {
  StreamSubscription<OnArrivalEvent>? _arrivalSub;
  GoogleNavigationViewController? _viewController;
  Timer? _autoOcultarTimer;

  TransladoNavegacao? _translado;
  final Set<String> _concluidas = {};

  bool _sessaoPronta = false;
  bool _confirmando = false;
  bool _painelVisivel = true;
  bool _modoNoite = false;
  String? _erro;
  String _status = 'Preparando navegação…';

  /// Tempo que o painel do passageiro fica visível antes de se auto-ocultar.
  static const _tempoAutoOcultar = Duration(seconds: 9);

  @override
  void initState() {
    super.initState();
    unawaited(_preparar());
  }

  @override
  void dispose() {
    _autoOcultarTimer?.cancel();
    _arrivalSub?.cancel();
    try {
      GoogleMapsNavigator.cleanup();
    } on Object catch (_) {
      // Sessão pode não estar inicializada — ignorar.
    }
    super.dispose();
  }

  void _agendarAutoOcultar() {
    _autoOcultarTimer?.cancel();
    _autoOcultarTimer = Timer(_tempoAutoOcultar, () {
      if (mounted) {
        setState(() => _painelVisivel = false);
        unawaited(_aplicarPadding());
      }
    });
  }

  void _mostrarPainel() {
    setState(() => _painelVisivel = true);
    unawaited(_aplicarPadding());
    _agendarAutoOcultar();
  }

  void _ocultarPainel() {
    _autoOcultarTimer?.cancel();
    setState(() => _painelVisivel = false);
    unawaited(_aplicarPadding());
  }

  /// Inseta a câmera do SDK na área visível: o topo já é tratado pelo SafeArea
  /// da view; aqui reservamos a área do painel embaixo (a câmera mira acima
  /// dele). Reaplicado ao mostrar/ocultar o painel.
  Future<void> _aplicarPadding() async {
    if (!mounted) return;
    final bottom = _painelVisivel ? 200.0 : 60.0;
    try {
      await _viewController?.setPadding(EdgeInsets.only(bottom: bottom));
    } on Object catch (_) {}
  }

  Future<void> _recentrar() async {
    try {
      await _viewController?.followMyLocation(CameraPerspective.tilted);
    } on Object catch (_) {}
  }

  Future<void> _alternarNoite() async {
    final noite = !_modoNoite;
    setState(() => _modoNoite = noite);
    try {
      // Durante a navegação o esquema é controlado pelo modo noturno; o
      // setMapColorScheme sozinho é ignorado (auto segue o horário).
      await _viewController?.setForceNightMode(
        noite
            ? NavigationForceNightMode.forceNight
            : NavigationForceNightMode.forceDay,
      );
      await _viewController?.setMapColorScheme(
        noite ? MapColorScheme.dark : MapColorScheme.light,
      );
    } on Object catch (_) {}
  }

  List<ParadaNavegacao> get _paradas =>
      _translado?.paradas
          .map((p) => p.copyWith(concluida: _concluidas.contains(p.id)))
          .toList() ??
      const [];

  ParadaNavegacao? get _proxima {
    for (final p in _paradas) {
      if (!p.concluida) return p;
    }
    return null;
  }

  ParadaNavegacao? _paradaPorId(String id) {
    for (final p in _paradas) {
      if (p.id == id) return p;
    }
    return null;
  }

  Future<void> _preparar() async {
    try {
      final translado =
          await ref.read(navegacaoRepositoryProvider).obterTransladoAtual();
      if (!mounted) return;
      setState(() => _translado = translado);

      // 1) Termos de uso do Navigation SDK (exigência do Google).
      setState(() => _status = 'Verificando termos de uso…');
      final termsOk = await GoogleMapsNavigator.areTermsAccepted() ||
          await GoogleMapsNavigator.showTermsAndConditionsDialog(
            'SMS Maricá — Agente',
            'Prefeitura de Maricá',
          );
      if (!termsOk) {
        setState(() => _erro = 'Os termos do Navigation não foram aceitos.');
        return;
      }

      // 2) Permissão de localização.
      setState(() => _status = 'Permissão de localização…');
      final locOk = await const PermissoesLocalizacao().solicitarForeground();
      if (!locOk) {
        setState(() => _erro = 'Permissão de localização negada.');
        return;
      }

      // 3) Inicializa a sessão e ouve as chegadas.
      setState(() => _status = 'Iniciando sessão de navegação…');
      await GoogleMapsNavigator.initializeNavigationSession();
      _arrivalSub = GoogleMapsNavigator.setOnArrivalListener(_aoChegar);

      // 4) Lança a rota única (todas as paradas como waypoints) e guia.
      setState(() => _status = 'Calculando a rota…');
      await _definirRotaEGuide();

      if (!mounted) return;
      setState(() => _sessaoPronta = true);
    } on SessionInitializationException catch (e) {
      if (mounted) setState(() => _erro = _msgInit(e.code));
    } on Object catch (e) {
      if (mounted) setState(() => _erro = 'Falha ao preparar navegação: $e');
    }
  }

  String _msgInit(SessionInitializationError code) => switch (code) {
        SessionInitializationError.termsNotAccepted =>
          'Termos do Navigation não aceitos.',
        SessionInitializationError.locationPermissionMissing =>
          'Falta permissão de localização.',
        SessionInitializationError.notAuthorized =>
          'Chave de API ausente/sem autorização para o Navigation SDK.',
      };

  Future<void> _definirRotaEGuide() async {
    final pendentes = _paradas.where((p) => !p.concluida).toList();
    if (pendentes.isEmpty) return;

    final waypoints = pendentes
        .map(
          (p) => NavigationWaypoint.withLatLngTarget(
            title: p.id,
            target: LatLng(latitude: p.latitude, longitude: p.longitude),
          ),
        )
        .toList();

    final status = await GoogleMapsNavigator.setDestinations(
      Destinations(
        waypoints: waypoints,
        displayOptions: NavigationDisplayOptions(showDestinationMarkers: true),
        routingOptions: RoutingOptions(
          travelMode: NavigationTravelMode.driving,
        ),
      ),
    );

    if (status != NavigationRouteStatus.statusOk) {
      setState(() => _erro = 'Rota não calculada (${status.name}).');
      return;
    }
    await GoogleMapsNavigator.startGuidance();
  }

  void _aoChegar(OnArrivalEvent event) {
    final parada = _paradaPorId(event.waypoint.title);
    if (parada != null) {
      _mostrarPainel();
      unawaited(_confirmarParada(parada));
    }
  }

  Future<void> _confirmarParada(ParadaNavegacao parada) async {
    if (_confirmando || _concluidas.contains(parada.id)) return;
    setState(() => _confirmando = true);

    // Pausa a guidance enquanto o motorista confirma (mantém a sessão viva).
    try {
      await GoogleMapsNavigator.stopGuidance();
    } on Object catch (_) {}

    if (!mounted) return;
    final ok = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => ConfirmacaoParadaSheet(parada: parada),
    );

    if (!mounted) return;

    if (ok ?? false) {
      setState(() => _concluidas.add(parada.id));
      await _seguirProxima();
    } else {
      // Retoma a guidance na mesma parada.
      try {
        await GoogleMapsNavigator.startGuidance();
      } on Object catch (_) {}
    }
    if (mounted) setState(() => _confirmando = false);
  }

  Future<void> _seguirProxima() async {
    if (_proxima == null) {
      // Fim do translado: encerra guidance e limpa destinos (mesma sessão).
      try {
        await GoogleMapsNavigator.stopGuidance();
        await GoogleMapsNavigator.clearDestinations();
      } on Object catch (_) {}
      return;
    }
    // Mesma sessão: avança ao próximo waypoint, sem recriar (sem nova cobra).
    try {
      final resp = await GoogleMapsNavigator.continueToNextDestination();
      if (resp.waypoint != null) {
        await GoogleMapsNavigator.startGuidance();
      }
    } on Object catch (_) {
      // Fallback raro: redefine a rota com as paradas restantes (1 sessão).
      await _definirRotaEGuide();
    }
  }

  Future<void> _onViewCreated(GoogleNavigationViewController controller) async {
    _viewController = controller;
    try {
      await controller.setMyLocationEnabled(true);
      await controller.setNavigationUIEnabled(true);
      // Desliga o rodapé do SDK p/ dar lugar ao painel do passageiro.
      await controller.setNavigationFooterEnabled(false);
      await controller.setForceNightMode(
        _modoNoite
            ? NavigationForceNightMode.forceNight
            : NavigationForceNightMode.forceDay,
      );
      await controller.setMapColorScheme(
        _modoNoite ? MapColorScheme.dark : MapColorScheme.light,
      );
      await controller.followMyLocation(CameraPerspective.tilted);
    } on Object catch (_) {
      // View pode ter sido descartada durante o await — ignorar.
    }
    await _aplicarPadding();
    // Em alguns devices a surface nasce com tamanho transitório e o conteúdo
    // fica deslocado; reaplicar câmera/padding após assentar corrige.
    Future<void>.delayed(const Duration(milliseconds: 800), () async {
      if (!mounted) return;
      try {
        await _viewController?.followMyLocation(CameraPerspective.tilted);
      } on Object catch (_) {}
      await _aplicarPadding();
    });
    _agendarAutoOcultar();
  }

  /// Simula o trajeto para testar sem dirigir (tablet parado). Apenas teste.
  Future<void> _simularTrajeto() async {
    try {
      await GoogleMapsNavigator.simulator
          .simulateLocationsAlongExistingRouteWithOptions(
        SimulationOptions(speedMultiplier: 8),
      );
    } on Object catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Não foi possível simular: $e')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_erro != null) return _telaErro(_erro!);
    if (_translado == null || !_sessaoPronta) return _telaCarregando();

    final proxima = _proxima;
    final cam = proxima ?? _paradas.first;

    return Scaffold(
      body: Stack(
        // Sem isso o Stack encolhe para a altura do maior filho não
        // posicionado; expand garante que o mapa ocupe a tela inteira.
        fit: StackFit.expand,
        children: [
          // SafeArea para a UI do SDK (cabeçalho de manobra) não ficar atrás do
          // status bar/relógio. A própria view é insetada.
          SafeArea(
            child: GoogleMapsNavigationView(
              onViewCreated: _onViewCreated,
              initialCameraPosition: CameraPosition(
                target: LatLng(
                  latitude: cam.latitude,
                  longitude: cam.longitude,
                ),
                zoom: 14,
              ),
            ),
          ),

          // Voltar (topo-esquerda).
          Positioned(
            top: 0,
            left: 0,
            child: SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: _FabRedondo(
                  icone: Icons.arrow_back,
                  onTap: () => Navigator.of(context).maybePop(),
                ),
              ),
            ),
          ),

          // Coluna de ações (direita, centralizada na vertical) — fica abaixo
          // da bússola do SDK para não competir o toque com ela.
          Positioned(
            top: 0,
            bottom: 0,
            right: 0,
            child: SafeArea(
              child: Padding(
                padding: const EdgeInsets.only(right: 8),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    _FabRedondo(
                      icone: _modoNoite ? Icons.light_mode : Icons.dark_mode,
                      onTap: _alternarNoite,
                    ),
                    const SizedBox(height: 12),
                    _FabRedondo(
                      icone: Icons.my_location,
                      onTap: _recentrar,
                    ),
                    const SizedBox(height: 12),
                    _FabRedondo(
                      icone: Icons.fast_forward,
                      rotulo: 'Simular',
                      onTap: _simularTrajeto,
                    ),
                  ],
                ),
              ),
            ),
          ),

          // Painel do passageiro (rodapé) — auto-oculta; reabre pelo FAB.
          if (proxima == null)
            Positioned(
              left: 8,
              right: 8,
              bottom: 8,
              child: SafeArea(
                child: _TransladoConcluido(
                  onVoltar: () => Navigator.of(context).maybePop(),
                ),
              ),
            )
          else if (_painelVisivel)
            Positioned(
              left: 8,
              right: 8,
              bottom: 8,
              child: SafeArea(
                child: PainelPassageiro(
                  parada: proxima,
                  onCheguei: () => _confirmarParada(proxima),
                  onOcultar: _ocultarPainel,
                ),
              ),
            )
          else
            Positioned(
              left: 0,
              right: 0,
              bottom: 8,
              child: SafeArea(
                child: Center(
                  child: _FabPassageiro(
                    nome: proxima.rotulo,
                    onTap: _mostrarPainel,
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _telaCarregando() => Scaffold(
        appBar: AppBar(title: const Text('Navegação')),
        body: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const CircularProgressIndicator(),
              const SizedBox(height: 16),
              Text(_status),
            ],
          ),
        ),
      );

  Widget _telaErro(String msg) => Scaffold(
        appBar: AppBar(title: const Text('Navegação')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.error_outline, color: Colors.red, size: 48),
                const SizedBox(height: 12),
                Text(msg, textAlign: TextAlign.center),
                const SizedBox(height: 20),
                ElevatedButton(
                  onPressed: () => Navigator.of(context).maybePop(),
                  child: const Text('Voltar'),
                ),
              ],
            ),
          ),
        ),
      );
}

class _FabRedondo extends StatelessWidget {
  const _FabRedondo({required this.icone, required this.onTap, this.rotulo});

  final IconData icone;
  final VoidCallback onTap;
  final String? rotulo;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      elevation: 4,
      borderRadius: BorderRadius.circular(24),
      child: InkWell(
        borderRadius: BorderRadius.circular(24),
        onTap: onTap,
        child: Padding(
          padding: EdgeInsets.symmetric(
            horizontal: rotulo == null ? 10 : 14,
            vertical: 10,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icone, color: MaricaTheme.cinzaTexto, size: 22),
              if (rotulo != null) ...[
                const SizedBox(width: 6),
                Text(
                  rotulo!,
                  style: const TextStyle(
                    color: MaricaTheme.cinzaTexto,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// FAB que reabre o painel do passageiro (mostra o nome para contexto rápido).
class _FabPassageiro extends StatelessWidget {
  const _FabPassageiro({required this.nome, required this.onTap});

  final String nome;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: MaricaTheme.vermelho,
      elevation: 6,
      borderRadius: BorderRadius.circular(28),
      child: InkWell(
        borderRadius: BorderRadius.circular(28),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.person, color: Colors.white, size: 22),
              const SizedBox(width: 8),
              ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 160),
                child: Text(
                  nome,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
              const SizedBox(width: 6),
              const Icon(
                Icons.keyboard_arrow_up,
                color: Colors.white,
                size: 20,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TransladoConcluido extends StatelessWidget {
  const _TransladoConcluido({required this.onVoltar});
  final VoidCallback onVoltar;

  @override
  Widget build(BuildContext context) {
    return Material(
      elevation: 4,
      borderRadius: BorderRadius.circular(16),
      color: Colors.white,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.check_circle, color: Colors.green, size: 40),
            const SizedBox(height: 8),
            const Text(
              'Translado concluído',
              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16),
            ),
            const SizedBox(height: 12),
            ElevatedButton(
              onPressed: onVoltar,
              child: const Text('Voltar à rota'),
            ),
          ],
        ),
      ),
    );
  }
}
