import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:sms_marica_cidadao/features/auth/presentation/auth_controller.dart';
import 'package:sms_marica_cidadao/features/perfil/domain/paciente.dart';
import 'package:sms_marica_cidadao/features/perfil/presentation/perfil_providers.dart';
import 'package:sms_marica_cidadao/shared/theme/cores_marica.dart';

class PerfilPage extends ConsumerWidget {
  const PerfilPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final estadoPerfil = ref.watch(perfilDoUsuarioProvider);
    final usuario = ref.watch(usuarioAtualProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Meu perfil'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sair',
            onPressed: () async {
              await ref.read(authControllerProvider.notifier).sair();
              if (context.mounted) {
                context.go('/login');
              }
            },
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async => ref.invalidate(perfilDoUsuarioProvider),
        child: estadoPerfil.when(
          loading: () => const _CentralizadoScroll(
            child: Padding(
              padding: EdgeInsets.all(32),
              child: CircularProgressIndicator(),
            ),
          ),
          error: (erro, _) => _CentralizadoScroll(
            child: _MensagemDeErro(
              mensagem: erro.toString(),
              nomeUsuario: usuario?.nomeExibicao,
              onTentarNovamente: () => ref.invalidate(perfilDoUsuarioProvider),
            ),
          ),
          data: (paciente) => _ConteudoPerfil(paciente: paciente),
        ),
      ),
    );
  }
}

class _ConteudoPerfil extends StatelessWidget {
  const _ConteudoPerfil({required this.paciente});
  final Paciente paciente;

  @override
  Widget build(BuildContext context) {
    final formatoData = DateFormat("dd 'de' MMMM 'de' y", 'pt_BR');

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _CartaoBoasVindas(nome: paciente.nomeCompleto),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Dados cadastrais',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 16),
                _Linha(rotulo: 'CPF', valor: paciente.cpfFormatado),
                _Linha(
                  rotulo: 'CNS',
                  valor: paciente.cns ?? 'Não informado',
                ),
                _Linha(
                  rotulo: 'Cadastrado em',
                  valor: formatoData.format(paciente.cadastradoEm.toLocal()),
                ),
                _Linha(
                  rotulo: 'Situação',
                  valor: paciente.ativo ? 'Ativo' : 'Inativo',
                  corValor: paciente.ativo
                      ? CoresMarica.sucesso
                      : CoresMarica.vermelhoEscuro,
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Residência (GPS cadastrado)',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 16),
                _Linha(
                  rotulo: 'Latitude',
                  valor: paciente.latitude.toStringAsFixed(6),
                ),
                _Linha(
                  rotulo: 'Longitude',
                  valor: paciente.longitude.toStringAsFixed(6),
                ),
                const SizedBox(height: 8),
                const Text(
                  'A localização cadastrada é usada apenas para o transporte sanitário até as unidades de saúde.',
                  style: TextStyle(
                    fontSize: 12,
                    color: CoresMarica.cinzaTexto,
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _CartaoBoasVindas extends StatelessWidget {
  const _CartaoBoasVindas({required this.nome});
  final String nome;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: CoresMarica.vermelhoPrincipal,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Olá, ${nome.split(' ').first}',
            style: const TextStyle(
              color: CoresMarica.branco,
              fontSize: 22,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'Bem-vindo ao SMSMarica. Confira seus dados e acompanhe seus translados.',
            style: TextStyle(
              color: CoresMarica.branco,
              fontSize: 14,
              height: 1.4,
            ),
          ),
        ],
      ),
    );
  }
}

class _Linha extends StatelessWidget {
  const _Linha({
    required this.rotulo,
    required this.valor,
    this.corValor,
  });
  final String rotulo;
  final String valor;
  final Color? corValor;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 120,
            child: Text(
              rotulo,
              style: const TextStyle(
                color: CoresMarica.cinzaTexto,
                fontSize: 14,
              ),
            ),
          ),
          Expanded(
            child: Text(
              valor,
              style: TextStyle(
                color: corValor ?? CoresMarica.preto,
                fontSize: 14,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _CentralizadoScroll extends StatelessWidget {
  const _CentralizadoScroll({required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: constraints.maxHeight),
          child: Center(child: child),
        ),
      ),
    );
  }
}

class _MensagemDeErro extends StatelessWidget {
  const _MensagemDeErro({
    required this.mensagem,
    required this.onTentarNovamente,
    this.nomeUsuario,
  });
  final String mensagem;
  final VoidCallback onTentarNovamente;
  final String? nomeUsuario;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(
            Icons.cloud_off,
            size: 64,
            color: CoresMarica.vermelhoPrincipal,
          ),
          const SizedBox(height: 16),
          Text(
            nomeUsuario != null
                ? 'Olá, ${nomeUsuario!.split(' ').first} — não foi possível carregar seu perfil agora.'
                : 'Não foi possível carregar o perfil agora.',
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            mensagem,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: CoresMarica.cinzaTexto,
              fontSize: 13,
            ),
          ),
          const SizedBox(height: 24),
          ElevatedButton.icon(
            onPressed: onTentarNovamente,
            icon: const Icon(Icons.refresh),
            label: const Text('Tentar novamente'),
          ),
        ],
      ),
    );
  }
}
