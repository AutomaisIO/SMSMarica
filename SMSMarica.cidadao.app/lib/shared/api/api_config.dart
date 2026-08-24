/// Configuração base da API do `SMSMais.server`.
///
/// Enquanto o app está em desenvolvimento local, apontamos para o launch do
/// Host em modo Dev (ver `SMSMais.server/src/SMSMais.Api/Properties/launchSettings.json`).
///
/// Para emulador Android, `localhost` do host é `10.0.2.2`.
/// Para dispositivo físico, trocar por IP da máquina de dev.
abstract final class ApiConfig {
  /// URL base a usar no emulador Android (acessa o host via 10.0.2.2).
  static const String baseUrlAndroidEmulator = 'http://10.0.2.2:5080';

  /// URL base a usar no simulador iOS / desktop / web de desenvolvimento.
  static const String baseUrlLocalhost = 'http://localhost:5080';

  /// Timeout padrão das requisições (em milissegundos).
  static const int timeoutMs = 15000;
}
