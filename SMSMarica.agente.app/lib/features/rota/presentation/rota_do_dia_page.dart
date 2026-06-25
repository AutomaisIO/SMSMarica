import 'package:agente/app/theme.dart';
import 'package:agente/features/auth/facial/presentation/reconhecimento_facial_page.dart';
import 'package:agente/features/rota/data/rota_repository.dart';
import 'package:agente/features/rota/domain/parada.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:agente/shared/permissoes/permissoes_localizacao.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

class RotaDoDiaPage extends ConsumerStatefulWidget {
  const RotaDoDiaPage({super.key});

  @override
  ConsumerState<RotaDoDiaPage> createState() => _RotaDoDiaPageState();
}

class _RotaDoDiaPageState extends ConsumerState<RotaDoDiaPage> {
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
          IconButton(
            icon: const Icon(Icons.face),
            tooltip: 'Reconhecimento facial',
            onPressed: () => Navigator.of(context).push<void>(
              MaterialPageRoute(
                builder: (_) => const ReconhecimentoFacialPage(),
              ),
            ),
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
      bottomNavigationBar: rotaAsync.maybeWhen(
        data: (paradas) => paradas.isEmpty
            ? null
            : SafeArea(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                  child: ElevatedButton.icon(
                    onPressed: () => context.push('/navegacao'),
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
                onRefresh: () async => ref.refresh(rotaDoDiaProvider.future),
                child: ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: paradas.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 12),
                  itemBuilder: (_, i) => _ParadaCard(parada: paradas[i]),
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
  const _ParadaCard({required this.parada});
  final Parada parada;

  @override
  Widget build(BuildContext context) {
    final hh = parada.horarioPrevisto.hour.toString().padLeft(2, '0');
    final mm = parada.horarioPrevisto.minute.toString().padLeft(2, '0');

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
            const SizedBox(height: 4),
            Text(parada.endereco),
            const SizedBox(height: 4),
            Text('Previsto: $hh:$mm'),
          ],
        ),
        trailing: IconButton(
          icon: const Icon(
            Icons.navigation_outlined,
            color: MaricaTheme.vermelho,
          ),
          tooltip: 'Navegar',
          onPressed: () => context.push('/navegacao'),
        ),
      ),
    );
  }
}
