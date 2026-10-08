import 'dart:async';

import 'package:agente/app/theme.dart';
import 'package:agente/features/deslocamento/application/deslocamento_controller.dart';
import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:agente/shared/gps/gps_ao_vivo.dart';
import 'package:agente/shared/permissoes/permissoes_localizacao.dart';
import 'package:agente/shared/plataforma/canal_dispositivo.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';
import 'package:google_navigation_flutter/google_navigation_flutter.dart';

/// Tela de Deslocamento: mapa seguindo o tablet + velocímetro e odômetro com
/// Iniciar/Parar. Usa o mapa simples (sem sessão de navegação — não é cobrado
/// como navegação). Contrato: `docs/modulos/tfd/deslocamento-tablet.md`.
class DeslocamentoPage extends ConsumerStatefulWidget {
  const DeslocamentoPage({super.key});

  @override
  ConsumerState<DeslocamentoPage> createState() => _DeslocamentoPageState();
}

class _DeslocamentoPageState extends ConsumerState<DeslocamentoPage> {
  GoogleMapViewController? _mapa;
  bool _seguir = true;
  double _rumoCamera = 0;
  Timer? _relogio;

  @override
  void initState() {
    super.initState();
    unawaited(const CanalDispositivo().manterTelaLigada(ligada: true));
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      await const PermissoesLocalizacao().solicitarForeground();
      await ref.read(gpsAoVivoProvider.notifier).iniciar();
    });
    // Atualiza o cronômetro na tela enquanto o deslocamento conta.
    _relogio = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted && ref.read(deslocamentoProvider).ativo) setState(() {});
    });
  }

  @override
  void dispose() {
    _relogio?.cancel();
    unawaited(const CanalDispositivo().manterTelaLigada(ligada: false));
    super.dispose();
  }

  Future<void> _aoCriarMapa(GoogleMapViewController controller) async {
    _mapa = controller;
    await controller.setMyLocationEnabled(true);
    await controller.settings.setMyLocationButtonEnabled(false);
    await controller.settings.setZoomControlsEnabled(false);
    await controller.settings.setMapToolbarEnabled(false);
    final fix = ref.read(gpsAoVivoProvider);
    if (fix != null) await _centralizar(fix, animar: false);
  }

  Future<void> _centralizar(Position p, {bool animar = true}) async {
    final mapa = _mapa;
    if (mapa == null) return;
    // Rumo do carro só quando está andando; parado, o GPS dá rumo aleatório.
    if (p.speed * 3.6 > 5 && p.heading >= 0 && p.heading < 360) {
      _rumoCamera = p.heading;
    }
    final update = CameraUpdate.newCameraPosition(
      CameraPosition(
        target: LatLng(latitude: p.latitude, longitude: p.longitude),
        zoom: 17,
        bearing: _rumoCamera,
        tilt: 30,
      ),
    );
    try {
      if (animar) {
        await mapa.animateCamera(
          update,
          duration: const Duration(milliseconds: 800),
        );
      } else {
        await mapa.moveCamera(update);
      }
    } on Object catch (_) {
      // Mapa ainda não pronto: o próximo fix tenta de novo.
    }
  }

  void _alternarDeslocamento() {
    final controller = ref.read(deslocamentoProvider.notifier);
    if (ref.read(deslocamentoProvider).ativo) {
      controller.parar();
    } else {
      controller.iniciar();
      setState(() => _seguir = true);
      final fix = ref.read(gpsAoVivoProvider);
      if (fix != null) unawaited(_centralizar(fix));
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.listen<Position?>(gpsAoVivoProvider, (_, p) {
      if (p != null && _seguir) unawaited(_centralizar(p));
    });
    final desloc = ref.watch(deslocamentoProvider);
    final dispositivo = ref.watch(dispositivoProvider);
    final semGps = ref.watch(gpsAoVivoProvider) == null;

    ref.listen<EstadoDispositivo>(dispositivoProvider, (_, atual) {
      final aviso = atual.aviso;
      if (aviso != null) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(aviso)));
        ref.read(dispositivoProvider.notifier).limparAviso();
      }
    });

    return Scaffold(
      appBar: AppBar(
        title: const Text('Deslocamento'),
        actions: [
          TextButton.icon(
            onPressed: () => context.push('/rota'),
            style: TextButton.styleFrom(foregroundColor: Colors.white),
            icon: const Icon(Icons.list_alt),
            label: const Text('Rota do dia'),
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sair',
            onPressed: () {
              ref.read(sessaoProvider.notifier).sair();
              context.go('/login');
            },
          ),
        ],
      ),
      body: OrientationBuilder(
        builder: (context, orientacao) {
          final painel = _PainelDeslocamento(
            estado: desloc,
            semGps: semGps,
            onAlternar: _alternarDeslocamento,
          );
          final mapa = Stack(
            fit: StackFit.expand,
            children: [
              GoogleMapsMapView(
                onViewCreated: _aoCriarMapa,
                initialCameraPosition: const CameraPosition(
                  // Centro de Maricá até o primeiro fix.
                  target: LatLng(latitude: -22.9193, longitude: -42.8186),
                  zoom: 13,
                ),
                initialZoomControlsEnabled: false,
                initialMapToolbarEnabled: false,
                onCameraMoveStarted: (_, gesto) {
                  if (gesto && _seguir) setState(() => _seguir = false);
                },
              ),
              Positioned(
                top: 12,
                left: 12,
                right: 12,
                child: _FaixaVeiculo(
                  estado: dispositivo,
                  onVincular: () => context.push('/vincular'),
                ),
              ),
              if (!_seguir)
                Positioned(
                  right: 16,
                  bottom: 16,
                  child: FloatingActionButton.extended(
                    heroTag: 'recentrar',
                    onPressed: () {
                      setState(() => _seguir = true);
                      final fix = ref.read(gpsAoVivoProvider);
                      if (fix != null) unawaited(_centralizar(fix));
                    },
                    icon: const Icon(Icons.my_location),
                    label: const Text('Centralizar'),
                  ),
                ),
            ],
          );

          if (orientacao == Orientation.landscape) {
            return Row(
              children: [
                Expanded(child: mapa),
                SizedBox(width: 360, child: painel),
              ],
            );
          }
          return Column(
            children: [
              Expanded(child: mapa),
              painel,
            ],
          );
        },
      ),
    );
  }
}

class _FaixaVeiculo extends StatelessWidget {
  const _FaixaVeiculo({required this.estado, required this.onVincular});

  final EstadoDispositivo estado;
  final VoidCallback onVincular;

  @override
  Widget build(BuildContext context) {
    if (estado.carregando) return const SizedBox.shrink();
    final vinculado = estado.vinculado;
    return Align(
      alignment: Alignment.topLeft,
      child: Material(
        elevation: 3,
        borderRadius: BorderRadius.circular(24),
        color: vinculado ? Colors.white : Colors.amber.shade100,
        child: InkWell(
          borderRadius: BorderRadius.circular(24),
          onTap: onVincular,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(
                  vinculado ? Icons.directions_car : Icons.link_off,
                  color: vinculado
                      ? MaricaTheme.vermelho
                      : Colors.orange.shade900,
                ),
                const SizedBox(width: 8),
                Text(
                  vinculado
                      ? (estado.veiculo?.descricao ?? 'Veículo vinculado')
                      : 'Tablet não vinculado — Vincular',
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _PainelDeslocamento extends StatelessWidget {
  const _PainelDeslocamento({
    required this.estado,
    required this.semGps,
    required this.onAlternar,
  });

  final EstadoDeslocamento estado;
  final bool semGps;
  final VoidCallback onAlternar;

  @override
  Widget build(BuildContext context) {
    final ativo = estado.ativo;
    final mostrarValores = estado.fase != FaseDeslocamento.parado;
    final velocidade = mostrarValores
        ? estado.velocidadeKmh.round().toString()
        : '—';

    return Material(
      elevation: 8,
      color: Colors.white,
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              _Rotulo(
                estado.fase == FaseDeslocamento.congelado
                    ? 'Deslocamento parado'
                    : ativo
                    ? 'Em deslocamento'
                    : 'Pronto para iniciar',
                cor: ativo ? Colors.green.shade700 : Colors.grey.shade700,
              ),
              const SizedBox(height: 8),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.baseline,
                textBaseline: TextBaseline.alphabetic,
                children: [
                  Text(
                    velocidade,
                    style: const TextStyle(
                      fontSize: 88,
                      height: 1,
                      fontWeight: FontWeight.w800,
                      fontFeatures: [FontFeature.tabularFigures()],
                    ),
                  ),
                  const SizedBox(width: 8),
                  const Text(
                    'km/h',
                    style: TextStyle(fontSize: 24, fontWeight: FontWeight.w600),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: _Indicador(
                      titulo: 'Deslocamento',
                      valor: mostrarValores
                          ? _formatarDistancia(estado.distanciaM)
                          : '—',
                    ),
                  ),
                  Expanded(
                    child: _Indicador(
                      titulo: 'Tempo',
                      valor: mostrarValores
                          ? _formatarTempo(estado.decorrido)
                          : '—',
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                height: 76,
                child: ElevatedButton.icon(
                  onPressed: onAlternar,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: ativo
                        ? Colors.red.shade700
                        : Colors.green.shade700,
                    foregroundColor: Colors.white,
                    textStyle: const TextStyle(
                      fontSize: 26,
                      fontWeight: FontWeight.w800,
                    ),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14),
                    ),
                  ),
                  icon: Icon(
                    ativo ? Icons.stop_rounded : Icons.play_arrow_rounded,
                    size: 40,
                  ),
                  label: Text(ativo ? 'PARAR' : 'INICIAR'),
                ),
              ),
              if (semGps) ...[
                const SizedBox(height: 10),
                Text(
                  'Aguardando sinal de GPS…',
                  style: TextStyle(
                    color: Colors.orange.shade900,
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

  static String _formatarDistancia(double metros) {
    if (metros < 1000) return '${metros.round()} m';
    return '${(metros / 1000).toStringAsFixed(2).replaceAll('.', ',')} km';
  }

  static String _formatarTempo(Duration d) {
    String dois(int n) => n.toString().padLeft(2, '0');
    final h = d.inHours;
    final m = d.inMinutes.remainder(60);
    final s = d.inSeconds.remainder(60);
    return h > 0 ? '$h:${dois(m)}:${dois(s)}' : '${dois(m)}:${dois(s)}';
  }
}

class _Rotulo extends StatelessWidget {
  const _Rotulo(this.texto, {required this.cor});

  final String texto;
  final Color cor;

  @override
  Widget build(BuildContext context) => Text(
    texto.toUpperCase(),
    style: TextStyle(
      color: cor,
      fontWeight: FontWeight.w700,
      letterSpacing: 1.2,
    ),
  );
}

class _Indicador extends StatelessWidget {
  const _Indicador({required this.titulo, required this.valor});

  final String titulo;
  final String valor;

  @override
  Widget build(BuildContext context) => Column(
    children: [
      Text(titulo, style: TextStyle(color: Colors.grey.shade700, fontSize: 15)),
      const SizedBox(height: 4),
      Text(
        valor,
        style: const TextStyle(
          fontSize: 32,
          fontWeight: FontWeight.w700,
          fontFeatures: [FontFeature.tabularFigures()],
        ),
      ),
    ],
  );
}
