import 'package:agente/features/auth/presentation/login_page.dart';
import 'package:agente/features/rota/presentation/rota_do_dia_page.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = _SessaoRefresh(ref);

  return GoRouter(
    initialLocation: '/login',
    refreshListenable: refresh,
    redirect: (context, state) {
      final sessao = ref.read(sessaoProvider);
      final indoParaLogin = state.matchedLocation == '/login';
      if (sessao == null && !indoParaLogin) return '/login';
      if (sessao != null && indoParaLogin) return '/rota';
      return null;
    },
    routes: [
      GoRoute(
        path: '/login',
        builder: (_, __) => const LoginPage(),
      ),
      GoRoute(
        path: '/rota',
        builder: (_, __) => const RotaDoDiaPage(),
      ),
    ],
  );
});

class _SessaoRefresh extends ChangeNotifier {
  _SessaoRefresh(Ref ref) {
    ref.listen(sessaoProvider, (_, __) => notifyListeners());
  }
}
