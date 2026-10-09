import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:sms_mais_cidadao/features/agendados/agendados_page.dart';
import 'package:sms_mais_cidadao/features/agendados/ticket_exame_page.dart';
import 'package:sms_mais_cidadao/features/atendimentos/atendimentos_page.dart';
import 'package:sms_mais_cidadao/features/auth/consentimento_gate.dart';
import 'package:sms_mais_cidadao/features/auth/entrar_page.dart';
import 'package:sms_mais_cidadao/features/auth/login_page.dart';
import 'package:sms_mais_cidadao/features/auth/otp_page.dart';
import 'package:sms_mais_cidadao/features/auth/verificacao_page.dart';
import 'package:sms_mais_cidadao/features/chat/chat_page.dart';
import 'package:sms_mais_cidadao/features/documento_publico/documento_publico_page.dart';
import 'package:sms_mais_cidadao/features/documentos/documentos_page.dart';
import 'package:sms_mais_cidadao/features/exames/exames_page.dart';
import 'package:sms_mais_cidadao/features/inicio/inicio_page.dart';
import 'package:sms_mais_cidadao/features/perfil/perfil_page.dart';
import 'package:sms_mais_cidadao/features/shell/app_shell.dart';
import 'package:sms_mais_cidadao/features/transporte/transporte_page.dart';
import 'package:sms_mais_cidadao/shared/sessao/sessao.dart';

/// Rotas — as MESMAS do PWA (`App.tsx`), para que os links do WhatsApp (`/entrar/:token`,
/// `/documento/:token`, `/exames?exame=…`) abram o mesmo lugar no app e no navegador.
bool _publica(String caminho) =>
    caminho.startsWith('/login') || caminho.startsWith('/entrar/') || caminho.startsWith('/documento/');

/// Transição das telas internas: o `animate-rise` do PWA já anima o conteúdo; aqui só um fade
/// curto, para a barra vermelha não "piscar" de uma tela para a outra.
Page<void> _pagina(GoRouterState s, Widget filho) => CustomTransitionPage<void>(
      key: s.pageKey,
      child: filho,
      transitionDuration: const Duration(milliseconds: 150),
      transitionsBuilder: (_, anim, __, c) => FadeTransition(opacity: anim, child: c),
    );

final routerProvider = Provider<GoRouter>((ref) {
  // O roteador reavalia o redirect quando a sessão muda (login, logout, 401).
  final mudouSessao = ValueNotifier<int>(0);
  ref
    ..listen(sessaoProvider, (_, __) => mudouSessao.value++)
    ..onDispose(mudouSessao.dispose);

  return GoRouter(
    initialLocation: '/',
    refreshListenable: mudouSessao,
    redirect: (context, state) {
      final logado = ref.read(sessaoProvider) != null;
      final caminho = state.matchedLocation;
      if (!logado && !_publica(caminho)) return '/login';
      if (logado && caminho.startsWith('/login')) return '/';
      return null;
    },
    routes: [
      GoRoute(path: '/login', builder: (_, __) => const LoginPage()),
      GoRoute(
        path: '/login/verificacao',
        builder: (_, s) => VerificacaoPage(dados: s.extra as Map<String, String?>?),
      ),
      GoRoute(
        path: '/login/codigo',
        builder: (_, s) => OtpPage(dados: s.extra as Map<String, Object?>?),
      ),
      // Link público de download (uso único) enviado ao paciente — sem autenticação.
      GoRoute(
        path: '/documento/:token',
        builder: (_, s) => DocumentoPublicoPage(token: s.pathParameters['token']!),
      ),
      // Magic-link: login em 1 toque a partir do WhatsApp.
      GoRoute(path: '/entrar/:token', builder: (_, s) => EntrarPage(token: s.pathParameters['token']!)),
      ShellRoute(
        builder: (_, s, filho) => ConsentimentoGate(child: AppShell(local: s.uri.path, child: filho)),
        routes: [
          GoRoute(path: '/', pageBuilder: (_, s) => _pagina(s, const InicioPage())),
          GoRoute(path: '/atendimentos', pageBuilder: (_, s) => _pagina(s, const AtendimentosPage())),
          GoRoute(
            path: '/agendados/consultas',
            pageBuilder: (_, s) => _pagina(s, const ConsultasAgendadasPage()),
          ),
          GoRoute(
            path: '/agendados/exames',
            pageBuilder: (_, s) => _pagina(s, const ExamesAgendadosPage()),
            routes: [
              GoRoute(path: ':id', builder: (_, s) => TicketExamePage(id: s.pathParameters['id']!)),
            ],
          ),
          GoRoute(
            path: '/exames',
            pageBuilder: (_, s) => _pagina(s, ExamesPage(destaqueId: s.uri.queryParameters['exame'])),
          ),
          // Laudo pertence ao exame (abre dentro do card em /exames); /laudos redireciona.
          GoRoute(path: '/laudos', redirect: (_, __) => '/exames'),
          GoRoute(path: '/documentos', pageBuilder: (_, s) => _pagina(s, const DocumentosPage())),
          GoRoute(path: '/chat', pageBuilder: (_, s) => _pagina(s, const ChatPage())),
          GoRoute(path: '/transporte', pageBuilder: (_, s) => _pagina(s, const TransportePage())),
          GoRoute(path: '/perfil', pageBuilder: (_, s) => _pagina(s, const PerfilPage())),
        ],
      ),
    ],
    errorBuilder: (_, __) => const _Redirecionar(),
  );
});

/// Rota desconhecida → Início (como o `*` do PWA).
class _Redirecionar extends StatelessWidget {
  const _Redirecionar();

  @override
  Widget build(BuildContext context) {
    WidgetsBinding.instance.addPostFrameCallback((_) => context.go('/'));
    return const SizedBox.shrink();
  }
}
