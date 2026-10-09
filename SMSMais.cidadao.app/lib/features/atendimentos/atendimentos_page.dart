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
                    child: HtmlWidget(documento.conteudoHtml, textStyle: Txt.sans(14, altura: 1.5)),
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
