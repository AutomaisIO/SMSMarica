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
    await ref
        .read(sessaoProvider.notifier)
        .entrar(usuario: _cpfCtrl.text.trim(), senha: _senhaCtrl.text);

    if (!mounted) return;
    setState(() => _carregando = false);
    context.go('/deslocamento');
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, area) {
            // Tablet do carro em paisagem: marca à esquerda, formulário à
            // direita. Rolável sempre: com o teclado aberto sobra pouca
            // altura e a coluna única estourava ("BOTTOM OVERFLOWED").
            final paisagem =
                area.maxWidth > area.maxHeight && area.maxWidth >= 720;
            return Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.symmetric(
                  horizontal: 32,
                  vertical: 24,
                ),
                child: ConstrainedBox(
                  constraints: BoxConstraints(maxWidth: paisagem ? 900 : 480),
                  child: paisagem
                      ? Row(
                          children: [
                            const Expanded(child: _Marca()),
                            const SizedBox(width: 48),
                            Expanded(child: _formulario()),
                          ],
                        )
                      : Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            const _Marca(),
                            const SizedBox(height: 32),
                            _formulario(),
                          ],
                        ),
                ),
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _formulario() {
    return Form(
      key: _formKey,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
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
                      valueColor: AlwaysStoppedAnimation<Color>(Colors.white),
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
    );
  }
}

/// Logo e identificação: à esquerda em paisagem, no topo em retrato.
class _Marca extends StatelessWidget {
  const _Marca();

  @override
  Widget build(BuildContext context) {
    return const Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.local_hospital, size: 72, color: MaricaTheme.vermelho),
        SizedBox(height: 16),
        Text(
          'SMS Maricá',
          textAlign: TextAlign.center,
          style: TextStyle(
            fontSize: 28,
            fontWeight: FontWeight.bold,
            color: MaricaTheme.vermelho,
          ),
        ),
        Text(
          'Agente de transporte sanitário',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 14, color: MaricaTheme.cinzaTexto),
        ),
        SizedBox(height: 8),
        Text(
          'Primeiro acesso: CPF + código por WhatsApp',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 12, color: Colors.grey),
        ),
      ],
    );
  }
}
