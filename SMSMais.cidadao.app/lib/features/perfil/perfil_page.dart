import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/features/perfil/avatar_uploader.dart';
import 'package:sms_mais_cidadao/features/perfil/perfil_provider.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/documentos_br.dart';
import 'package:sms_mais_cidadao/shared/util/formatos.dart';
import 'package:sms_mais_cidadao/shared/widgets/lista.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Meu perfil (`Perfil.tsx`): foto, cadastro oficial (somente leitura) e contatos editáveis. O
/// celular só troca por código enviado ao número NOVO.
class PerfilPage extends ConsumerStatefulWidget {
  const PerfilPage({super.key});

  @override
  ConsumerState<PerfilPage> createState() => _PerfilPageState();
}

class _PerfilPageState extends ConsumerState<PerfilPage> {
  final _email = TextEditingController();
  final _residencial = TextEditingController();
  String _celular = '';
  bool _salvando = false;
  bool _salvandoFoto = false;
  bool _celularTrocado = false;
  bool _ok = false;
  String? _erro;
  Timer? _timerOk;
  Timer? _timerTrocado;

  @override
  void initState() {
    super.initState();
    final p = ref.read(perfilProvider).valueOrNull;
    if (p != null) _preencher(p);
  }

  @override
  void dispose() {
    _email.dispose();
    _residencial.dispose();
    _timerOk?.cancel();
    _timerTrocado?.cancel();
    super.dispose();
  }

  void _preencher(Perfil p) {
    _email.text = p.email ?? '';
    _celular = p.telefoneCelular ?? p.telefonePrincipal ?? '';
    _residencial.text = p.telefoneResidencial ?? '';
  }

  Future<void> _trocarFoto(String base64) async {
    setState(() {
      _salvandoFoto = true;
      _erro = null;
    });
    try {
      await ref.read(apiProvider).salvarFoto(base64);
      final p = ref.read(perfilProvider).valueOrNull;
      if (p != null) ref.read(perfilProvider.notifier).definir(p.copiar(fotoBase64: base64));
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _salvandoFoto = false);
    }
  }

  Future<void> _salvar() async {
    FocusScope.of(context).unfocus();
    setState(() {
      _salvando = true;
      _erro = null;
      _ok = false;
    });
    final email = _email.text.trim();
    final celular = _celular.trim();
    final residencial = _residencial.text.trim();
    try {
      await ref.read(apiProvider).salvarContato(
            email: email.isEmpty ? null : email,
            telefonePrincipal: celular.isEmpty ? null : celular,
            telefoneCelular: celular.isEmpty ? null : celular,
            telefoneResidencial: residencial.isEmpty ? null : residencial,
          );
      final p = ref.read(perfilProvider).valueOrNull;
      if (p != null) {
        ref.read(perfilProvider.notifier).definir(
              p.copiar(
                email: email.isEmpty ? null : email,
                limparEmail: email.isEmpty,
                telefoneCelular: celular.isEmpty ? null : celular,
                telefonePrincipal: celular.isEmpty ? null : celular,
                telefoneResidencial: residencial.isEmpty ? null : residencial,
                limparResidencial: residencial.isEmpty,
              ),
            );
      }
      if (!mounted) return;
      setState(() => _ok = true);
      _timerOk?.cancel();
      _timerOk = Timer(const Duration(milliseconds: 2500), () {
        if (mounted) setState(() => _ok = false);
      });
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _salvando = false);
    }
  }

  Future<void> _trocarCelular() async {
    final novo = await abrirFolha<String>(context, builder: (_) => const _TrocaCelularFolha());
    if (novo == null || !mounted) return;
    setState(() {
      _celular = novo;
      _celularTrocado = true;
    });
    _timerTrocado?.cancel();
    _timerTrocado = Timer(const Duration(seconds: 4), () {
      if (mounted) setState(() => _celularTrocado = false);
    });
    await ref.read(perfilProvider.notifier).recarregar();
  }

  @override
  Widget build(BuildContext context) {
    // O PWA reaplica os campos sempre que o perfil muda (carregou, trocou foto, recarregou).
    ref.listen<AsyncValue<Perfil>>(perfilProvider, (anterior, proximo) {
      final p = proximo.valueOrNull;
      if (p != null && !identical(p, anterior?.valueOrNull)) setState(() => _preencher(p));
    });

    final estado = ref.watch(perfilProvider);
    final perfil = estado.valueOrNull;

    if (perfil == null) {
      if (estado.hasError && !estado.isLoading) {
        return CorpoPagina(
          children: [
            const SizedBox(height: 16),
            const CabecalhoSecao(titulo: 'Meu perfil'),
            ErroCard(
              mensagem: 'Não foi possível carregar seu perfil. ${extrairMensagemDeErro(estado.error!)}',
              aoTentar: () => ref.read(perfilProvider.notifier).recarregar(),
            ),
          ],
        );
      }
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 96),
        child: Align(alignment: Alignment.topCenter, child: Girando()),
      );
    }

    final nome = perfil.nomeExibicao;
    final cns = perfil.cns;
    final nascimento = formatarDataPura(perfil.dataNascimento);

    return CorpoPagina(
      espaco: 24,
      children: [
        Padding(
          padding: const EdgeInsets.only(top: 8),
          child: Column(
            children: [
              AvatarUploader(valor: perfil.fotoBase64, nome: nome, aoMudar: _trocarFoto, salvando: _salvandoFoto),
              const SizedBox(height: 16),
              Text(nome, textAlign: TextAlign.center, style: Txt.display(24, altura: 1.33)),
              Text(
                _salvandoFoto ? 'Salvando foto…' : 'Toque na câmera para trocar a foto',
                textAlign: TextAlign.center,
                style: Txt.sans(14, cor: CoresMarica.tintaMute),
              ),
            ],
          ),
        ),

        // Dados oficiais (somente leitura)
        Cartao(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                decoration: BoxDecoration(
                  color: CoresMarica.papel.withValues(alpha: 0.6),
                  border: const Border(bottom: BorderSide(color: CoresMarica.areia)),
                ),
                child: Row(
                  children: [
                    const Icon(LucideIcons.badgeCheck, size: 16, color: CoresMarica.lagoa),
                    const SizedBox(width: 8),
                    Text(
                      'CADASTRO OFICIAL',
                      style: Txt.sans(12, peso: FontWeight.w600, cor: CoresMarica.tintaMute, espacamento: 0.6),
                    ),
                  ],
                ),
              ),
              ..._linhas([
                ('Nome', perfil.nome),
                ('CPF', formatarCpf(perfil.cpf)),
                if (cns != null && cns.isNotEmpty) ('Cartão SUS', cns),
                if (nascimento != null) ('Nascimento', nascimento),
              ]),
            ],
          ),
        ),

        // Contatos (editáveis)
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const CabecalhoSecao(sobretitulo: 'Como falamos com você', titulo: 'Contatos'),
            const SizedBox(height: 4),
            Campo(
              rotulo: 'E-mail',
              controller: _email,
              dica: 'Seu e-mail',
              teclado: TextInputType.emailAddress,
              autofill: const [AutofillHints.email],
            ),
            const SizedBox(height: 16),
            _blocoCelular(),
            const SizedBox(height: 16),
            Campo(
              rotulo: 'Telefone fixo (opcional)',
              controller: _residencial,
              dica: '(21) 0000-0000',
              teclado: TextInputType.phone,
            ),
            if (_erro != null) ...[const SizedBox(height: 16), TextoErro(_erro!)],
            const SizedBox(height: 16),
            BotaoPrimario(
              rotulo: _ok ? 'Salvo' : 'Salvar alterações',
              icone: _ok ? LucideIcons.check : null,
              carregando: _salvando,
              onPressed: _salvar,
            ),
          ],
        ),
      ],
    );
  }

  /// Celular de contato: troca só por OTP (confirma o código no número novo).
  Widget _blocoCelular() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: CoresMarica.branco,
        borderRadius: BorderRadius.circular(RaiosMarica.x2l),
        border: Border.all(color: CoresMarica.areia),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Celular / WhatsApp',
                      style: Txt.sans(12, peso: FontWeight.w500, cor: CoresMarica.tintaMute),
                    ),
                    Text(
                      _celular.isEmpty ? 'Não informado' : _celular,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: Txt.sans(14, peso: FontWeight.w600),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Pressionavel(
                onTap: _trocarCelular,
                escala: 0.95,
                semantica: 'Trocar',
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                  decoration: BoxDecoration(
                    color: CoresMarica.marica.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(RaiosMarica.xl),
                  ),
                  child: Text('Trocar', style: Txt.sans(14, peso: FontWeight.w600, cor: CoresMarica.marica)),
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          if (_celularTrocado)
            Row(
              children: [
                const Icon(LucideIcons.check, size: 14, color: CoresMarica.lagoa),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    'Número atualizado e verificado — já salvo.',
                    style: Txt.sans(12, peso: FontWeight.w500, cor: CoresMarica.lagoa),
                  ),
                ),
              ],
            )
          else
            Text(
              'É neste número que enviamos seu código de acesso e avisos. Para trocar, você confirma um '
              'código enviado ao número novo — assim não corremos o risco de perder o contato.',
              style: Txt.sans(12, cor: CoresMarica.tintaMute, altura: 1.33),
            ),
        ],
      ),
    );
  }

  List<Widget> _linhas(List<(String, String)> linhas) => [
        for (var i = 0; i < linhas.length; i++)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            decoration: i == 0
                ? null
                : const BoxDecoration(border: Border(top: BorderSide(color: CoresMarica.areia))),
            child: Row(
              children: [
                Text(linhas[i].$1, style: Txt.sans(14, cor: CoresMarica.tintaMute)),
                const SizedBox(width: 16),
                Expanded(
                  child: Text(
                    linhas[i].$2,
                    textAlign: TextAlign.right,
                    style: Txt.sans(14, peso: FontWeight.w500),
                  ),
                ),
              ],
            ),
          ),
      ];
}

/// Troca do celular em 2 passos: (1) informar o número novo → recebe um código nele; (2) digitar
/// o código → só então a troca é salva. Evita cadastrar número errado e ficar sem contato.
/// Devolve (pelo `pop`) o número já verificado.
class _TrocaCelularFolha extends ConsumerStatefulWidget {
  const _TrocaCelularFolha();

  @override
  ConsumerState<_TrocaCelularFolha> createState() => _TrocaCelularFolhaState();
}

class _TrocaCelularFolhaState extends ConsumerState<_TrocaCelularFolha> {
  final _numero = TextEditingController();
  final _codigo = TextEditingController();
  bool _passoCodigo = false;
  String _canon = '';
  String? _mascara;
  bool _ocupado = false;
  String? _erro;

  @override
  void dispose() {
    _numero.dispose();
    _codigo.dispose();
    super.dispose();
  }

  Future<void> _enviar() async {
    final canon = normalizarCelularBr(_numero.text);
    // 55 + DDD(2) + número(>=8) = 12 dígitos no mínimo.
    if (canon.length < 12) {
      setState(() => _erro = 'Informe um celular válido (com DDD).');
      return;
    }
    setState(() {
      _ocupado = true;
      _erro = null;
    });
    try {
      final r = await ref.read(apiProvider).solicitarOtpContato(canon);
      if (!mounted) return;
      setState(() {
        _canon = canon;
        _mascara = r.mascara;
        _passoCodigo = true;
      });
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _ocupado = false);
    }
  }

  Future<void> _confirmar() async {
    if (soDigitos(_codigo.text).length < 4) {
      setState(() => _erro = 'Digite o código recebido.');
      return;
    }
    setState(() {
      _ocupado = true;
      _erro = null;
    });
    try {
      final r = await ref.read(apiProvider).confirmarContato(_canon, _codigo.text.trim());
      if (mounted) Navigator.of(context).pop(r.numero);
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _ocupado = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: EdgeInsets.fromLTRB(24, 24, 24, MediaQuery.paddingOf(context).bottom + 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          const CabecalhoSecao(sobretitulo: 'Segurança', titulo: 'Trocar celular'),
          const SizedBox(height: 4),
          if (!_passoCodigo) ...[
            Campo(
              rotulo: 'Novo celular / WhatsApp',
              controller: _numero,
              dica: '(21) 90000-0000',
              ajuda: 'Enviaremos um código por WhatsApp para este número.',
              teclado: TextInputType.phone,
              autofill: const [AutofillHints.telephoneNumber],
              autofocus: true,
            ),
            if (_erro != null) ...[const SizedBox(height: 16), TextoErro(_erro!)],
            const SizedBox(height: 16),
            BotaoPrimario(rotulo: 'Enviar código', carregando: _ocupado, onPressed: _enviar),
          ] else ...[
            Text(
              'Enviamos um código para ${_mascara ?? 'o número novo'}. Digite-o para confirmar a troca.',
              style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43),
            ),
            const SizedBox(height: 16),
            Campo(
              rotulo: 'Código',
              controller: _codigo,
              dica: '000000',
              teclado: TextInputType.number,
              autofill: const [AutofillHints.oneTimeCode],
              autofocus: true,
            ),
            if (_erro != null) ...[const SizedBox(height: 16), TextoErro(_erro!)],
            const SizedBox(height: 16),
            BotaoPrimario(rotulo: 'Confirmar troca', carregando: _ocupado, onPressed: _confirmar),
            const SizedBox(height: 8),
            LinkDiscreto(
              rotulo: 'Corrigir o número',
              onTap: () => setState(() {
                _passoCodigo = false;
                _codigo.clear();
                _erro = null;
              }),
            ),
          ],
        ],
      ),
    );
  }
}
