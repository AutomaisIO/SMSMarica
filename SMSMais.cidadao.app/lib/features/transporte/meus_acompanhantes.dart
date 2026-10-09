import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

String _mensagem(Object e) {
  if (classificarErro(e).status == 429) {
    return 'Você chegou ao limite de consultas de hoje. Tente de novo amanhã ou fale com a equipe do transporte.';
  }
  return extrairMensagemDeErro(e);
}

/// Acompanhantes do paciente no transporte (`MeusAcompanhantes.tsx`). O próprio paciente
/// cadastra pelo CPF e pela data de nascimento — o sistema confere e traz o nome. Quem a equipe
/// cadastrou só a equipe tira.
class MeusAcompanhantes extends ConsumerStatefulWidget {
  const MeusAcompanhantes({super.key});

  @override
  ConsumerState<MeusAcompanhantes> createState() => _MeusAcompanhantesState();
}

class _MeusAcompanhantesState extends ConsumerState<MeusAcompanhantes> {
  List<AcompanhanteCidadao>? _lista;
  String? _erroLista;
  bool _adicionando = false;
  String? _confirmandoId;
  bool _removendo = false;
  String? _erroRemover;

  @override
  void initState() {
    super.initState();
    _carregar();
  }

  Future<void> _carregar() async {
    setState(() => _erroLista = null);
    try {
      final lista = await ref.read(apiProvider).acompanhantes();
      if (mounted) setState(() => _lista = lista);
    } on Object catch (e) {
      if (mounted) setState(() => _erroLista = extrairMensagemDeErro(e));
    }
  }

  Future<void> _remover(AcompanhanteCidadao a) async {
    setState(() {
      _erroRemover = null;
      _removendo = true;
    });
    try {
      await ref.read(apiProvider).removerAcompanhante(a.id);
      if (!mounted) return;
      setState(() => _confirmandoId = null);
      await _carregar();
    } on Object catch (e) {
      if (mounted) setState(() => _erroRemover = _mensagem(e));
    } finally {
      if (mounted) setState(() => _removendo = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final Widget conteudo;
    if (_erroLista != null) {
      conteudo = ErroCard(mensagem: _erroLista!, aoTentar: _carregar);
    } else if (_lista == null) {
      conteudo = const Esqueleto(altura: 64);
    } else if (_lista!.isEmpty) {
      conteudo = BordaTracejada(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
          child: Text(
            'Nenhum acompanhante cadastrado ainda.',
            textAlign: TextAlign.center,
            style: Txt.sans(14, cor: CoresMarica.tintaMute),
          ),
        ),
      );
    } else {
      conteudo = Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (var i = 0; i < _lista!.length; i++) ...[
            if (i > 0) const SizedBox(height: 8),
            _cartao(_lista![i]),
          ],
        ],
      );
    }

    return Padding(
      padding: const EdgeInsets.only(top: 32),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              const Icon(LucideIcons.users, size: 20, color: CoresMarica.marica),
              const SizedBox(width: 8),
              Text('Meus acompanhantes', style: Txt.display(18)),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            'Quem pode ir com você nas viagens. Em cada viagem vai 1 acompanhante (2 só com liberação da equipe).',
            style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43),
          ),
          const SizedBox(height: 12),
          conteudo,
          const SizedBox(height: 12),
          if (_adicionando)
            _FormularioAcompanhante(
              aoCancelar: () => setState(() => _adicionando = false),
              aoConcluir: () {
                setState(() => _adicionando = false);
                _carregar();
              },
            )
          else
            BotaoFantasma(
              rotulo: 'Adicionar acompanhante',
              icone: LucideIcons.userPlus,
              onPressed: () => setState(() => _adicionando = true),
            ),
        ],
      ),
    );
  }

  Widget _cartao(AcompanhanteCidadao a) {
    final parentesco = rotuloParentesco(a.parentesco);
    final confirmando = _confirmandoId == a.id;
    return Cartao(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      a.nome,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: Txt.sans(16, peso: FontWeight.w600),
                    ),
                    Text(
                      '${parentesco != null ? '$parentesco · ' : ''}CPF ${cpfParcial(a.cpf)}'
                      '${a.origem == 'Painel' ? ' · cadastrado pela equipe' : ''}',
                      style: Txt.sans(12, cor: CoresMarica.tintaMute),
                    ),
                  ],
                ),
              ),
              if (a.origem == 'App' && !confirmando)
                IconButton(
                  onPressed: () => setState(() {
                    _confirmandoId = a.id;
                    _erroRemover = null;
                  }),
                  tooltip: 'Tirar ${a.nome} da lista',
                  style: IconButton.styleFrom(
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(RaiosMarica.xl)),
                    highlightColor: CoresMarica.vermelho50,
                  ),
                  icon: const Icon(LucideIcons.trash2, size: 20, color: CoresMarica.vermelho700),
                ),
            ],
          ),
          if (confirmando)
            Container(
              margin: const EdgeInsets.only(top: 12),
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: CoresMarica.papel,
                borderRadius: BorderRadius.circular(RaiosMarica.xl),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text('Tirar ${a.nome} da sua lista?', style: Txt.sans(14, altura: 1.43)),
                  if (_erroRemover != null) ...[
                    const SizedBox(height: 8),
                    Text(_erroRemover!, style: Txt.sans(14, cor: CoresMarica.vermelho700, altura: 1.43)),
                  ],
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Expanded(
                        child: BotaoFantasma(rotulo: 'Não', onPressed: () => setState(() => _confirmandoId = null)),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: BotaoPrimario(
                          rotulo: 'Tirar da lista',
                          carregando: _removendo,
                          onPressed: () => _remover(a),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _FormularioAcompanhante extends ConsumerStatefulWidget {
  const _FormularioAcompanhante({required this.aoCancelar, required this.aoConcluir});

  final VoidCallback aoCancelar;
  final VoidCallback aoConcluir;

  @override
  ConsumerState<_FormularioAcompanhante> createState() => _FormularioAcompanhanteState();
}

class _FormularioAcompanhanteState extends ConsumerState<_FormularioAcompanhante> {
  final _cpf = TextEditingController();
  final _nascimento = TextEditingController();
  String _parentesco = '';
  ConsultaAcompanhante? _encontrado;
  String? _erro;
  bool _carregando = false;

  @override
  void initState() {
    super.initState();
    _cpf.addListener(() => setState(() {}));
    _nascimento.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _cpf.dispose();
    _nascimento.dispose();
    super.dispose();
  }

  String? get _nascimentoIso => dataParaIso(_nascimento.text);
  bool get _cpfCompleto => digitosCpf(_cpf.text).length == 11;
  bool get _cpfErrado => _cpfCompleto && !cpfValido(_cpf.text);
  bool get _podeConferir => _cpfCompleto && !_cpfErrado && _nascimentoIso != null;

  Future<void> _conferir() async {
    final iso = _nascimentoIso;
    if (iso == null) return;
    setState(() {
      _erro = null;
      _carregando = true;
    });
    try {
      final r = await ref.read(apiProvider).consultarAcompanhante(digitosCpf(_cpf.text), iso);
      if (mounted) setState(() => _encontrado = r);
    } on Object catch (e) {
      if (mounted) setState(() => _erro = _mensagem(e));
    } finally {
      if (mounted) setState(() => _carregando = false);
    }
  }

  Future<void> _confirmar() async {
    final iso = _nascimentoIso;
    if (iso == null) return;
    setState(() {
      _erro = null;
      _carregando = true;
    });
    try {
      await ref
          .read(apiProvider)
          .adicionarAcompanhante(digitosCpf(_cpf.text), iso, _parentesco.isEmpty ? null : _parentesco);
      widget.aoConcluir();
    } on Object catch (e) {
      if (mounted) setState(() => _erro = _mensagem(e));
    } finally {
      if (mounted) setState(() => _carregando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final encontrado = _encontrado;
    return Cartao(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (encontrado != null)
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const Icon(LucideIcons.circleCheck, size: 16, color: CoresMarica.lagoaEscuro),
                    const SizedBox(width: 8),
                    Text('Encontramos', style: Txt.sans(14, peso: FontWeight.w500, cor: CoresMarica.lagoaEscuro)),
                  ],
                ),
                const SizedBox(height: 4),
                Text(encontrado.nome, style: Txt.sans(18, peso: FontWeight.w600)),
                const SizedBox(height: 4),
                Text(
                  encontrado.jaCadastrado
                      ? 'Essa pessoa já está na sua lista.'
                      : 'É essa pessoa? Confirme para ela entrar na sua lista.',
                  style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43),
                ),
              ],
            )
          else ...[
            _CampoForm(
              rotulo: 'CPF do acompanhante',
              controller: _cpf,
              dica: '000.000.000-00',
              formatadores: [MascaraFormatter(mascararCpf)],
              erro: _cpfErrado ? 'CPF inválido. Confira os números.' : null,
            ),
            const SizedBox(height: 12),
            _CampoForm(
              rotulo: 'Data de nascimento',
              controller: _nascimento,
              dica: 'dd/mm/aaaa',
              formatadores: [MascaraFormatter(mascararData)],
            ),
            const SizedBox(height: 12),
            _rotulo('Parentesco (opcional)'),
            DecoratedBox(
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(RaiosMarica.x2l),
                boxShadow: SombrasMarica.carta,
              ),
              child: DropdownButtonFormField<String>(
                initialValue: _parentesco,
                isExpanded: true,
                onChanged: (v) => setState(() => _parentesco = v ?? ''),
                decoration: decoracaoCampo(),
                style: Txt.sans(16),
                dropdownColor: CoresMarica.branco,
                borderRadius: BorderRadius.circular(RaiosMarica.xl),
                icon: const Icon(LucideIcons.chevronDown, size: 20, color: CoresMarica.tintaMute),
                items: [
                  const DropdownMenuItem(value: '', child: Text('Não informar')),
                  for (final p in parentescos) DropdownMenuItem(value: p.$1, child: Text(p.$2)),
                ],
              ),
            ),
          ],
          if (_erro != null) ...[
            const SizedBox(height: 12),
            Semantics(
              liveRegion: true,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                decoration: BoxDecoration(
                  color: CoresMarica.vermelho50,
                  borderRadius: BorderRadius.circular(RaiosMarica.xl),
                ),
                child: Text(_erro!, style: Txt.sans(14, cor: CoresMarica.vermelho700, altura: 1.43)),
              ),
            ),
          ],
          const SizedBox(height: 12),
          Row(
            children: encontrado != null
                ? [
                    Expanded(
                      child: BotaoFantasma(
                        rotulo: 'Corrigir',
                        onPressed: () => setState(() {
                          _encontrado = null;
                          _erro = null;
                        }),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: encontrado.jaCadastrado
                          ? BotaoPrimario(rotulo: 'Fechar', onPressed: widget.aoConcluir)
                          : BotaoPrimario(rotulo: 'Confirmar', carregando: _carregando, onPressed: _confirmar),
                    ),
                  ]
                : [
                    Expanded(child: BotaoFantasma(rotulo: 'Cancelar', onPressed: widget.aoCancelar)),
                    const SizedBox(width: 8),
                    Expanded(
                      child: BotaoPrimario(
                        rotulo: 'Conferir',
                        carregando: _carregando,
                        onPressed: _podeConferir ? _conferir : null,
                      ),
                    ),
                  ],
          ),
        ],
      ),
    );
  }
}

Widget _rotulo(String texto) => Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Text(texto, style: Txt.sans(14, peso: FontWeight.w500)),
    );

/// Campo numérico com máscara, sombra `shadow-carta` e halo de foco (o `CAMPO` do PWA).
class _CampoForm extends StatefulWidget {
  const _CampoForm({
    required this.rotulo,
    required this.controller,
    required this.dica,
    required this.formatadores,
    this.erro,
  });

  final String rotulo;
  final TextEditingController controller;
  final String dica;
  final List<TextInputFormatter> formatadores;
  final String? erro;

  @override
  State<_CampoForm> createState() => _CampoFormState();
}

class _CampoFormState extends State<_CampoForm> {
  final _foco = FocusNode();

  @override
  void dispose() {
    _foco.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _rotulo(widget.rotulo),
        HaloFoco(
          foco: _foco,
          child: TextField(
            controller: widget.controller,
            focusNode: _foco,
            keyboardType: TextInputType.number,
            autocorrect: false,
            enableSuggestions: false,
            inputFormatters: widget.formatadores,
            style: Txt.sans(16, recursos: Txt.tabular),
            decoration: decoracaoCampo(dica: widget.dica),
          ),
        ),
        if (widget.erro != null)
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Text(widget.erro!, style: Txt.sans(12, cor: CoresMarica.vermelho700)),
          ),
      ],
    );
  }
}
