import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:sms_marica_cidadao/features/auth/presentation/auth_controller.dart';
import 'package:sms_marica_cidadao/features/auth/presentation/login_page.dart';
import 'package:sms_marica_cidadao/features/perfil/presentation/perfil_page.dart';

final routerProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    initialLocation: '/login',
    redirect: (context, state) {
      final usuario = ref.read(usuarioAtualProvider);
      final autenticado = usuario != null;
      final indoParaLogin = state.matchedLocation == '/login';

      if (!autenticado && !indoParaLogin) {
        return '/login';
      }
      if (autenticado && indoParaLogin) {
        return '/perfil';
      }
      return null;
    },
    routes: [
      GoRoute(
        path: '/login',
        name: 'login',
        builder: (_, __) => const LoginPage(),
      ),
      GoRoute(
        path: '/perfil',
        name: 'perfil',
        builder: (_, __) => const PerfilPage(),
      ),
    ],
  );
});
