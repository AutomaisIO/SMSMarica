import 'package:agente/app/theme.dart';
import 'package:agente/features/navegacao/domain/translado_navegacao.dart';
import 'package:flutter/material.dart';

/// Confirmação ao chegar na parada: **uma conferência por pessoa**.
///
/// Paciente com acompanhante são duas pessoas e duas marcações (embarcou /
/// ausente), cada uma com o assento do planejamento. "Embarque concluído" só
/// libera quando todos da parada foram conferidos. Na chegada o app comuta
/// para cá; ao concluir, segue para o próximo waypoint sem recriar a sessão
/// de navegação.
///
/// Fecha com as marcas por id de pessoa (paciente ou
/// [PassageiroTranslado.idAcompanhante]): `true` = embarcou/desembarcou,
/// `false` = ausente. `null` = voltou à navegação sem concluir.
class ConfirmacaoParadaSheet extends StatefulWidget {
  const ConfirmacaoParadaSheet({required this.parada, super.key});

  final ParadaNavegacao parada;

  @override
  State<ConfirmacaoParadaSheet> createState() => _ConfirmacaoParadaSheetState();
}

/// Marca que o motorista deu à pessoa na parada.
enum _Marca { embarcou, ausente }

/// Uma pessoa a conferir: o paciente ou o acompanhante dele.
class _Pessoa {
  const _Pessoa({
    required this.id,
    required this.papel,
    required this.nome,
    this.assento,
  });

  final String id;
  final String papel;
  final String nome;
  final String? assento;
}

class _ConfirmacaoParadaSheetState extends State<ConfirmacaoParadaSheet> {
  /// Marca por pessoa (sem entrada = ainda não conferida).
  final Map<String, _Marca> _marcas = {};

  bool get _isColeta => widget.parada.tipo == TipoParada.coleta;

  List<_Pessoa> _pessoasDe(PassageiroTranslado p) => [
    _Pessoa(id: p.id, papel: 'Paciente', nome: p.nome, assento: p.assento),
    for (var i = 0; i < p.acompanhantes.length; i++)
      _Pessoa(
        id: p.idAcompanhante(i),
        papel: p.acompanhantes.length > 1
            ? 'Acompanhante ${i + 1}'
            : 'Acompanhante',
        nome: p.acompanhantes[i].nome,
        assento: p.acompanhantes[i].assento,
      ),
  ];

  int get _total => widget.parada.passageiros.fold(
    0,
    (n, p) => n + 1 + p.acompanhantes.length,
  );

  @override
  Widget build(BuildContext context) {
    final total = _total;
    final conferidos = _marcas.length;
    final todosConferidos = conferidos == total;
    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
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
            const SizedBox(height: 4),
            const Text(
              'Confira cada pessoa: paciente e acompanhante são marcados '
              'separadamente.',
              style: TextStyle(fontSize: 12.5, color: Colors.black54),
            ),
            const SizedBox(height: 14),
            ...widget.parada.passageiros.map(_cartaoPaciente),
            Center(
              child: Text(
                '$conferidos de $total conferidos',
                style: TextStyle(
                  fontWeight: FontWeight.w600,
                  color: todosConferidos
                      ? Colors.green.shade700
                      : Colors.black54,
                ),
              ),
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => Navigator.of(context).pop(),
                    style: OutlinedButton.styleFrom(
                      minimumSize: const Size.fromHeight(52),
                    ),
                    child: const Text('Voltar à navegação'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  flex: 2,
                  child: ElevatedButton.icon(
                    onPressed: todosConferidos
                        ? () => Navigator.of(context).pop(<String, bool>{
                            for (final e in _marcas.entries)
                              e.key: e.value == _Marca.embarcou,
                          })
                        : null,
                    icon: const Icon(Icons.check),
                    label: Text(
                      _isColeta
                          ? 'Embarque concluído'
                          : 'Desembarque concluído',
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _cartaoPaciente(PassageiroTranslado p) {
    final pessoas = _pessoasDe(p);
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
          if (p.observacao != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Row(
                children: [
                  const Icon(
                    Icons.warning_amber,
                    color: Colors.orange,
                    size: 18,
                  ),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Text(
                      p.observacao!,
                      style: const TextStyle(
                        fontSize: 13,
                        color: Colors.orange,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          for (final (i, pessoa) in pessoas.indexed) ...[
            if (i > 0) const Divider(height: 20),
            _linhaPessoa(pessoa),
          ],
        ],
      ),
    );
  }

  Widget _linhaPessoa(_Pessoa pessoa) {
    final marca = _marcas[pessoa.id];
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
                pessoa.papel,
                style: const TextStyle(fontSize: 11, color: Colors.black54),
              ),
              Text(
                pessoa.nome,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 15,
                ),
              ),
              Text(
                pessoa.assento ?? 'Assento livre',
                style: const TextStyle(fontSize: 13, color: Colors.black87),
              ),
            ],
          ),
        ),
        const SizedBox(width: 8),
        _BotaoMarca(
          rotulo: 'Ausente',
          cor: Colors.red,
          marcado: marca == _Marca.ausente,
          onTap: () => setState(() => _marcas[pessoa.id] = _Marca.ausente),
        ),
        const SizedBox(width: 8),
        _BotaoMarca(
          rotulo: _isColeta ? 'Embarcou' : 'Desembarcou',
          cor: Colors.green.shade700,
          marcado: marca == _Marca.embarcou,
          onTap: () => setState(() => _marcas[pessoa.id] = _Marca.embarcou),
        ),
      ],
    );
  }
}

/// Botão grande de marcação (preenchido quando escolhido).
class _BotaoMarca extends StatelessWidget {
  const _BotaoMarca({
    required this.rotulo,
    required this.cor,
    required this.marcado,
    required this.onTap,
  });

  final String rotulo;
  final Color cor;
  final bool marcado;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 140,
      height: 52,
      child: OutlinedButton(
        onPressed: onTap,
        style: OutlinedButton.styleFrom(
          foregroundColor: marcado ? Colors.white : cor,
          backgroundColor: marcado ? cor : null,
          side: BorderSide(color: cor),
        ),
        child: Text(rotulo),
      ),
    );
  }
}
