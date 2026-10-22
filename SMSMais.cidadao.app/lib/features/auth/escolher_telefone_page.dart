import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/features/auth/auth_shell.dart';
import 'package:sms_mais_cidadao/features/auth/entrada_erros.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// A escolha "Nenhum desses é meu" — fica no mesmo grupo, depois dos números.
const _nenhum = 'nenhum';

/// Quem já tem WhatsApp confirmado vê só ele: trocar é no posto (ADR-0057 §5).
OpcaoTelefoneEntrada? _unicoVerificado(EntradaCidadao? entrada) {
  final opcoes = entrada?.opcoes ?? const <OpcaoTelefoneEntrada>[];
  return opcoes.length == 1 && opcoes.first.verificado ? opcoes.first : null;
}

String _rotuloOrigem(OpcaoTelefoneEntrada opcao) {
  if (opcao.verificado) return 'Seu WhatsApp confirmado';
  return opcao.origem == 'esus' ? 'Encontrado no e-SUS' : 'Do seu cadastro';
}

/// Passo 2 do login (`EscolherTelefone.tsx`, ADR-0078): para onde vai o código. CPF e nascimento já
/// conferiram; o servidor mostra só o final dos números que conhece e a pessoa escolhe o dela — o
/// código nunca vai para um número digitado. "Nenhum desses é meu" procura no e-SUS quando o canal
/// permite (`podeBuscarEsus`); senão, ou se lá também não houver, o caminho é o posto.
class EscolherTelefonePage extends ConsumerStatefulWidget {
  const EscolherTelefonePage({required this.dados, super.key});

  /// 'cpf' (String) e 'entrada' ([EntradaCidadao]) — vindos do Login.
  final Map<String, Object?>? dados;

  @override
  ConsumerState<EscolherTelefonePage> createState() => _EscolherTelefonePageState();
}

class _EscolherTelefonePageState extends ConsumerState<EscolherTelefonePage> {
  late EntradaCidadao? _entrada = widget.dados?['entrada'] as EntradaCidadao?;

  // O confirmado sozinho já vem marcado; numa lista, a pessoa escolhe — reconhecer o próprio número
  // entre os mascarados faz parte da prova.
  late String? _escolha = _unicoVerificado(_entrada)?.id;
  bool _enviando = false;
  bool _procurando = false;
  bool _semOutros = false;
  String? _erro;

  String? get _cpf => widget.dados?['cpf'] as String?;

  @override
  void initState() {
    super.initState();
    if (_cpf == null || _entrada == null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) context.go('/login');
      });
    }
  }

  /// Volta ao CPF (os campos continuam preenchidos), opcionalmente com um aviso.
  void _voltar([String? aviso]) {
    if (context.canPop()) {
      context.pop(aviso);
    } else {
      context.go('/login');
    }
  }

  void _escolher(String id) => setState(() {
        _escolha = id;
        _erro = null;
      });

  Future<void> _enviar() async {
    final entrada = _entrada;
    final opcao = entrada?.opcoes.where((o) => o.id == _escolha).firstOrNull;
    if (entrada == null || opcao == null || _enviando) return;
    setState(() {
      _erro = null;
      _enviando = true;
    });
    try {
      final r = await ref.read(apiProvider).enviarCodigo(entrada.desafioId, opcao.id);
      if (!mounted) return;
      // Sem `await`: o botão sai do "carregando" já na ida. Voltar do código traz de volta a lista.
      context.push('/login/codigo', extra: <String, Object?>{
        'cpf': _cpf,
        'codigoTeste': r.codigoTeste,
        'telefoneMascarado': r.telefoneMascarado ?? opcao.finalDoNumero,
        // "Trocar é no posto" só se diz do WhatsApp já confirmado; o do cadastro acabou de ser
        // escolhido numa lista que já oferecia "Nenhum desses é meu".
        'semTroca': !opcao.verificado,
      }).ignore();
    } on Object catch (e) {
      if (!mounted) return;
      if (entradaExpirou(e)) {
        _voltar(avisoEntradaExpirada);
      } else {
        setState(() => _erro = mensagemDaEntrada(e));
      }
    } finally {
      if (mounted) setState(() => _enviando = false);
    }
  }

  Future<void> _procurarNoEsus() async {
    final entrada = _entrada;
    if (entrada == null || _procurando) return;
    setState(() {
      _erro = null;
      _procurando = true;
    });
    try {
      final nova = await ref.read(apiProvider).procurarTelefoneNoEsus(entrada.desafioId);
      if (!mounted) return;
      final conhecidos = entrada.opcoes.map((o) => o.id).toSet();
      final achouOutros = nova.opcoes.any((o) => !conhecidos.contains(o.id));
      setState(() {
        _entrada = nova;
        // A lista mudou: a pessoa escolhe de novo. Sem nada novo, fica no "nenhum" e vê o posto.
        if (achouOutros) {
          _escolha = null;
        } else {
          _semOutros = true;
        }
      });
    } on Object catch (e) {
      if (!mounted) return;
      if (entradaExpirou(e)) {
        _voltar(avisoEntradaExpirada);
      } else {
        setState(() => _erro = mensagemDaEntrada(e));
      }
    } finally {
      if (mounted) setState(() => _procurando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final entrada = _entrada;
    if (_cpf == null || entrada == null) return const SizedBox.shrink();

    final semNumeros = entrada.opcoes.isEmpty;
    final unico = _unicoVerificado(entrada);
    final nenhum = semNumeros || _escolha == _nenhum;
    // Para quem só tem o confirmado, "nenhum desses" levaria ao mesmo posto do aviso abaixo.
    final mostrarNenhum = !semNumeros && (unico == null || entrada.podeBuscarEsus);
    final ocupado = _enviando || _procurando;

    final Widget acao;
    if (!nenhum) {
      acao = BotaoPrimario(
        rotulo: 'Enviar código',
        icone: LucideIcons.messageCircle,
        carregando: _enviando,
        onPressed: _escolha != null ? _enviar : null,
      );
    } else if (entrada.podeBuscarEsus) {
      acao = Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          BotaoPrimario(
            rotulo: 'Procurar outros números',
            icone: LucideIcons.search,
            carregando: _procurando,
            onPressed: _procurarNoEsus,
          ),
          if (_procurando) ...[
            const SizedBox(height: 20),
            Semantics(
              liveRegion: true,
              child: Text(
                'Procurando outros números… isso pode levar alguns segundos.',
                textAlign: TextAlign.center,
                style: Txt.sans(14, cor: CoresMarica.tintaMute),
              ),
            ),
          ],
        ],
      );
    } else {
      acao = _AvisoPosto(
        inicio: _semOutros ? 'Não encontramos outros números. Procure o ' : 'Procure o ',
        fim: ' onde você é atendido(a), com um documento com foto, para atualizar o seu WhatsApp.',
      );
    }

    return AuthShell(
      titulo: 'Para onde enviamos o código?',
      subtitulo: semNumeros
          ? 'Não encontramos um celular no seu cadastro.'
          : 'Escolha o seu WhatsApp. Mostramos só o final de cada número.',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (!semNumeros) ...[
            for (final o in entrada.opcoes) ...[
              _OpcaoCartao(
                marcada: _escolha == o.id,
                onTap: ocupado ? null : () => _escolher(o.id),
                semantica: 'WhatsApp final ${o.finalDoNumero}',
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text.rich(
                      TextSpan(
                        style: Txt.sans(15),
                        children: [
                          const TextSpan(text: 'WhatsApp final '),
                          TextSpan(
                            text: o.finalDoNumero,
                            style: Txt.display(18, espacamento: 18 * 0.12).copyWith(fontFeatures: Txt.tabular),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 6),
                    _EtiquetaOrigem(rotulo: _rotuloOrigem(o), confirmado: o.verificado),
                  ],
                ),
              ),
              const SizedBox(height: 12),
            ],
            if (mostrarNenhum) ...[
              _OpcaoCartao(
                marcada: _escolha == _nenhum,
                onTap: ocupado ? null : () => _escolher(_nenhum),
                semantica: 'Nenhum desses é meu',
                child: Text('Nenhum desses é meu', style: Txt.sans(15, peso: FontWeight.w500)),
              ),
              const SizedBox(height: 12),
            ],
            const SizedBox(height: 8),
          ],
          if (_erro != null) ...[
            TextoErro(_erro!, centralizado: true),
            const SizedBox(height: 20),
          ],
          acao,
          // Quem já tem número confirmado NÃO troca de número por aqui: seria a porta para alguém
          // que sabe CPF e nascimento apontar o contato para o próprio celular.
          if (unico != null && !nenhum) ...[
            const SizedBox(height: 20),
            const _AvisoPosto(
              inicio: 'Não tem mais esse número? Para trocar, procure o ',
              fim: ' onde você é atendido(a), com um documento com foto.',
            ),
          ],
          const SizedBox(height: 20),
          LinkDiscreto(rotulo: 'Voltar', onTap: _voltar),
        ],
      ),
    );
  }
}

/// Cartão de escolha única (radio), com o anel lagoa do foco quando marcado.
class _OpcaoCartao extends StatelessWidget {
  const _OpcaoCartao({required this.marcada, required this.onTap, required this.child, this.semantica});

  final bool marcada;
  final VoidCallback? onTap;
  final Widget child;
  final String? semantica;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      inMutuallyExclusiveGroup: true,
      checked: marcada,
      child: Opacity(
        opacity: onTap == null ? 0.6 : 1,
        child: Pressionavel(
          onTap: onTap,
          semantica: semantica,
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 150),
            constraints: const BoxConstraints(minHeight: 64),
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            decoration: BoxDecoration(
              color: CoresMarica.branco,
              borderRadius: BorderRadius.circular(RaiosMarica.x2l),
              border: Border.all(color: marcada ? CoresMarica.lagoa : CoresMarica.areia),
              boxShadow: [
                ...SombrasMarica.carta,
                if (marcada) BoxShadow(color: CoresMarica.lagoa.withValues(alpha: 0.15), spreadRadius: 4),
              ],
            ),
            child: Row(
              children: [
                AnimatedContainer(
                  duration: const Duration(milliseconds: 150),
                  width: 20,
                  height: 20,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: marcada ? CoresMarica.lagoa : CoresMarica.tintaMute.withValues(alpha: 0.4),
                      width: 2,
                    ),
                  ),
                  child: marcada
                      ? Container(
                          width: 10,
                          height: 10,
                          decoration: const BoxDecoration(shape: BoxShape.circle, color: CoresMarica.lagoa),
                        )
                      : null,
                ),
                const SizedBox(width: 12),
                Expanded(child: child),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// De onde veio o número (`rounded-full px-3 py-1 text-xs font-semibold`).
class _EtiquetaOrigem extends StatelessWidget {
  const _EtiquetaOrigem({required this.rotulo, required this.confirmado});

  final String rotulo;
  final bool confirmado;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      decoration: BoxDecoration(
        color: confirmado ? CoresMarica.lagoaClaro : CoresMarica.areia.withValues(alpha: 0.6),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        rotulo,
        style: Txt.sans(12, peso: FontWeight.w600, cor: confirmado ? CoresMarica.lagoaEscuro : CoresMarica.tintaMute),
      ),
    );
  }
}

/// Caixa "procure o posto de saúde" — a mesma da tela do código (`Otp.tsx`).
class _AvisoPosto extends StatelessWidget {
  const _AvisoPosto({required this.inicio, required this.fim});

  final String inicio;
  final String fim;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: CoresMarica.branco.withValues(alpha: 0.7),
        borderRadius: BorderRadius.circular(RaiosMarica.x2l),
        border: Border.all(color: CoresMarica.areia),
      ),
      child: Text.rich(
        TextSpan(
          style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.625),
          children: [
            TextSpan(text: inicio),
            TextSpan(text: 'posto de saúde', style: Txt.sans(14, peso: FontWeight.w700, cor: CoresMarica.tintaMute)),
            TextSpan(text: fim),
          ],
        ),
        textAlign: TextAlign.center,
      ),
    );
  }
}
