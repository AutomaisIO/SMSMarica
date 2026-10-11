import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_widget_from_html_core/flutter_widget_from_html_core.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/api/api.dart';
import 'package:sms_mais_cidadao/shared/api/modelos.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/util/formatos.dart';
import 'package:sms_mais_cidadao/shared/widgets/lista.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Histórico de atendimentos na rede municipal (`Atendimentos.tsx`), com os documentos clínicos
/// de cada um (receituário, atestado…) abrindo em tela cheia.
class AtendimentosPage extends ConsumerWidget {
  const AtendimentosPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Lista<Atendimento>(
      sobretitulo: 'Histórico de saúde',
      titulo: 'Meus atendimentos',
      carregar: () => ref.read(apiProvider).atendimentos(),
      iconeVazio: LucideIcons.stethoscope,
      tituloVazio: 'Nenhum atendimento ainda',
      descricaoVazio: 'Quando você passar por consultas na rede municipal, elas aparecem aqui.',
      item: (_, a, __) => _AtendimentoCard(atendimento: a),
    );
  }
}

class _AtendimentoCard extends StatelessWidget {
  const _AtendimentoCard({required this.atendimento});

  final Atendimento atendimento;

  @override
  Widget build(BuildContext context) {
    final a = atendimento;
    return Cartao(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(a.estabelecimento, style: Txt.display(16))),
              const SizedBox(width: 12),
              Text(formatarData(a.data), style: Txt.sans(12, cor: CoresMarica.tintaMute)),
            ],
          ),
          const SizedBox(height: 4),
          Text(a.profissional, style: Txt.sans(14, cor: CoresMarica.tintaMute)),
          if (a.descricao.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(a.descricao, style: Txt.sans(14)),
          ],
          // O PWA desenha a faixa mesmo sem documento; aqui só quando há o que mostrar.
          if (a.documentos.isNotEmpty)
            Container(
              width: double.infinity,
              margin: const EdgeInsets.only(top: 12),
              padding: const EdgeInsets.only(top: 12),
              decoration: const BoxDecoration(border: Border(top: BorderSide(color: CoresMarica.areia))),
              child: Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [for (final d in a.documentos) _ChipDocumento(documento: d)],
              ),
            ),
        ],
      ),
    );
  }
}

class _ChipDocumento extends StatelessWidget {
  const _ChipDocumento({required this.documento});

  final DocumentoAtendimento documento;

  @override
  Widget build(BuildContext context) {
    return Pressionavel(
      escala: 0.95,
      semantica: documento.tipo,
      onTap: () => Navigator.of(context, rootNavigator: true).push(
        PageRouteBuilder<void>(
          transitionDuration: const Duration(milliseconds: 200),
          reverseTransitionDuration: const Duration(milliseconds: 150),
          pageBuilder: (_, __, ___) => _DocumentoTela(documento: documento),
          transitionsBuilder: (_, anim, __, filho) => FadeTransition(opacity: anim, child: filho),
        ),
      ),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: CoresMarica.papel,
          borderRadius: BorderRadius.circular(RaiosMarica.xl),
          border: Border.all(color: CoresMarica.areia),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(LucideIcons.fileText, size: 14, color: CoresMarica.lagoa),
            const SizedBox(width: 6),
            Text(documento.tipo, style: Txt.sans(12, peso: FontWeight.w500)),
          ],
        ),
      ),
    );
  }
}

/// Visualizador do documento clínico. O PWA põe o HTML do hub num iframe com sandbox vazio; aqui
/// o HTML é desenhado como widgets (sem JavaScript, sem rede), isolado do resto do app.
class _DocumentoTela extends StatelessWidget {
  const _DocumentoTela({required this.documento});

  final DocumentoAtendimento documento;

  @override
  Widget build(BuildContext context) {
    final topo = MediaQuery.paddingOf(context).top;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark.copyWith(statusBarColor: Colors.transparent),
      child: Scaffold(
        backgroundColor: CoresMarica.papel,
        body: Column(
          children: [
            DecoratedBox(
              decoration: const BoxDecoration(
                color: CoresMarica.branco,
                boxShadow: SombrasMarica.topo,
                border: Border(bottom: BorderSide(color: CoresMarica.areia)),
              ),
              child: Padding(
                padding: EdgeInsets.fromLTRB(16, topo + 12, 12, 12),
                child: Row(
                  children: [
                    const Selo(icone: LucideIcons.fileText, tamanho: 36, tamanhoIcone: 20, raio: RaiosMarica.xl),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(documento.tipo, maxLines: 1, overflow: TextOverflow.ellipsis, style: Txt.display(16)),
                          if (documento.data != null)
                            Text(formatarData(documento.data!), style: Txt.sans(12, cor: CoresMarica.tintaMute)),
                        ],
                      ),
                    ),
                    IconButton(
                      onPressed: () => Navigator.of(context).pop(),
                      tooltip: 'Fechar documento',
                      icon: const Icon(LucideIcons.x, size: 24, color: CoresMarica.tintaMute),
                    ),
                  ],
                ),
              ),
            ),
            Expanded(
              child: ColoredBox(
                color: CoresMarica.branco,
                child: SingleChildScrollView(
                  padding: EdgeInsets.fromLTRB(16, 16, 16, 16 + MediaQuery.paddingOf(context).bottom),
                  child: SelectionArea(
                    child: _DocumentoHtml(documento.conteudoHtml),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Padrão visual do documento de atendimento — o mesmo do painel e do PWA (classes `edoc-*`).
///
/// Chegam três formas do servidor: eDoc do Salux (`.edoc-campo`: rótulo + valor), Boletim do
/// Klinikos (`h3` + `p`, sem classe) e evolução em texto (`.edoc-texto`, parágrafos). O
/// renderizador não conhece `text-transform` nem `letter-spacing`, então o rótulo (e o título de
/// seção do boletim) é desenhado como widget; o resto vai por estilo de elemento.
class _DocumentoHtml extends StatelessWidget {
  const _DocumentoHtml(this.html);

  final String html;

  @override
  Widget build(BuildContext context) {
    return HtmlWidget(
      html,
      textStyle: Txt.sans(15, altura: 1.6),
      customStylesBuilder: (e) {
        final classes = e.classes;
        final ultimo = e.nextElementSibling == null;
        if (classes.contains('edoc-titulo')) {
          return {
            'color': '#0A5E5D',
            'font-size': '16px',
            'font-weight': '600',
            'margin': '0 0 12px',
            'padding-bottom': '8px',
            'border-bottom': '2px solid #E5F2F1',
          };
        }
        if (classes.contains('edoc-doc') && e.previousElementSibling != null) {
          return {'margin-top': '24px', 'padding-top': '16px', 'border-top': '1px solid #ECE3E1'};
        }
        if (classes.contains('edoc-campo')) {
          return {'padding': '10px 0', if (!ultimo) 'border-bottom': '1px solid #ECE3E1'};
        }
        // O valor do eDoc guarda as quebras de linha que o profissional digitou.
        if (classes.contains('edoc-valor')) return {'white-space': 'pre'};
        if (classes.contains('edoc-vazio')) return {'color': '#6F6466', 'font-style': 'italic'};
        if (e.localName == 'p') return {'margin': ultimo ? '0' : '0 0 12px'};
        return null;
      },
      customWidgetBuilder: (e) {
        final secao = e.localName == 'h3' && !e.classes.contains('edoc-titulo');
        if (!secao && !e.classes.contains('edoc-rotulo')) return null;

        final rotulo = Text(
          e.text.trim().toUpperCase(),
          style: Txt.sans(11, peso: FontWeight.w600, cor: CoresMarica.tintaMute, altura: 1.4, espacamento: 0.66),
        );
        if (!secao) return Padding(padding: const EdgeInsets.only(bottom: 2), child: rotulo);

        // Seção do boletim (Anamnese, Exame físico…): fio acima, menos na primeira. O parágrafo de
        // cima já deixa 12; aqui as margens somam, não colapsam como no navegador.
        final primeira = e.previousElementSibling == null;
        return Container(
          width: double.infinity,
          margin: EdgeInsets.only(top: primeira ? 0 : 6, bottom: 4),
          padding: EdgeInsets.only(top: primeira ? 0 : 14),
          decoration: primeira ? null : const BoxDecoration(border: Border(top: BorderSide(color: CoresMarica.areia))),
          child: rotulo,
        );
      },
    );
  }
}
