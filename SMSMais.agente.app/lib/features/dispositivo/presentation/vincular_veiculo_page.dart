import 'package:agente/features/dispositivo/application/dispositivo_controller.dart';
import 'package:agente/features/dispositivo/data/dispositivo_repository.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

/// Vincula este tablet a um veículo com o código gerado no painel
/// (Veículos › editar › Tablet vinculado).
class VincularVeiculoPage extends ConsumerStatefulWidget {
  const VincularVeiculoPage({super.key});

  @override
  ConsumerState<VincularVeiculoPage> createState() =>
      _VincularVeiculoPageState();
}

class _VincularVeiculoPageState extends ConsumerState<VincularVeiculoPage> {
  final _codigoCtrl = TextEditingController();
  bool _enviando = false;
  String? _erro;

  @override
  void dispose() {
    _codigoCtrl.dispose();
    super.dispose();
  }

  Future<void> _vincular() async {
    final codigo = _codigoCtrl.text.trim().toUpperCase();
    if (codigo.length != 8) {
      setState(() => _erro = 'O código tem 8 caracteres.');
      return;
    }
    setState(() {
      _enviando = true;
      _erro = null;
    });
    try {
      final veiculo = await ref
          .read(dispositivoProvider.notifier)
          .ativar(codigo);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Tablet vinculado ao veículo ${veiculo.descricao}.'),
        ),
      );
      context.go('/deslocamento');
    } on CodigoAtivacaoInvalido {
      setState(
        () => _erro =
            'Código inválido, expirado ou já usado. Gere outro no painel.',
      );
    } on Object catch (_) {
      setState(() => _erro = 'Sem conexão com o servidor. Tente de novo.');
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  Future<void> _desvincular() async {
    await ref.read(dispositivoProvider.notifier).desvincularLocal();
    if (mounted) setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(dispositivoProvider);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Vincular veículo')),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (estado.vinculado) ...[
                  Card(
                    child: ListTile(
                      leading: const Icon(Icons.directions_car, size: 36),
                      title: Text(
                        estado.veiculo?.descricao ?? 'Veículo vinculado',
                      ),
                      subtitle: const Text('Este tablet já está vinculado.'),
                      trailing: TextButton(
                        onPressed: _desvincular,
                        child: const Text('Desvincular'),
                      ),
                    ),
                  ),
                  const SizedBox(height: 24),
                  Text(
                    'Para trocar de veículo, gere um código novo no painel '
                    'e digite abaixo.',
                    style: theme.textTheme.bodyMedium,
                  ),
                ] else
                  Text(
                    'No painel do SMSMais, abra Veículos › editar › '
                    'Tablet vinculado, '
                    'gere o código de ativação e digite aqui.',
                    style: theme.textTheme.bodyLarge,
                  ),
                const SizedBox(height: 16),
                TextField(
                  controller: _codigoCtrl,
                  autofocus: !estado.vinculado,
                  textCapitalization: TextCapitalization.characters,
                  maxLength: 8,
                  textAlign: TextAlign.center,
                  style: theme.textTheme.headlineMedium?.copyWith(
                    letterSpacing: 6,
                    fontWeight: FontWeight.w700,
                  ),
                  inputFormatters: [
                    FilteringTextInputFormatter.allow(RegExp('[a-zA-Z0-9]')),
                    TextInputFormatter.withFunction(
                      (_, novo) => novo.copyWith(text: novo.text.toUpperCase()),
                    ),
                  ],
                  decoration: InputDecoration(
                    labelText: 'Código de ativação',
                    errorText: _erro,
                    border: const OutlineInputBorder(),
                  ),
                  onSubmitted: (_) => _vincular(),
                ),
                const SizedBox(height: 16),
                ElevatedButton.icon(
                  onPressed: _enviando ? null : _vincular,
                  icon: _enviando
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.link),
                  label: const Text('Vincular'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
