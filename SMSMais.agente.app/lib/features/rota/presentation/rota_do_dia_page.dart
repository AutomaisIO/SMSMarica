import 'dart:async';

import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/facial/presentation/reconhecimento_facial_page.dart';
import 'package:agente/features/auth/presentation/confirmar_motorista.dart';
import 'package:agente/features/ocorrencia/presentation/botao_report.dart';
import 'package:agente/features/resgate/presentation/resgates_page.dart';
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

  /// A lista de resgate é de todos os carros: dá para marcar daqui também.
  Future<void> _resgates() async {
    final rota = await ref.read(rotaAtualProvider.future);
    if (rota == null || !mounted) return;
    final mudou = await Navigator.of(context).push<bool>(
      MaterialPageRoute(builder: (_) => ResgatesPage(rotaId: rota.rotaId)),
    );
    if (mudou ?? false) ref.invalidate(rotasDoDiaProvider);
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
    final rotaAberta =
        ref.watch(rotaAtualProvider).valueOrNull?.concluida == false;

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
      // Com rota aberta, o resgate fica à mão mesmo sem parada (rota só para
      // resgatar); a navegação, só com paradas.
      bottomNavigationBar: rotaAsync.maybeWhen(
        data: (paradas) => paradas.isEmpty && !rotaAberta
            ? null
            : SafeArea(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                  // Tablet em paisagem: os dois lado a lado.
                  child: Row(
                    children: [
                      Expanded(
                        child: OutlinedButton.icon(
                          style: OutlinedButton.styleFrom(
                            minimumSize: const Size.fromHeight(52),
                          ),
                          onPressed: _resgates,
                          icon: const Icon(Icons.pan_tool_alt),
                          label: const Text(
                            'Pacientes disponíveis para resgate',
                          ),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: ElevatedButton.icon(
                          onPressed: paradas.isEmpty ? null : _navegar,
                          icon: const Icon(Icons.navigation),
                          label: const Text('Iniciar navegação do translado'),
                        ),
                      ),
                    ],
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
                        children: [
                          const Icon(
                            Icons.event_busy,
                            size: 48,
                            color: Colors.grey,
                          ),
                          const SizedBox(height: 12),
                          Text(
                            rotaAberta
                                ? 'A rota de hoje ainda não tem paradas. '
                                      'Marque quem este carro vai buscar em '
                                      '“Pacientes disponíveis para resgate”.'
                                : 'Nenhum translado hoje para este veículo. '
                                      'Puxe a tela para baixo para atualizar '
                                      'ou fale com a central.',
                            textAlign: TextAlign.center,
                            style: const TextStyle(fontSize: 16),
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
