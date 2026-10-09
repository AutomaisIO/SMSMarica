import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/theme/tipografia.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';
import 'package:url_launcher/url_launcher.dart';

/// Moldura das telas pré-login (CPF / código) — `AuthShell.tsx`. Hero cívico com a marca
/// centralizada, folha de "papel" que recebe a logo oficial de Maricá e o conteúdo — tudo no eixo
/// central, com cara de app (não de formulário web).
class AuthShell extends StatelessWidget {
  const AuthShell({required this.titulo, required this.subtitulo, required this.child, super.key});

  final String titulo;
  final String subtitulo;
  final Widget child;

  /// Hero: 48 (respiro) + selo 64 + 12 + sobretítulo ~16 + 80 (a parte que a folha cobre).
  static const double _heroSemTopo = 220;

  /// Rodapé "Privacidade · Termos": linha ~18 + 24 de respiro.
  static const double _rodapeSemBase = 42;

  /// A folha de papel sobe sobre o hero (`-mt-12` do PWA).
  static const double _sobreposicao = 48;

  Future<void> _abrir(String caminho) async {
    await launchUrl(Uri.parse('$appWebUrl$caminho'), mode: LaunchMode.externalApplication);
  }

  @override
  Widget build(BuildContext context) {
    final topo = MediaQuery.paddingOf(context).top;
    final base = MediaQuery.paddingOf(context).bottom;
    final alturaHero = topo + _heroSemTopo;
    final alturaRodape = base + _rodapeSemBase;
    final rodape = Txt.sans(12, cor: CoresMarica.tintaMute);
    final link = rodape.copyWith(decoration: TextDecoration.underline, decorationColor: CoresMarica.tintaMute);

    // Hero — faixa guilloché com a marca da Saúde centralizada.
    final hero = Guilloche(
      gradiente: CoresMarica.gradienteCivico,
      child: Container(
        height: alturaHero,
        alignment: Alignment.topCenter,
        padding: EdgeInsets.fromLTRB(24, topo + 48, 24, 0),
        child: Column(
          children: [
            Container(
              width: 64,
              height: 64,
              decoration: BoxDecoration(
                color: CoresMarica.branco.withValues(alpha: 0.15),
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: CoresMarica.branco.withValues(alpha: 0.25)),
              ),
              child: const Icon(LucideIcons.heartPulse, size: 32, color: CoresMarica.branco),
            ),
            const SizedBox(height: 12),
            Text(
              'APP DO CIDADÃO',
              textAlign: TextAlign.center,
              style: Txt.sobretitulo(cor: CoresMarica.branco.withValues(alpha: 0.85), espacamentoEm: 0.3),
            ),
          ],
        ),
      ),
    );

    // Folha de papel: logo + conteúdo, centralizados no espaço restante.
    Widget folha(double minimo) => Container(
          constraints: BoxConstraints(minHeight: minimo),
          alignment: Alignment.center,
          decoration: const BoxDecoration(
            color: CoresMarica.papel,
            borderRadius: BorderRadius.vertical(top: Radius.circular(32)),
            boxShadow: [
              BoxShadow(color: Color(0x8C6E1322), offset: Offset(0, -14), blurRadius: 44, spreadRadius: -26),
            ],
          ),
          padding: const EdgeInsets.fromLTRB(24, 36, 24, 32),
          child: Subir(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Center(
                  child: Semantics(
                    label: 'Prefeitura de Maricá — cidade que cuida, transforma e inspira',
                    image: true,
                    child: Image.asset('assets/brand/marica_logo.png', width: 188),
                  ),
                ),
                const SizedBox(height: 32),
                Text(titulo, textAlign: TextAlign.center, style: Txt.display(26, altura: 1.25)),
                const SizedBox(height: 8),
                Center(
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 304),
                    child: Text(
                      subtitulo,
                      textAlign: TextAlign.center,
                      style: Txt.sans(15, cor: CoresMarica.tintaMute, altura: 1.625),
                    ),
                  ),
                ),
                const SizedBox(height: 32),
                child,
              ],
            ),
          ),
        );

    final rodapeLinks = Container(
      height: alturaRodape,
      color: CoresMarica.papel,
      padding: EdgeInsets.fromLTRB(24, 0, 24, base + 24),
      alignment: Alignment.bottomCenter,
      child: Text.rich(
        TextSpan(
          style: rodape,
          children: [
            TextSpan(
              text: 'Privacidade',
              style: link,
              recognizer: TapGestureRecognizer()..onTap = () => _abrir('/privacidade/'),
            ),
            const TextSpan(text: ' · '),
            TextSpan(
              text: 'Termos',
              style: link,
              recognizer: TapGestureRecognizer()..onTap = () => _abrir('/termos/'),
            ),
          ],
        ),
        textAlign: TextAlign.center,
      ),
    );

    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.light.copyWith(statusBarColor: Colors.transparent),
      child: Scaffold(
        backgroundColor: CoresMarica.papel,
        body: LayoutBuilder(
          // Sem IntrinsicHeight: ele subestimava a altura de campos/imagem e a folha estourava em
          // tela baixa. Hero e rodapé têm altura fixa; a folha começa 48px acima do fim do hero e
          // recebe o resto da tela como altura MÍNIMA, centralizando o conteúdo — se não couber,
          // a tela rola.
          builder: (context, c) {
            final minimo = c.maxHeight - (alturaHero - _sobreposicao) - alturaRodape;
            return SingleChildScrollView(
              child: Stack(
                children: [
                  Padding(
                    padding: EdgeInsets.only(top: alturaHero - _sobreposicao),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [folha(minimo > 0 ? minimo : 0), rodapeLinks],
                    ),
                  ),
                  // O hero é desenhado POR CIMA da folha, como no navegador: no PWA o `.guilloche`
                  // é posicionado (cria camada própria) e cobre o topo arredondado da folha — o
                  // cidadão vê uma borda reta. Fica igual ao que já está em produção.
                  Positioned(top: 0, left: 0, right: 0, child: hero),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}
