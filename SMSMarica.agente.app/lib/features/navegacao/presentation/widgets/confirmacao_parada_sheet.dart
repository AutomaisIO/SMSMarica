import 'package:agente/app/theme.dart';
import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:flutter/material.dart';

/// Confirmação ao chegar na parada.
///
/// Mostra ao motorista a **alocação dos assentos** (paciente + acompanhante,
/// conforme o planejamento) e deixa marcar embarcou / ausente para cada um.
/// Na chegada o app comuta para cá; ao concluir, segue para o próximo
/// waypoint sem recriar a sessão de navegação.
class ConfirmacaoParadaSheet extends StatefulWidget {
  const ConfirmacaoParadaSheet({required this.parada, super.key});

  final ParadaNavegacao parada;

  @override
  State<ConfirmacaoParadaSheet> createState() => _ConfirmacaoParadaSheetState();
}

/// Marca que o motorista deu ao passageiro na parada.
enum _Marca { embarcou, ausente }

class _ConfirmacaoParadaSheetState extends State<ConfirmacaoParadaSheet> {
  /// Marca por id de passageiro (ausente da mapa = ainda não tocou).
  final Map<String, _Marca> _marcas = {};

  bool get _isColeta => widget.parada.tipo == TipoParada.coleta;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 20),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: Colors.black12,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: 14),
            Text(
              _isColeta ? 'Confirmar embarque' : 'Confirmar desembarque',
              style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 18),
            ),
            Text(
              widget.parada.endereco,
              style: const TextStyle(fontSize: 12.5, color: Colors.black54),
            ),
            const SizedBox(height: 16),
            ...widget.parada.passageiros.map(_cartaoPassageiro),
            const SizedBox(height: 8),
            ElevatedButton.icon(
              onPressed: () => Navigator.of(context).pop(true),
              icon: const Icon(Icons.check),
              label: Text(
                _isColeta ? 'Embarque concluído' : 'Desembarque concluído',
              ),
              style: ElevatedButton.styleFrom(
                minimumSize: const Size.fromHeight(52),
              ),
            ),
            const SizedBox(height: 8),
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Voltar à navegação'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _cartaoPassageiro(PassageiroTranslado p) {
    final marca = _marcas[p.id];
    final ausente = marca == _Marca.ausente;
    final embarcou = marca == _Marca.embarcou;
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: MaricaTheme.cinzaFundo,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.person, color: MaricaTheme.vermelho),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  p.nome,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 15,
                  ),
                ),
              ),
            ],
          ),
          if (p.observacao != null) ...[
            const SizedBox(height: 4),
            Text(
              p.observacao!,
              style: const TextStyle(
                fontSize: 12,
                color: Colors.orange,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
          const SizedBox(height: 10),
          _Assento(rotulo: 'Paciente', nome: p.nome, assento: p.assento),
          if (p.temAcompanhante) ...[
            const SizedBox(height: 8),
            _Assento(
              rotulo: 'Acompanhante',
              nome: p.acompanhante!.nome,
              assento: p.acompanhante!.assento,
            ),
          ],
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: () =>
                      setState(() => _marcas[p.id] = _Marca.ausente),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: ausente ? Colors.white : Colors.red,
                    backgroundColor: ausente ? Colors.red : null,
                    side: const BorderSide(color: Colors.red),
                  ),
                  child: const Text('Ausente'),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: OutlinedButton(
                  onPressed: () =>
                      setState(() => _marcas[p.id] = _Marca.embarcou),
                  style: OutlinedButton.styleFrom(
                    foregroundColor:
                        embarcou ? Colors.white : Colors.green.shade700,
                    backgroundColor: embarcou ? Colors.green : null,
                    side: BorderSide(color: Colors.green.shade700),
                  ),
                  child: Text(_isColeta ? 'Embarcou' : 'Desembarcou'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Linha visual do assento alocado.
class _Assento extends StatelessWidget {
  const _Assento({required this.rotulo, required this.nome, this.assento});

  final String rotulo;
  final String nome;
  final String? assento;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(
              color: MaricaTheme.vermelho.withValues(alpha: 0.4),
            ),
          ),
          child: const Icon(Icons.event_seat, color: MaricaTheme.vermelho),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                rotulo,
                style: const TextStyle(fontSize: 11, color: Colors.black54),
              ),
              Text(
                assento ?? 'Assento livre',
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 13.5,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
