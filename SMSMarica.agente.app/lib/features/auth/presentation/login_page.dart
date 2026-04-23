import 'package:agente/app/theme.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:agente/shared/permissoes/permissoes_localizacao.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _usuarioCtrl = TextEditingController();
  final _senhaCtrl = TextEditingController();
  final _formKey = GlobalKey<FormState>();
  bool _carregando = false;

  @override
  void dispose() {
    _usuarioCtrl.dispose();
    _senhaCtrl.dispose();
    super.dispose();
  }

  Future<void> _entrar() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _carregando = true);

    const perms = PermissoesLocalizacao();
    await perms.solicitarForeground();

    await ref.read(sessaoProvider.notifier).entrar(
          usuario: _usuarioCtrl.text.trim(),
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
                  style: TextStyle(
                    fontSize: 14,
                    color: MaricaTheme.cinzaTexto,
                  ),
                ),
                const SizedBox(height: 48),
                TextFormField(
                  controller: _usuarioCtrl,
                  decoration: const InputDecoration(
                    labelText: 'Usuário',
                    prefixIcon: Icon(Icons.person_outline),
                  ),
                  validator: (v) => (v == null || v.trim().isEmpty)
                      ? 'Informe o usuário'
                      : null,
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _senhaCtrl,
                  obscureText: true,
                  decoration: const InputDecoration(
                    labelText: 'Senha',
                    prefixIcon: Icon(Icons.lock_outline),
                  ),
                  validator: (v) =>
                      (v == null || v.isEmpty) ? 'Informe a senha' : null,
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
                  'Versão 0.1.0 — mock',
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
