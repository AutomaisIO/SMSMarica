import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:sms_mais_cidadao/features/perfil/perfil_provider.dart';
import 'package:sms_mais_cidadao/shared/push/push.dart';
import 'package:sms_mais_cidadao/shared/sessao/sair.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';
import 'package:sms_mais_cidadao/shared/sincronizar.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

class ItemMenu {
  const ItemMenu(this.rota, this.rotulo, this.icone);

  final String rota;
  final String rotulo;
  final IconData icone;
}

/// Mesmo menu do PWA (`NAV` do AppShell.tsx), na mesma ordem.
const itensMenu = [
  ItemMenu('/', 'Início', LucideIcons.house),
  ItemMenu('/agendados/consultas', 'Consultas agendadas', LucideIcons.calendarClock),
  ItemMenu('/agendados/exames', 'Exames agendados', LucideIcons.calendarPlus),
  ItemMenu('/atendimentos', 'Meus atendimentos', LucideIcons.stethoscope),
  ItemMenu('/exames', 'Exames', LucideIcons.flaskConical),
  ItemMenu('/documentos', 'Documentos', LucideIcons.folderOpen),
  ItemMenu('/chat', 'Chat', LucideIcons.messageCircle),
  ItemMenu('/transporte', 'Transporte (TFD)', LucideIcons.calendarHeart),
  ItemMenu('/perfil', 'Meu perfil', LucideIcons.user),
];

/// Online/offline (para a faixa "Sem conexão").
final onlineProvider = StreamProvider<bool>((ref) async* {
  final c = Connectivity();
  bool online(List<ConnectivityResult> r) => !r.contains(ConnectivityResult.none) || r.length > 1;
  yield online(await c.checkConnectivity());
  yield* c.onConnectivityChanged.map(online);
});

final versaoAppProvider = FutureProvider<String>((_) async {
  final info = await PackageInfo.fromPlatform();
  return info.version;
});

/// Moldura das telas autenticadas (`AppShell.tsx`): barra vermelha com menu sanduíche, nome do
/// app e avatar; faixa de offline; menu lateral com mini-cartão; volta ao Início no "voltar".
class AppShell extends ConsumerStatefulWidget {
  const AppShell({required this.child, required this.local, super.key});

  final Widget child;

  /// Caminho atual (para marcar o item ativo do menu).
  final String local;

  @override
  ConsumerState<AppShell> createState() => _AppShellState();
}

class _AppShellState extends ConsumerState<AppShell> {
  final _scaffold = GlobalKey<ScaffoldState>();
  late final AppLifecycleListener _ciclo;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      // Sincronização offline-first: baixa listas + documentos em segundo plano (1x por entrada).
      ref.read(sincronizadorProvider).sincronizarTudo();
      // Push: o AppShell só existe depois do login e do termo aceito — só então pede a permissão.
      ref.read(pushProvider).registrarAparelho();
    });
    // Volta ao primeiro plano: refaz o registro do push que falhou (API fora, sem rede). Registrado,
    // não faz nada; negado, só lê a permissão — nunca reabre o pedido (ver MarcaDoRegistro).
    _ciclo = AppLifecycleListener(onResume: () => ref.read(pushProvider).registrarAparelho());
  }

  @override
  void dispose() {
    _ciclo.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final perfil = ref.watch(perfilProvider).valueOrNull;
    final sessao = ref.watch(sessaoProvider);
    final online = ref.watch(onlineProvider).valueOrNull ?? true;
    final nome = perfil?.nomeExibicao ?? sessao?.paciente.nome ?? 'Cidadão';
    final foto = perfil?.fotoBase64;
    final cpf = perfil?.cpf ?? sessao?.paciente.cpf;
    final topo = MediaQuery.paddingOf(context).top;

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light.copyWith(statusBarColor: Colors.transparent),
      child: PopScope(
        canPop: widget.local == '/',
        onPopInvokedWithResult: (podeVoltar, _) {
          if (podeVoltar) return;
          if (_scaffold.currentState?.isDrawerOpen ?? false) {
            Navigator.of(context).pop();
            return;
          }
          context.go('/');
        },
        child: Scaffold(
          key: _scaffold,
          backgroundColor: CoresMarica.papel,
          drawerScrimColor: CoresMarica.tinta.withValues(alpha: 0.4),
          drawer: _Menu(nome: nome, foto: foto, cpf: cpf, local: widget.local),
          body: Column(
            children: [
              // Barra superior (civismo Maricá)
              DecoratedBox(
                decoration: const BoxDecoration(color: CoresMarica.marica, boxShadow: SombrasMarica.topo),
                child: Padding(
                  padding: EdgeInsets.fromLTRB(12, topo + 12, 12, 12),
                  child: Row(
                    children: [
                      _BotaoBarra(
                        icone: LucideIcons.menu,
                        rotulo: 'Abrir menu',
                        onTap: () => _scaffold.currentState?.openDrawer(),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Saúde Maricá',
                              style: Txt.display(15, cor: CoresMarica.branco, altura: 1.25, espacamento: -0.2),
                            ),
                            Text(
                              'App do Cidadão',
                              style: Txt.sans(
                                11,
                                peso: FontWeight.w500,
                                cor: CoresMarica.branco.withValues(alpha: 0.8),
                                altura: 1.25,
                              ),
                            ),
                          ],
                        ),
                      ),
                      Pressionavel(
                        onTap: () => context.go('/perfil'),
                        escala: 0.95,
                        semantica: 'Meu perfil',
                        child: Container(
                          decoration: BoxDecoration(
                            shape: BoxShape.circle,
                            border: Border.all(color: CoresMarica.branco.withValues(alpha: 0.3), width: 2),
                          ),
                          child: Avatar(
                            nome: nome,
                            foto: foto,
                            tamanho: 40,
                            fundo: CoresMarica.branco.withValues(alpha: 0.15),
                            corTexto: CoresMarica.branco,
                            anel: Border.all(color: Colors.transparent, width: 0),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              // Faixa de offline — os dados exibidos são os últimos sincronizados.
              if (!online)
                Container(
                  width: double.infinity,
                  color: CoresMarica.ambar100,
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                  child: Text(
                    'Sem conexão — mostrando os dados salvos no aparelho.',
                    textAlign: TextAlign.center,
                    style: Txt.sans(12, peso: FontWeight.w500, cor: CoresMarica.ambar800),
                  ),
                ),
              Expanded(child: widget.child),
            ],
          ),
        ),
      ),
    );
  }
}

class _BotaoBarra extends StatelessWidget {
  const _BotaoBarra({required this.icone, required this.rotulo, required this.onTap});

  final IconData icone;
  final String rotulo;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(RaiosMarica.xl),
        highlightColor: CoresMarica.branco.withValues(alpha: 0.15),
        splashColor: CoresMarica.branco.withValues(alpha: 0.1),
        child: Tooltip(
          message: rotulo,
          child: SizedBox(width: 44, height: 44, child: Icon(icone, color: CoresMarica.branco, size: 24)),
        ),
      ),
    );
  }
}

/// Menu lateral (drawer) com o mini-cartão vinho no topo.
class _Menu extends ConsumerWidget {
  const _Menu({required this.nome, required this.foto, required this.cpf, required this.local});

  final String nome;
  final String? foto;
  final String? cpf;
  final String local;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final largura = MediaQuery.sizeOf(context).width.clamp(0, 460) * 0.78;
    final versao = ref.watch(versaoAppProvider).valueOrNull;
    final topo = MediaQuery.paddingOf(context).top;

    return Drawer(
      width: largura > 320 ? 320 : largura,
      backgroundColor: CoresMarica.papel,
      shape: const RoundedRectangleBorder(),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Guilloche(
            cor: CoresMarica.vinho,
            child: Padding(
              // pt-[safe+1.5rem]; botão fechar 36px + mb-4 antes do avatar (como o PWA).
              padding: EdgeInsets.fromLTRB(20, topo + 24, 20, 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Align(
                    alignment: Alignment.centerRight,
                    child: SizedBox(
                      width: 36,
                      height: 36,
                      child: IconButton(
                        padding: EdgeInsets.zero,
                        onPressed: () => Navigator.of(context).pop(),
                        tooltip: 'Fechar menu',
                        style: IconButton.styleFrom(
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(RaiosMarica.lg)),
                        ),
                        icon: const Icon(LucideIcons.x, color: CoresMarica.branco, size: 20),
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                  Avatar(
                    nome: nome,
                    foto: foto,
                    tamanho: 56,
                    fundo: CoresMarica.branco.withValues(alpha: 0.15),
                    corTexto: CoresMarica.branco,
                    anel: Border.all(color: CoresMarica.branco.withValues(alpha: 0.2)),
                  ),
                  const SizedBox(height: 12),
                  Text(nome, style: Txt.display(18, cor: CoresMarica.branco, altura: 1.25)),
                  if (cpf != null && cpf!.isNotEmpty)
                    Text(
                      'CPF ${formatarCpf(cpf!)}',
                      style: Txt.sans(12, cor: CoresMarica.branco.withValues(alpha: 0.7)),
                    ),
                ],
              ),
            ),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 16),
              children: [
                for (final item in itensMenu)
                  _LinhaMenu(
                    item: item,
                    ativo: item.rota == '/' ? local == '/' : local.startsWith(item.rota),
                    onTap: () {
                      Navigator.of(context).pop();
                      context.go(item.rota);
                    },
                  ),
              ],
            ),
          ),
          DecoratedBox(
            decoration: const BoxDecoration(border: Border(top: BorderSide(color: CoresMarica.areia))),
            child: Padding(
              padding: EdgeInsets.fromLTRB(12, 12, 12, 12 + MediaQuery.paddingOf(context).bottom),
              child: Column(
                children: [
                  _LinhaMenu(
                    item: const ItemMenu('', 'Sair', LucideIcons.logOut),
                    ativo: false,
                    cor: CoresMarica.marica,
                    onTap: () async {
                      Navigator.of(context).pop();
                      await sairDoApp(ref);
                    },
                  ),
                  if (versao != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Text(
                        'versão $versao',
                        style: Txt.sans(
                          11,
                          peso: FontWeight.w500,
                          cor: CoresMarica.tintaMute.withValues(alpha: 0.7),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _LinhaMenu extends StatelessWidget {
  const _LinhaMenu({required this.item, required this.ativo, required this.onTap, this.cor});

  final ItemMenu item;
  final bool ativo;
  final VoidCallback onTap;
  final Color? cor;

  @override
  Widget build(BuildContext context) {
    final c = ativo ? CoresMarica.marica : (cor ?? CoresMarica.tinta);
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Material(
        color: ativo ? CoresMarica.marica.withValues(alpha: 0.1) : Colors.transparent,
        borderRadius: BorderRadius.circular(RaiosMarica.x2l),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(RaiosMarica.x2l),
          highlightColor: (cor ?? CoresMarica.areia).withValues(alpha: cor != null ? 0.1 : 0.6),
          child: Container(
            constraints: const BoxConstraints(minHeight: 52),
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: Row(
              children: [
                Icon(item.icone, size: 20, color: c),
                const SizedBox(width: 12),
                Expanded(child: Text(item.rotulo, style: Txt.sans(15, peso: FontWeight.w500, cor: c))),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
