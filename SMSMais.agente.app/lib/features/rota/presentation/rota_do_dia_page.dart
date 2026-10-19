import 'dart:async';

import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/facial/presentation/reconhecimento_facial_page.dart';
import 'package:agente/features/auth/presentation/confirmar_motorista.dart';
import 'package:agente/features/ocorrencia/presentation/botao_report.dart';
import 'package:agente/features/rota/data/rota_repository.dart';
import 'package:agente/features/rota/domain/parada.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:agente/shared/permissoes/permissoes_localizacao.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

class RotaDoDiaPage extends ConsumerStatefulWidget {
  const RotaDoDiaPage({super.key});

  @override
  ConsumerState<RotaDoDiaPage> createState() => _RotaDoDiaPageState();
}

class _RotaDoDiaPageState extends ConsumerState<RotaDoDiaPage> {
  /// Confere quem está ao volante antes de abrir a navegação (ADR-0073).
  Future<void> _navegar() async {
    if (await confirmarMotoristaParaTranslado(context, ref) && mounted) {
      unawaited(context.push('/navegacao'));
    }
  }

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      const perms = PermissoesLocalizacao();
      await perms.solicitarBackground();
      await perms.solicitarNotificacoes();
    });
  }

  @override
  Widget build(BuildContext context) {
    final sessao = ref.watch(sessaoProvider);
    final rotaAsync = ref.watch(rotaDoDiaProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Rota do dia'),
        actions: [
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 6),
            child: BotaoReport(sobreBarra: true),
          ),
          const SizedBox(width: 8),
          // Tela de calibração do reconhecimento: só nos builds de teste.
          if (!kReleaseMode)
            IconButton(
              icon: const Icon(Icons.face),
              tooltip: 'Reconhecimento facial (teste)',
              onPressed: () => Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => const ReconhecimentoFacialPage(),
                ),
              ),
            ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Trocar motorista',
            onPressed: () {
              unawaited(ref.read(sessaoProvider.notifier).sair());
              context.go('/login');
            },
          ),
        ],
      ),
      bottomNavigationBar: rotaAsync.maybeWhen(
        data: (paradas) => paradas.isEmpty
            ? null
            : SafeArea(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                  child: ElevatedButton.icon(
                    onPressed: _navegar,
                    icon: const Icon(Icons.navigation),
                    label: const Text('Iniciar navegação do translado'),
                  ),
                ),
              ),
        orElse: () => null,
      ),
      body: Column(
        children: [
          _CabecalhoMotorista(nome: sessao?.nome ?? ''),
          Expanded(
            child: rotaAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(child: Text('Erro ao carregar: $e')),
              data: (paradas) => RefreshIndicator(
                onRefresh: () async => ref.refresh(rotasDoDiaProvider.future),
                child: paradas.isEmpty
                    ? ListView(
                        padding: const EdgeInsets.all(32),
                        children: const [
                          Icon(Icons.event_busy, size: 48, color: Colors.grey),
                          SizedBox(height: 12),
                          Text(
                            'Nenhum translado hoje para este veículo. '
                            'Puxe a tela para baixo para atualizar ou fale '
                            'com a central.',
                            textAlign: TextAlign.center,
                            style: TextStyle(fontSize: 16),
                          ),
                        ],
                      )
                    : ListView.separated(
                        padding: const EdgeInsets.all(16),
                        itemCount: paradas.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 12),
                        itemBuilder: (_, i) => _ParadaCard(
                          parada: paradas[i],
                          onNavegar: _navegar,
                        ),
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _CabecalhoMotorista extends StatelessWidget {
  const _CabecalhoMotorista({required this.nome});
  final String nome;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      color: MaricaTheme.vermelho,
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Olá, $nome',
            style: const TextStyle(
              color: Colors.white,
              fontSize: 18,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'Confira as paradas do dia',
            style: TextStyle(color: Colors.white70, fontSize: 13),
          ),
        ],
      ),
    );
  }
}

class _ParadaCard extends StatelessWidget {
  const _ParadaCard({required this.parada, required this.onNavegar});
  final Parada parada;
  final VoidCallback onNavegar;

  @override
  Widget build(BuildContext context) {
    final previsto = parada.horarioPrevisto;
    final hora = previsto == null
        ? null
        : '${previsto.hour.toString().padLeft(2, '0')}:'
              '${previsto.minute.toString().padLeft(2, '0')}';

    return Card(
      elevation: 1,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
      child: ListTile(
        contentPadding: const EdgeInsets.all(12),
        leading: CircleAvatar(
          backgroundColor: MaricaTheme.vermelho,
          child: Text(
            '${parada.ordem}',
            style: const TextStyle(color: Colors.white),
          ),
        ),
        title: Text(
          parada.pacienteNome,
          style: const TextStyle(fontWeight: FontWeight.w600),
        ),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (parada.etiqueta != null) ...[
              const SizedBox(height: 4),
              Text(
                parada.etiqueta!,
                style: const TextStyle(
                  color: MaricaTheme.vermelho,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
            const SizedBox(height: 4),
            Text(parada.endereco),
            if (hora != null) ...[
              const SizedBox(height: 4),
              Text('Previsto: $hora'),
            ],
          ],
        ),
        trailing: IconButton(
          icon: const Icon(
            Icons.navigation_outlined,
            color: MaricaTheme.vermelho,
          ),
          tooltip: 'Navegar',
          onPressed: onNavegar,
        ),
      ),
    );
  }
}
