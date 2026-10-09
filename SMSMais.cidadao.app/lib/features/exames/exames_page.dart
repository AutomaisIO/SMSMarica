import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/formatos.dart';
import 'package:sms_mais_cidadao/shared/visualizador/visualizador.dart';
import 'package:sms_mais_cidadao/shared/widgets/lista.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Resultados dos exames (`Exames.tsx`). O magic link de "exame liberado"/"laudo pronto" chega
/// com `?exame={id}`: o card correspondente nasce EXPANDIDO, destacado e rolado para a vista —
/// a senhora cai direto no resultado.
class ExamesPage extends ConsumerWidget {
  const ExamesPage({this.destaqueId, super.key});

  final String? destaqueId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Lista<Exame>(
      sobretitulo: 'Resultados',
      titulo: 'Exames',
      carregar: () => ref.read(apiProvider).exames(),
      iconeVazio: LucideIcons.flaskConical,
      tituloVazio: 'Nenhum exame disponível',
      descricaoVazio: 'Resultados, documentos e imagens dos seus exames aparecem aqui assim que ficam prontos.',
      item: (_, e, __) => _ExameCard(key: ValueKey(e.id), exame: e, destacado: e.id == destaqueId),
    );
  }
}

class _ExameCard extends ConsumerStatefulWidget {
  const _ExameCard({required this.exame, this.destacado = false, super.key});

  final Exame exame;
  final bool destacado;

  @override
  ConsumerState<_ExameCard> createState() => _ExameCardState();
}

class _ExameCardState extends ConsumerState<_ExameCard> {
  late bool _aberto = widget.destacado;
  final _chave = GlobalKey();
  bool _gerando = false;
  bool _abrindoLaudo = false;
  String? _abrindoDoc;
  String? _erro;

  Exame get _exame => widget.exame;

  @override
  void initState() {
    super.initState();
    // Rola o card destacado para a vista assim que a lista desenha.
    if (widget.destacado) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        final c = _chave.currentContext;
        if (c != null) {
          Scrollable.ensureVisible(c, alignment: 0.5, duration: const Duration(milliseconds: 400), curve: Curves.easeOut);
        }
      });
    }
  }

  /// Abre o conteúdo no visualizador do app; [marcar] liga/desliga o "carregando" da linha.
  Future<void> _abrir(String url, String nome, void Function(bool) marcar) async {
    setState(() {
      marcar(true);
      _erro = null;
    });
    try {
      await abrirDocumento(context, ref, url, nomePadrao: nome);
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => marcar(false));
    }
  }

  @override
  Widget build(BuildContext context) {
    final e = _exame;
    final expansivel = e.documentos.isNotEmpty || e.temImagens || e.laudoAssinado;

    final acoes = <Widget>[
      if (e.temImagens)
        _LinhaAcao(
          icone: LucideIcons.image,
          girando: _gerando,
          titulo: 'Imagens do exame',
          descricao: _gerando ? 'Preparando o documento…' : 'Gerar PDF com as imagens',
          destaque: true,
          onTap: () => _abrir(UrlsConteudo.exameImagens(e.id), 'exame-imagens-${e.id}.pdf', (v) => _gerando = v),
        ),
      // Laudo pertence AO EXAME: abre o PDF assinado aqui mesmo, dentro do card.
      if (e.laudoAssinado && e.laudoId != null)
        _LinhaAcao(
          icone: LucideIcons.shieldCheck,
          girando: _abrindoLaudo,
          titulo: 'Laudo assinado',
          descricao: _abrindoLaudo ? 'Abrindo o laudo…' : 'Abrir o laudo (PDF)',
          destaque: true,
          onTap: () => _abrir(UrlsConteudo.laudo(e.laudoId!), 'laudo-${e.laudoId}.pdf', (v) => _abrindoLaudo = v),
        ),
      for (final doc in e.documentos)
        _LinhaAcao(
          icone: LucideIcons.fileText,
          girando: _abrindoDoc == doc.id,
          titulo: doc.nome,
          descricao: _descreverDoc(doc),
          onTap: () => _abrir(UrlsConteudo.anexo(doc.id), '${doc.nome}.pdf', (v) => _abrindoDoc = v ? doc.id : null),
        ),
      if (_erro != null)
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4),
          child: TextoErro(_erro!, tamanho: 12),
        ),
    ];

    return Cartao(
      key: _chave,
      destaque: widget.destacado ? CoresMarica.lagoa : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Material(
            color: Colors.transparent,
            child: InkWell(
              onTap: expansivel ? () => setState(() => _aberto = !_aberto) : null,
              highlightColor: CoresMarica.papel,
              splashColor: Colors.transparent,
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(e.nome, maxLines: 1, overflow: TextOverflow.ellipsis, style: Txt.display(16)),
                          Text(formatarData(e.data), style: Txt.sans(12, cor: CoresMarica.tintaMute)),
                        ],
                      ),
                    ),
                    const SizedBox(width: 12),
                    Etiqueta(status: e.status),
                    if (expansivel) ...[
                      const SizedBox(width: 8),
                      AnimatedRotation(
                        turns: _aberto ? 0.5 : 0,
                        duration: const Duration(milliseconds: 150),
                        child: const Icon(LucideIcons.chevronDown, size: 20, color: CoresMarica.tintaMute),
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ),
          AnimatedSize(
            duration: const Duration(milliseconds: 200),
            curve: Curves.easeOut,
            alignment: Alignment.topCenter,
            child: !_aberto
                ? const SizedBox(width: double.infinity)
                : Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: CoresMarica.papel.withValues(alpha: 0.4),
                      border: const Border(top: BorderSide(color: CoresMarica.areia)),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        for (var i = 0; i < acoes.length; i++) ...[
                          if (i > 0) const SizedBox(height: 8),
                          acoes[i],
                        ],
                      ],
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}

class _LinhaAcao extends StatelessWidget {
  const _LinhaAcao({
    required this.icone,
    required this.titulo,
    required this.descricao,
    required this.onTap,
    this.girando = false,
    this.destaque = false,
  });

  final IconData icone;
  final bool girando;
  final String titulo;
  final String descricao;
  final VoidCallback onTap;
  final bool destaque;

  @override
  Widget build(BuildContext context) {
    return Opacity(
      opacity: girando ? 0.6 : 1,
      child: Pressionavel(
        onTap: girando ? null : onTap,
        semantica: titulo,
        child: Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: CoresMarica.branco,
            borderRadius: BorderRadius.circular(RaiosMarica.xl),
            boxShadow: SombrasMarica.carta,
          ),
          child: Row(
            children: [
              Selo(
                icone: icone,
                tamanho: 36,
                tamanhoIcone: 20,
                raio: RaiosMarica.lg,
                destaque: destaque,
                girando: girando,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(titulo, maxLines: 1, overflow: TextOverflow.ellipsis, style: Txt.sans(14, peso: FontWeight.w600)),
                    Text(
                      descricao,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: Txt.sans(12, cor: CoresMarica.tintaMute),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

String _descreverDoc(AnexoResumo doc) {
  final partes = [formatarTamanho(doc.tamanhoBytes)];
  if (doc.paginas != null && doc.paginas! > 0) partes.add('${doc.paginas} pág.');
  return partes.join(' · ');
}
