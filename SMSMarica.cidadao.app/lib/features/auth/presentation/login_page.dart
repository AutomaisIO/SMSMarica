import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:sms_marica_cidadao/features/auth/presentation/auth_controller.dart';
import 'package:sms_marica_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_marica_cidadao/shared/widgets/logo_marica.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _cpfController = TextEditingController();
  final _senhaController = TextEditingController();
  final _formKey = GlobalKey<FormState>();

  @override
  void dispose() {
    _cpfController.dispose();
    _senhaController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(authControllerProvider, (previous, next) {
      switch (next) {
        case AutenticacaoEntrada():
          context.go('/perfil');
        case AutenticacaoFalhou(:final mensagem):
          ScaffoldMessenger.of(context)
            ..clearSnackBars()
            ..showSnackBar(
              SnackBar(
                content: Text(mensagem),
                backgroundColor: CoresMarica.vermelhoEscuro,
              ),
            );
        case AutenticacaoInicial() || AutenticacaoCarregando():
          break;
      }
    });

    final estado = ref.watch(authControllerProvider);
    final carregando = estado is AutenticacaoCarregando;

    return Scaffold(
      backgroundColor: CoresMarica.vermelhoPrincipal,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Padding(
                  padding: EdgeInsets.only(bottom: 32),
                  child: Center(
                    child: LogoMarica(altura: 64, sobreVermelho: true),
                  ),
                ),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Form(
                      key: _formKey,
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          const Text(
                            'Acessar minha conta',
                            style: TextStyle(
                              fontSize: 20,
                              fontWeight: FontWeight.w700,
                              color: CoresMarica.preto,
                            ),
                          ),
                          const SizedBox(height: 6),
                          const Text(
                            'Use seu CPF e a senha cadastrada na Secretaria de Saúde.',
                            style: TextStyle(
                              fontSize: 14,
                              color: CoresMarica.cinzaTexto,
                            ),
                          ),
                          const SizedBox(height: 20),
                          TextFormField(
                            controller: _cpfController,
                            keyboardType: TextInputType.number,
                            inputFormatters: [
                              FilteringTextInputFormatter.digitsOnly,
                              LengthLimitingTextInputFormatter(11),
                            ],
                            decoration: const InputDecoration(
                              labelText: 'CPF',
                              hintText: '000.000.000-00',
                            ),
                            validator: (v) {
                              final digitos = (v ?? '').replaceAll(
                                RegExp(r'\D'),
                                '',
                              );
                              if (digitos.length != 11) {
                                return 'Informe um CPF válido.';
                              }
                              return null;
                            },
                          ),
                          const SizedBox(height: 12),
                          TextFormField(
                            controller: _senhaController,
                            obscureText: true,
                            decoration: const InputDecoration(
                              labelText: 'Senha',
                            ),
                            validator: (v) {
                              if ((v ?? '').isEmpty) {
                                return 'Informe a senha.';
                              }
                              return null;
                            },
                          ),
                          const SizedBox(height: 24),
                          ElevatedButton(
                            onPressed: carregando ? null : _submeter,
                            child: carregando
                                ? const SizedBox(
                                    height: 22,
                                    width: 22,
                                    child: CircularProgressIndicator(
                                      color: CoresMarica.branco,
                                      strokeWidth: 2.5,
                                    ),
                                  )
                                : const Text('Entrar'),
                          ),
                          const SizedBox(height: 8),
                          TextButton(
                            onPressed: () {},
                            child: const Text('Esqueci minha senha'),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
                const SizedBox(height: 16),
                Text(
                  'v0.1.0 · ambiente de desenvolvimento',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: CoresMarica.branco.withValues(alpha: 0.75),
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _submeter() {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    FocusScope.of(context).unfocus();
    ref.read(authControllerProvider.notifier).entrar(
          cpf: _cpfController.text,
          senha: _senhaController.text,
        );
  }
}
