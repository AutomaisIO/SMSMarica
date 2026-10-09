import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image/image.dart' as img;
import 'package:image_picker/image_picker.dart';
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

/// Mesmo teto do servidor (25 MB): conferir antes poupa o cidadão de subir o arquivo à toa.
const _tamanhoMaximo = 25 * 1024 * 1024;
const _tituloMax = 200;
const _descricaoMax = 2000;

/// Documentos do paciente (`Documentos.tsx`): TUDO o que está no cadastro dele (documentos,
/// anexos da anamnese, laudos assinados, imagens dos exames — sim, repete o que está em Exames, de
/// propósito: aqui é a pasta completa) e o que ele mesmo enviou. O envio entra "Em conferência"
/// até a equipe aceitar; enquanto isso, só ele vê e pode retirar.
class DocumentosPage extends ConsumerStatefulWidget {
  const DocumentosPage({super.key});

  @override
  ConsumerState<DocumentosPage> createState() => _DocumentosPageState();
}

class _DocumentosPageState extends ConsumerState<DocumentosPage> {
  List<ItemAcervo>? _itens;
  String? _erro;
  bool _enviadoAgora = false;

  @override
  void initState() {
    super.initState();
    _carregar();
  }

  /// [silencioso]: recarrega sem trocar a lista por esqueleto (após enviar/retirar).
  Future<void> _carregar({bool silencioso = false}) async {
    setState(() {
      _erro = null;
      if (!silencioso) _itens = null;
    });
    try {
      final itens = await ref.read(apiProvider).documentos();
      if (mounted) setState(() => _itens = itens);
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    }
  }

  Future<void> _anexar() async {
    setState(() => _enviadoAgora = false);
    final enviado = await abrirFolha<bool>(context, builder: (_) => const _AnexarDocumentoFolha());
    if (enviado ?? false) {
      if (!mounted) return;
      setState(() => _enviadoAgora = true);
      await _carregar(silencioso: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final emConferencia = _itens?.where((i) => i.pendente).length ?? 0;

    final Widget conteudo;
    if (_erro != null) {
      conteudo = ErroCard(mensagem: _erro!, aoTentar: _carregar);
    } else if (_itens == null) {
      conteudo = const Column(
        children: [Esqueleto(), SizedBox(height: 12), Esqueleto(), SizedBox(height: 12), Esqueleto()],
      );
    } else if (_itens!.isEmpty) {
      conteudo = const EstadoVazio(
        icone: LucideIcons.folderOpen,
        titulo: 'Nenhum documento ainda',
        descricao:
            'Exames, laudos e documentos do seu cadastro aparecem aqui. Use “Anexar documento” para enviar uma foto ou PDF.',
      );
    } else {
      conteudo = Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (var i = 0; i < _itens!.length; i++) ...[
            if (i > 0) const SizedBox(height: 12),
            _DocumentoCard(
              key: ValueKey(_itens![i].chave),
              item: _itens![i],
              aoRetirar: () => _carregar(silencioso: true),
            ),
          ],
        ],
      );
    }

    return CorpoPagina(
      aoAtualizar: () => _carregar(silencioso: true),
      children: [
        const CabecalhoSecao(sobretitulo: 'Cadastro', titulo: 'Documentos'),
        Transform.translate(
          offset: const Offset(0, -4),
          child: Text(
            'Tudo o que está no seu cadastro: exames, laudos, receitas e documentos. Você também pode '
            'enviar uma foto ou PDF para a equipe da Saúde.',
            style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43),
          ),
        ),
        const SizedBox(height: 12),
        BotaoPrimario(rotulo: 'Anexar documento', icone: LucideIcons.plus, onPressed: _anexar),
        if (_enviadoAgora) ...[
          const SizedBox(height: 12),
          _AvisoEnviado(aoFechar: () => setState(() => _enviadoAgora = false)),
        ],
        if (emConferencia > 0 && !_enviadoAgora) ...[
          const SizedBox(height: 12),
          Row(
            children: [
              const Icon(LucideIcons.clock, size: 16, color: CoresMarica.ambar600),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  emConferencia == 1
                      ? '1 documento seu está em conferência pela equipe.'
                      : '$emConferencia documentos seus estão em conferência pela equipe.',
                  style: Txt.sans(12, cor: CoresMarica.tintaMute),
                ),
              ),
            ],
          ),
        ],
        const SizedBox(height: 20),
        conteudo,
      ],
    );
  }
}

class _AvisoEnviado extends StatelessWidget {
  const _AvisoEnviado({required this.aoFechar});

  final VoidCallback aoFechar;

  @override
  Widget build(BuildContext context) {
    final base = Txt.sans(14, cor: CoresMarica.lagoaEscuro, altura: 1.43);
    return Semantics(
      liveRegion: true,
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: CoresMarica.lagoaClaro,
          borderRadius: BorderRadius.circular(RaiosMarica.x2l),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Padding(
              padding: EdgeInsets.only(top: 2),
              child: Icon(LucideIcons.circleCheck, size: 20, color: CoresMarica.lagoaEscuro),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text.rich(
                TextSpan(
                  style: base,
                  children: [
                    const TextSpan(text: 'Documento enviado! Ele fica '),
                    TextSpan(text: 'em conferência', style: base.copyWith(fontWeight: FontWeight.w700)),
                    const TextSpan(
                      text: ' até a equipe da Saúde confirmar e, depois disso, passa a valer no seu cadastro.',
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(width: 8),
            InkWell(
              onTap: aoFechar,
              borderRadius: BorderRadius.circular(RaiosMarica.lg),
              child: const Tooltip(
                message: 'Fechar aviso',
                child: Padding(
                  padding: EdgeInsets.all(4),
                  child: Icon(LucideIcons.x, size: 16, color: CoresMarica.lagoaEscuro),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _DocumentoCard extends ConsumerStatefulWidget {
  const _DocumentoCard({required this.item, required this.aoRetirar, super.key});

  final ItemAcervo item;
  final VoidCallback aoRetirar;

  @override
  ConsumerState<_DocumentoCard> createState() => _DocumentoCardState();
}

class _DocumentoCardState extends ConsumerState<_DocumentoCard> {
  bool _abrindo = false;
  String? _erro;
  bool _confirmando = false;
  bool _retirando = false;

  ItemAcervo get _item => widget.item;

  /// Só o envio do próprio paciente, ainda não conferido, pode ser retirado por ele.
  bool get _podeRetirar =>
      _item.tipo == 'Documento' && _item.pendente && _item.origem == origemEnviadoPeloPaciente;

  Future<void> _abrir() async {
    setState(() {
      _abrindo = true;
      _erro = null;
    });
    try {
      await abrirDocumento(
        context,
        ref,
        UrlsConteudo.acervo(_item),
        nomePadrao: _nomeArquivo(_item),
        mimeType: _item.mimeType,
      );
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _abrindo = false);
    }
  }

  Future<void> _retirar() async {
    setState(() {
      _retirando = true;
      _erro = null;
    });
    try {
      await ref.read(apiProvider).retirarDocumento(_item.id);
      if (!mounted) return;
      setState(() => _confirmando = false);
      widget.aoRetirar();
    } on Object catch (e) {
      if (mounted) setState(() => _erro = extrairMensagemDeErro(e));
    } finally {
      if (mounted) setState(() => _retirando = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final (icone, destaque) = _iconeDoItem(_item);
    final descricao = _item.descricao;

    return Cartao(
      corBorda: _item.pendente ? CoresMarica.ambar200 : CoresMarica.areia,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Material(
                  color: Colors.transparent,
                  child: InkWell(
                    onTap: _abrindo ? null : _abrir,
                    highlightColor: CoresMarica.papel,
                    splashColor: CoresMarica.areia.withValues(alpha: 0.4),
                    child: Opacity(
                      opacity: _abrindo ? 0.7 : 1,
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Selo(
                              icone: icone,
                              tamanho: 44,
                              tamanhoIcone: 20,
                              raio: RaiosMarica.xl,
                              destaque: destaque,
                              girando: _abrindo,
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(_item.titulo, style: Txt.display(16, altura: 1.375)),
                                  if (descricao != null && descricao.isNotEmpty)
                                    Padding(
                                      padding: const EdgeInsets.only(top: 2),
                                      child: Text(
                                        descricao,
                                        maxLines: 2,
                                        overflow: TextOverflow.ellipsis,
                                        style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43),
                                      ),
                                    ),
                                  const SizedBox(height: 4),
                                  Text(_descreverMeta(_item), style: Txt.sans(12, cor: CoresMarica.tintaMute)),
                                  if (_item.pendente) ...[
                                    const SizedBox(height: 8),
                                    const _PilulaConferencia(),
                                  ],
                                  if (_abrindo)
                                    Padding(
                                      padding: const EdgeInsets.only(top: 4),
                                      child: Text(
                                        _item.tipo == 'ImagensExame'
                                            ? 'Preparando o documento… pode levar alguns segundos.'
                                            : 'Abrindo…',
                                        style: Txt.sans(12, peso: FontWeight.w500, cor: CoresMarica.lagoa),
                                      ),
                                    ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ),
              if (_podeRetirar && !_confirmando)
                Padding(
                  padding: const EdgeInsets.all(8),
                  child: IconButton(
                    onPressed: () => setState(() {
                      _confirmando = true;
                      _erro = null;
                    }),
                    tooltip: 'Retirar ${_item.titulo}',
                    style: IconButton.styleFrom(
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(RaiosMarica.xl)),
                      highlightColor: CoresMarica.vermelho50,
                    ),
                    icon: const Icon(LucideIcons.trash2, size: 20, color: CoresMarica.vermelho700),
                  ),
                ),
            ],
          ),
          if (_confirmando)
            Container(
              margin: const EdgeInsets.fromLTRB(12, 0, 12, 12),
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: CoresMarica.papel,
                borderRadius: BorderRadius.circular(RaiosMarica.xl),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text.rich(
                    TextSpan(
                      style: Txt.sans(14, altura: 1.43),
                      children: [
                        const TextSpan(text: 'Retirar '),
                        TextSpan(text: _item.titulo, style: Txt.sans(14, peso: FontWeight.w700, altura: 1.43)),
                        const TextSpan(
                          text: '? A equipe ainda não conferiu este envio; ele deixa de ir para o seu cadastro.',
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Expanded(
                        child: BotaoFantasma(
                          rotulo: 'Não',
                          onPressed: _retirando ? null : () => setState(() => _confirmando = false),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: BotaoPrimario(rotulo: 'Retirar', carregando: _retirando, onPressed: _retirar),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          if (_erro != null)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              child: TextoErro(_erro!, tamanho: 12),
            ),
        ],
      ),
    );
  }
}

class _PilulaConferencia extends StatelessWidget {
  const _PilulaConferencia();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 2),
      decoration: BoxDecoration(color: CoresMarica.ambar50, borderRadius: BorderRadius.circular(999)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(LucideIcons.clock, size: 14, color: CoresMarica.ambar700),
          const SizedBox(width: 4),
          Text('Em conferência', style: Txt.sans(12, peso: FontWeight.w600, cor: CoresMarica.ambar700)),
        ],
      ),
    );
  }
}

/// Arquivo escolhido para enviar, já pronto (foto reduzida, tipo conferido).
class _ArquivoEscolhido {
  const _ArquivoEscolhido({required this.bytes, required this.nome, required this.mimeType});

  final Uint8List bytes;
  final String nome;
  final String mimeType;

  bool get ehImagem => mimeType.startsWith('image/');
}

/// Folha (bottom sheet) para anexar: foto pela câmera, foto da galeria ou PDF, com nome
/// obrigatório e descrição opcional. Foto grande é reduzida no aparelho antes de subir.
class _AnexarDocumentoFolha extends ConsumerStatefulWidget {
  const _AnexarDocumentoFolha();

  @override
  ConsumerState<_AnexarDocumentoFolha> createState() => _AnexarDocumentoFolhaState();
}

class _AnexarDocumentoFolhaState extends ConsumerState<_AnexarDocumentoFolha> {
  final _titulo = TextEditingController();
  final _descricao = TextEditingController();
  _ArquivoEscolhido? _arquivo;
  bool _preparando = false;
  double? _progresso;
  String? _erro;

  bool get _enviando => _progresso != null;

  @override
  void initState() {
    super.initState();
    // O botão "Enviar" só acende com nome preenchido.
    _titulo.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _titulo.dispose();
    _descricao.dispose();
    super.dispose();
  }

  /// Câmera direto. O próprio image_picker reduz a foto (lado maior ≤ 2000px, JPEG 85) — é o
  /// mesmo corte do `prepararFotoParaEnvio` do PWA, feito pelo celular antes de devolver.
  Future<void> _tirarFoto() async {
    setState(() => _erro = null);
    try {
      final foto = await ImagePicker().pickImage(
        source: ImageSource.camera,
        maxWidth: 2000,
        maxHeight: 2000,
        imageQuality: 85,
      );
      if (foto == null) return;
      setState(() => _preparando = true);
      final bytes = await foto.readAsBytes();
      _aceitar(
        _ArquivoEscolhido(
          bytes: bytes,
          nome: foto.name,
          mimeType: foto.mimeType ?? _mimePorNome(foto.name) ?? 'image/jpeg',
        ),
      );
    } on Object {
      if (mounted) setState(() => _erro = 'Não foi possível abrir a câmera.');
    } finally {
      if (mounted) setState(() => _preparando = false);
    }
  }

  /// Galeria/arquivos — aceita foto OU PDF.
  Future<void> _escolherArquivo() async {
    setState(() => _erro = null);
    try {
      final r = await FilePicker.platform.pickFiles(
        type: FileType.custom,
        allowedExtensions: const ['pdf', 'jpg', 'jpeg', 'png', 'webp', 'heic', 'heif'],
        withData: true,
      );
      final f = r?.files.firstOrNull;
      if (f == null) return;
      final bytes = f.bytes;
      final mime = _mimePorNome(f.name);
      if (bytes == null || mime == null) {
        setState(() => _erro = 'Escolha uma foto ou um arquivo PDF.');
        return;
      }
      if (mime == 'application/pdf') {
        // Alguns celulares não informam o tipo do PDF — o servidor só aceita o tipo declarado.
        _aceitar(_ArquivoEscolhido(bytes: bytes, nome: f.name, mimeType: mime));
        return;
      }
      setState(() => _preparando = true);
      final pronta = await _prepararFotoParaEnvio(_ArquivoEscolhido(bytes: bytes, nome: f.name, mimeType: mime));
      _aceitar(pronta);
    } on Object {
      if (mounted) setState(() => _erro = 'Não foi possível abrir o arquivo.');
    } finally {
      if (mounted) setState(() => _preparando = false);
    }
  }

  void _aceitar(_ArquivoEscolhido a) {
    if (!mounted) return;
    if (a.bytes.length > _tamanhoMaximo) {
      setState(() {
        _arquivo = null;
        _erro = 'O arquivo tem ${formatarTamanho(a.bytes.length)}. O limite é 25 MB.';
      });
      return;
    }
    setState(() => _arquivo = a);
  }

  Future<void> _enviar() async {
    final arquivo = _arquivo;
    if (arquivo == null) {
      setState(() => _erro = 'Escolha a foto ou o PDF do documento.');
      return;
    }
    final titulo = _titulo.text.trim();
    if (titulo.isEmpty) {
      setState(() => _erro = 'Informe o nome do documento.');
      return;
    }
    setState(() {
      _erro = null;
      _progresso = 0;
    });
    try {
      final descricao = _descricao.text.trim();
      await ref.read(apiProvider).enviarDocumento(
            bytes: arquivo.bytes,
            nomeArquivo: arquivo.nome,
            mimeType: arquivo.mimeType,
            titulo: titulo,
            descricao: descricao.isEmpty ? null : descricao,
            aoProgresso: (f) {
              if (mounted) setState(() => _progresso = f);
            },
          );
      if (mounted) Navigator.of(context).pop(true);
    } on Object catch (e) {
      if (mounted) {
        setState(() {
          _erro = _mensagemErroEnvio(e);
          _progresso = null;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final podeEnviar = !_preparando && _arquivo != null && _titulo.text.trim().isNotEmpty;
    return PopScope(
      canPop: !_enviando,
      // Durante o envio a folha não fecha nem por arrasto (este detector vence o da folha).
      child: GestureDetector(
        onVerticalDragStart: _enviando ? (_) {} : null,
        child: SingleChildScrollView(
          padding: EdgeInsets.fromLTRB(24, 24, 24, MediaQuery.paddingOf(context).bottom + 24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Expanded(child: CabecalhoSecao(sobretitulo: 'Documentos', titulo: 'Anexar documento')),
                  Transform.translate(
                    offset: const Offset(8, -4),
                    child: IconButton(
                      onPressed: _enviando ? null : () => Navigator.of(context).pop(),
                      tooltip: 'Fechar',
                      style: IconButton.styleFrom(
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(RaiosMarica.xl)),
                      ),
                      icon: Icon(
                        LucideIcons.x,
                        size: 20,
                        color: CoresMarica.tintaMute.withValues(alpha: _enviando ? 0.4 : 1),
                      ),
                    ),
                  ),
                ],
              ),
              Text(
                'Envie uma foto ou PDF (receita, exame em papel, encaminhamento…). A equipe da Saúde confere '
                'antes de o documento valer no seu cadastro.',
                style: Txt.sans(14, cor: CoresMarica.tintaMute, altura: 1.43),
              ),
              const SizedBox(height: 16),
              _areaArquivo(),
              const SizedBox(height: 16),
              Campo(
                rotulo: 'Nome do documento',
                controller: _titulo,
                dica: 'Ex.: Receita do cardiologista',
                maxLength: _tituloMax,
                habilitado: !_enviando,
              ),
              const SizedBox(height: 16),
              Campo(
                rotulo: 'Descrição',
                rotuloComplemento: '(opcional)',
                controller: _descricao,
                dica: 'Algo que a equipe precise saber sobre este documento',
                maxLength: _descricaoMax,
                linhas: 3,
                teclado: TextInputType.multiline,
                habilitado: !_enviando,
              ),
              if (_erro != null) ...[const SizedBox(height: 16), TextoErro(_erro!)],
              if (_enviando) ...[const SizedBox(height: 16), _BarraProgresso(fracao: _progresso ?? 0)],
              const SizedBox(height: 16),
              BotaoPrimario(
                rotulo: 'Enviar documento',
                carregando: _enviando,
                onPressed: podeEnviar ? _enviar : null,
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _areaArquivo() {
    if (_preparando) {
      return BordaTracejada(
        child: Container(
          constraints: const BoxConstraints(minHeight: 96),
          decoration: BoxDecoration(
            color: CoresMarica.branco,
            borderRadius: BorderRadius.circular(RaiosMarica.x2l),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Girando(tamanho: 20),
              const SizedBox(width: 8),
              Text('Preparando a foto…', style: Txt.sans(14, cor: CoresMarica.tintaMute)),
            ],
          ),
        ),
      );
    }

    final arquivo = _arquivo;
    if (arquivo != null) {
      return Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: CoresMarica.branco,
          borderRadius: BorderRadius.circular(RaiosMarica.x2l),
          border: Border.all(color: CoresMarica.areia),
        ),
        child: Row(
          children: [
            if (arquivo.ehImagem)
              ClipRRect(
                borderRadius: BorderRadius.circular(RaiosMarica.xl),
                child: Image.memory(
                  arquivo.bytes,
                  width: 64,
                  height: 64,
                  fit: BoxFit.cover,
                  cacheWidth: 192,
                  semanticLabel: 'Prévia do documento',
                  // HEIC e afins: o Flutter pode não decodificar — mostra o ícone de arquivo.
                  errorBuilder: (_, __, ___) => const Selo(
                    icone: LucideIcons.image,
                    tamanho: 64,
                    tamanhoIcone: 28,
                    raio: RaiosMarica.xl,
                  ),
                ),
              )
            else
              const Selo(icone: LucideIcons.fileText, tamanho: 64, tamanhoIcone: 28, raio: RaiosMarica.xl),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    arquivo.nome,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Txt.sans(14, peso: FontWeight.w600),
                  ),
                  Text(formatarTamanho(arquivo.bytes.length), style: Txt.sans(12, cor: CoresMarica.tintaMute)),
                ],
              ),
            ),
            TextButton(
              onPressed: _enviando ? null : () => setState(() => _arquivo = null),
              style: TextButton.styleFrom(
                foregroundColor: CoresMarica.marica,
                textStyle: Txt.sans(14, peso: FontWeight.w600),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(RaiosMarica.xl)),
              ),
              child: const Text('Trocar'),
            ),
          ],
        ),
      );
    }

    return Row(
      children: [
        Expanded(
          child: _BotaoOrigem(
            icone: LucideIcons.camera,
            rotulo: 'Tirar foto',
            destaque: true,
            onTap: _tirarFoto,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: _BotaoOrigem(icone: LucideIcons.paperclip, rotulo: 'Foto ou PDF', onTap: _escolherArquivo),
        ),
      ],
    );
  }
}

class _BotaoOrigem extends StatelessWidget {
  const _BotaoOrigem({required this.icone, required this.rotulo, required this.onTap, this.destaque = false});

  final IconData icone;
  final String rotulo;
  final VoidCallback onTap;
  final bool destaque;

  @override
  Widget build(BuildContext context) {
    return Pressionavel(
      onTap: onTap,
      escala: 0.98,
      semantica: rotulo,
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: CoresMarica.branco,
          borderRadius: BorderRadius.circular(RaiosMarica.x2l),
          border: Border.all(color: CoresMarica.areia),
          boxShadow: SombrasMarica.carta,
        ),
        child: Column(
          children: [
            Selo(icone: icone, destaque: destaque),
            const SizedBox(height: 8),
            Text(rotulo, textAlign: TextAlign.center, style: Txt.sans(14, peso: FontWeight.w600)),
          ],
        ),
      ),
    );
  }
}

class _BarraProgresso extends StatelessWidget {
  const _BarraProgresso({required this.fracao});

  final double fracao;

  @override
  Widget build(BuildContext context) {
    final pct = (fracao * 100).round();
    return Column(
      children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(999),
          child: SizedBox(
            height: 8,
            child: Stack(
              children: [
                const Positioned.fill(child: ColoredBox(color: CoresMarica.areia)),
                LayoutBuilder(
                  builder: (_, c) => AnimatedContainer(
                    duration: const Duration(milliseconds: 200),
                    width: c.maxWidth * fracao.clamp(0, 1),
                    decoration: BoxDecoration(
                      color: CoresMarica.marica,
                      borderRadius: BorderRadius.circular(999),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 4),
        Text(
          fracao < 1 ? 'Enviando… $pct%' : 'Finalizando…',
          textAlign: TextAlign.center,
          style: Txt.sans(12, cor: CoresMarica.tintaMute),
        ),
      ],
    );
  }
}

/// Texto do erro do envio: o servidor já fala a língua do cidadão (ex.: limite de pendentes).
String _mensagemErroEnvio(Object e) {
  // 413 vem do proxy/servidor web, antes da API — sem ProblemDetails.
  if (classificarErro(e).status == 413) return 'O arquivo passou do limite de 25 MB.';
  if (semConexao(e)) return 'Não foi possível enviar agora. Confira a sua internet e tente de novo.';
  return extrairMensagemDeErro(e);
}

/// Tipo pelo nome do arquivo — o seletor de arquivos nem sempre informa (e o servidor só aceita
/// o tipo declarado). Nulo = não é foto nem PDF.
String? _mimePorNome(String nome) {
  final ext = nome.contains('.') ? nome.split('.').last.toLowerCase() : '';
  return switch (ext) {
    'pdf' => 'application/pdf',
    'jpg' || 'jpeg' => 'image/jpeg',
    'png' => 'image/png',
    'webp' => 'image/webp',
    'gif' => 'image/gif',
    'heic' => 'image/heic',
    'heif' => 'image/heif',
    _ => null,
  };
}

/// Tipos de imagem que o servidor aceita como documento.
const _tiposImagemAceitos = {'image/jpeg', 'image/png', 'image/webp', 'image/gif'};

/// Prepara a FOTO escolhida da galeria (`prepararFotoParaEnvio` do PWA): a câmera do celular gera
/// 4–12 MB por foto, e o público usa sinal fraco e plano de dados curto. Reduz para no máx. 2000px
/// no lado maior, em JPEG 85 — ainda legível para receita/exame em papel. Regras:
/// - GIF vai como está (pode ser animado e costuma ser pequeno);
/// - imagem já pequena e em tipo aceito vai como está (não piora o que já está bom);
/// - formato que o servidor não aceita é convertido para JPEG — se não der para decodificar (ex.:
///   HEIC), vai o original e o servidor responde com a mensagem dele;
/// - se a versão reduzida sair maior que a original, fica a original.
Future<_ArquivoEscolhido> _prepararFotoParaEnvio(_ArquivoEscolhido a) async {
  if (a.mimeType == 'image/gif') return a;
  final aceito = _tiposImagemAceitos.contains(a.mimeType);
  try {
    final jpg = await compute(_reduzirFoto, (bytes: a.bytes, aceito: aceito));
    if (jpg == null) return a;
    final base = a.nome.replaceFirst(RegExp(r'\.[^.]+$'), '');
    return _ArquivoEscolhido(bytes: jpg, nome: '${base.isEmpty ? 'foto' : base}.jpg', mimeType: 'image/jpeg');
  } on Object {
    return a;
  }
}

/// Roda fora da thread da tela (`compute`). Nulo = manter o original.
Uint8List? _reduzirFoto(({Uint8List bytes, bool aceito}) e) {
  const ladoMax = 2000;
  final decodificada = img.decodeImage(e.bytes);
  if (decodificada == null) return null;
  final orientada = img.bakeOrientation(decodificada);
  final maior = orientada.width > orientada.height ? orientada.width : orientada.height;
  if (e.aceito && maior <= ladoMax && e.bytes.length <= 1024 * 1024) return null;

  final reduzida = maior > ladoMax
      ? img.copyResize(
          orientada,
          width: orientada.width >= orientada.height ? ladoMax : null,
          height: orientada.height > orientada.width ? ladoMax : null,
          interpolation: img.Interpolation.cubic,
        )
      : orientada;
  // Fundo branco: PNG com transparência viraria preto no JPEG.
  final fundo = img.Image(width: reduzida.width, height: reduzida.height)
    ..clear(img.ColorRgb8(255, 255, 255));
  img.compositeImage(fundo, reduzida);
  final jpg = img.encodeJpg(fundo, quality: 85);
  if (e.aceito && jpg.length >= e.bytes.length) return null;
  return jpg;
}

(IconData, bool) _iconeDoItem(ItemAcervo item) => switch (item.tipo) {
      'Laudo' => (LucideIcons.shieldCheck, true),
      'ImagensExame' => (LucideIcons.scanLine, true),
      'AnexoExame' => (LucideIcons.paperclip, false),
      _ => (item.mimeType.startsWith('image/') ? LucideIcons.image : LucideIcons.fileText, false),
    };

String _descreverMeta(ItemAcervo item) {
  final partes = <String>[
    item.origem,
    if (item.data.isNotEmpty) formatarData(item.data),
    if (item.paginas != null && item.paginas! > 0) '${item.paginas} pág.',
    if (item.tamanhoBytes != null && item.tamanhoBytes! > 0) formatarTamanho(item.tamanhoBytes!),
  ];
  return partes.where((p) => p.isNotEmpty).join(' · ');
}

/// Nome para Baixar/Compartilhar quando o servidor não mandar um (ex.: abrindo do cache).
String _nomeArquivo(ItemAcervo item) {
  final sub = item.mimeType.split('/').length > 1 ? item.mimeType.split('/')[1].replaceAll('jpeg', 'jpg') : '';
  final ext = item.mimeType == 'application/pdf' ? 'pdf' : (sub.isEmpty ? 'pdf' : sub);
  final base = item.titulo.replaceAll(RegExp(r'[\\/:*?"<>|]+'), ' ').trim();
  return '${base.isEmpty ? 'documento' : base}.$ext';
}
