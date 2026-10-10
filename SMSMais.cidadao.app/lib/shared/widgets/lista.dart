import 'package:flutter/material.dart';
import 'package:sms_mais_cidadao/shared/api/http.dart';
import 'package:sms_mais_cidadao/shared/theme/cores_marica.dart';
import 'package:sms_mais_cidadao/shared/widgets/ui.dart';

/// Padding do conteúdo das telas autenticadas (`main` do AppShell: px-4 pt-5 pb-10).
const paddingPagina = EdgeInsets.fromLTRB(16, 20, 16, 40);

/// Corpo rolável padrão de uma tela do app, com a animação de entrada (`animate-rise`).
class CorpoPagina extends StatelessWidget {
  const CorpoPagina({required this.children, this.aoAtualizar, this.espaco = 0, super.key});

  final List<Widget> children;

  /// Puxar para atualizar (opcional).
  final Future<void> Function()? aoAtualizar;

  /// Espaço vertical entre os filhos (`space-y-*`).
  final double espaco;

  @override
  Widget build(BuildContext context) {
    final itens = <Widget>[];
    for (var i = 0; i < children.length; i++) {
      if (i > 0 && espaco > 0) itens.add(SizedBox(height: espaco));
      itens.add(children[i]);
    }
    final lista = ListView(
      padding: paddingPagina,
      physics: const AlwaysScrollableScrollPhysics(),
      children: [Subir(child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: itens))],
    );
    if (aoAtualizar == null) return lista;
    return RefreshIndicator(color: CoresMarica.marica, onRefresh: aoAtualizar!, child: lista);
  }
}

/// Scaffold das telas de lista (`Lista.tsx`): carrega, mostra esqueleto, estado vazio ou erro
/// com "Tentar de novo". Aqui ganha também o "puxar para atualizar" — as mensagens de erro do
/// PWA já pedem "Puxe a tela para baixo para atualizar". [secao] (opcional) separa a lista em
/// blocos com título — os itens já vêm na ordem dos blocos.
class Lista<T> extends StatefulWidget {
  const Lista({
    required this.titulo,
    required this.carregar,
    required this.iconeVazio,
    required this.tituloVazio,
    required this.descricaoVazio,
    required this.item,
    this.sobretitulo,
    this.antes = const [],
    this.depois = const [],
    this.secao,
    super.key,
  });

  final String? sobretitulo;
  final String titulo;
  final Future<List<T>> Function() carregar;
  final IconData iconeVazio;
  final String tituloVazio;
  final String descricaoVazio;
  final Widget Function(BuildContext context, T item, VoidCallback recarregar) item;

  /// Blocos entre o título e a lista.
  final List<Widget> antes;

  /// Blocos depois da lista (ex.: "Meus acompanhantes" no Transporte).
  final List<Widget> depois;

  /// Título do bloco do item; itens seguidos com o mesmo título ficam no mesmo bloco.
  final String Function(T item)? secao;

  @override
  State<Lista<T>> createState() => ListaState<T>();
}

class ListaState<T> extends State<Lista<T>> {
  List<T>? _itens;
  String? _erro;

  @override
  void initState() {
    super.initState();
    recarregar(silencioso: false);
  }

  /// [silencioso]: recarrega sem trocar a lista por esqueleto (após responder num card).
  Future<void> recarregar({bool silencioso = true}) async {
    setState(() {
      _erro = null;
      if (!silencioso) _itens = null;
    });
    try {
      final itens = await widget.carregar();
      if (mounted) setState(() => _itens = itens);
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    }
  }

  @override
  Widget build(BuildContext context) {
    final Widget conteudo;
    if (_erro != null) {
      conteudo = ErroCard(mensagem: _erro!, aoTentar: () => recarregar(silencioso: false));
    } else if (_itens == null) {
      conteudo = const Column(
        children: [Esqueleto(), SizedBox(height: 12), Esqueleto(), SizedBox(height: 12), Esqueleto()],
      );
    } else if (_itens!.isEmpty) {
      conteudo = EstadoVazio(icone: widget.iconeVazio, titulo: widget.tituloVazio, descricao: widget.descricaoVazio);
    } else if (widget.secao != null) {
      final filhos = <Widget>[];
      String? atual;
      for (final item in _itens!) {
        final titulo = widget.secao!(item);
        if (titulo != atual) {
          if (filhos.isNotEmpty) filhos.add(const SizedBox(height: 24));
          filhos
            ..add(TituloBloco(titulo))
            ..add(const SizedBox(height: 8));
          atual = titulo;
        } else {
          filhos.add(const SizedBox(height: 12));
        }
        filhos.add(widget.item(context, item, recarregar));
      }
      conteudo = Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: filhos);
    } else {
      conteudo = Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (var i = 0; i < _itens!.length; i++) ...[
            if (i > 0) const SizedBox(height: 12),
            widget.item(context, _itens![i], recarregar),
          ],
        ],
      );
    }

    return CorpoPagina(
      aoAtualizar: recarregar,
      children: [
        CabecalhoSecao(sobretitulo: widget.sobretitulo, titulo: widget.titulo),
        ...widget.antes,
        conteudo,
        ...widget.depois,
      ],
    );
  }
}
