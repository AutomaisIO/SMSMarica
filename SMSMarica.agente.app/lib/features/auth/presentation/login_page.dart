import 'package:agente/app/theme.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:agente/shared/permissoes/permissoes_localizacao.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

/// Primeiro acesso do motorista: **CPF + OTP por WhatsApp** (espelha o fluxo do
/// paciente). Após autenticado, o cadastro facial é feito *dentro* da sessão
/// (associação justa ao usuário). Logins seguintes serão **só por rosto**.
///
/// Enquanto o backend de OTP do motorista não existe, esta tela opera em
/// **modo teste** (mock) — ver [[backend-app-motorista]] BE-4.
class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _cpfCtrl = TextEditingController();
  final _senhaCtrl = TextEditingController();
  final _formKey = GlobalKey<FormState>();
  bool _carregando = false;

  @override
  void dispose() {
    _cpfCtrl.dispose();
    _senhaCtrl.dispose();
    super.dispose();
  }

  Future<void> _entrar() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _carregando = true);

    const perms = PermissoesLocalizacao();
    await perms.solicitarForeground();

    // TODO(auth): trocar pelo fluxo real CPF + OTP (WhatsApp) — BE-4.
    await ref.read(sessaoProvider.notifier).entrar(
          usuario: _cpfCtrl.text.trim(),
          senha: _senhaCtrl.text,
        );

    if (!mounted) return;
    setState(() => _carregando = false);
    context.go('/rota');
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const SizedBox(height: 40),
                const Icon(
                  Icons.local_hospital,
                  size: 72,
                  color: MaricaTheme.vermelho,
                ),
                const SizedBox(height: 16),
                const Text(
                  'SMS Maricá',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: 28,
                    fontWeight: FontWeight.bold,
                    color: MaricaTheme.vermelho,
                  ),
                ),
                const Text(
                  'Agente de transporte sanitário',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 14, color: MaricaTheme.cinzaTexto),
                ),
                const SizedBox(height: 8),
                const Text(
                  'Primeiro acesso: CPF + código por WhatsApp',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 12, color: Colors.grey),
                ),
                const SizedBox(height: 40),
                TextFormField(
                  controller: _cpfCtrl,
                  keyboardType: TextInputType.number,
                  inputFormatters: [
                    FilteringTextInputFormatter.digitsOnly,
                    LengthLimitingTextInputFormatter(11),
                  ],
                  decoration: const InputDecoration(
                    labelText: 'CPF',
                    prefixIcon: Icon(Icons.badge_outlined),
                  ),
                  validator: (v) => (v == null || v.trim().length != 11)
                      ? 'Informe os 11 dígitos do CPF'
                      : null,
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _senhaCtrl,
                  obscureText: true,
                  decoration: const InputDecoration(
                    labelText: 'Código / senha (modo teste)',
                    prefixIcon: Icon(Icons.lock_outline),
                  ),
                  validator: (v) =>
                      (v == null || v.isEmpty) ? 'Informe o código' : null,
                ),
                const SizedBox(height: 32),
                ElevatedButton(
                  onPressed: _carregando ? null : _entrar,
                  child: _carregando
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            valueColor:
                                AlwaysStoppedAnimation<Color>(Colors.white),
                          ),
                        )
                      : const Text('Entrar'),
                ),
                const SizedBox(height: 16),
                const Text(
                  'Versão 0.1.0 — modo teste',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 12, color: Colors.grey),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
