import 'package:permission_handler/permission_handler.dart';

/// Fluxo recomendado Android 10+:
/// 1) pedir `location` (foreground);
/// 2) só depois pedir `locationAlways` (background), que redireciona o usuário
///    para a tela de configurações se negado.
/// Implementação concreta do foreground service entra em A2.1.
class PermissoesLocalizacao {
  const PermissoesLocalizacao();

  Future<bool> solicitarForeground() async {
    final status = await Permission.location.request();
    return status.isGranted;
  }

  Future<bool> solicitarBackground() async {
    if (!await Permission.location.isGranted) return false;
    final status = await Permission.locationAlways.request();
    return status.isGranted;
  }

  Future<bool> solicitarNotificacoes() async {
    final status = await Permission.notification.request();
    return status.isGranted;
  }
}
