using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.ComunicacaoInterna.Dtos;

/// <summary>Unidade a que a pessoa está vinculada (origem possível de uma conversa de unidade).</summary>
public sealed record UnidadeVinculoInternoDto(Guid UnidadeId, string Nome, bool Principal);

/// <summary>Quem eu sou no chat interno: meu id, minhas unidades e a unidade em que estou agora.</summary>
public sealed record ContextoComunicacaoInternaDto(
    Guid UsuarioId,
    IReadOnlyList<UnidadeVinculoInternoDto> MinhasUnidades,
    Guid? UnidadeAtualId);

/// <summary>Total de mensagens não lidas (balão laranja, título da aba). Conversa silenciada não conta.</summary>
public sealed record ResumoComunicacaoInternaDto(int NaoLidas);

/// <summary>
/// Item da lista de conversas, do ponto de vista de quem pede. Na conversa de unidade, A e B são
/// as duas unidades (A ≤ B pela ordem do Guid — a sessão é a mesma nos dois sentidos); na direta,
/// <see cref="OutroUsuarioId"/> é a outra pessoa e <see cref="Titulo"/> o nome dela.
/// </summary>
public sealed record ConversaInternaItemDto(
    Guid Id,
    TipoConversaInterna Tipo,
    string Titulo,
    string? Subtitulo,
    Guid? UnidadeAId,
    string? UnidadeANome,
    Guid? UnidadeBId,
    string? UnidadeBNome,
    Guid? OutroUsuarioId,
    bool OutroOnline,
    DateTime? UltimaMensagemEm,
    string? UltimaMensagemPreview,
    string? UltimaMensagemAutorNome,
    int NaoLidas,
    bool Silenciada);

/// <summary>Participante (derivado na hora dos vínculos com as unidades, ou as duas pessoas da direta).</summary>
public sealed record ParticipanteInternoDto(Guid UsuarioId, string Nome, string? UnidadeNome, bool Online);

/// <summary>
/// Detalhe da conversa aberta. <see cref="OutroLidaAte"/> só vem na direta: até onde a outra pessoa
/// leu (✓✓ nas minhas mensagens com <c>EnviadaEm</c> ≤ esse instante).
/// </summary>
public sealed record ConversaInternaDetalheDto(
    ConversaInternaItemDto Conversa,
    IReadOnlyList<ParticipanteInternoDto> Participantes,
    DateTime? OutroLidaAte);

/// <summary>
/// Anexo de uma mensagem (arquivo ou áudio gravado). O conteúdo baixa por <c>mensagens/{id}/anexo</c>.
/// <see cref="Transcricao"/> só existe depois que alguém da conversa pediu (<c>mensagens/{id}/transcricao</c>).
/// </summary>
public sealed record AnexoInternoDto(
    string Nome, string Mime, long TamanhoBytes, int? DuracaoSegundos, bool EhAudio, string? Transcricao = null);

public sealed record MensagemInternaDto(
    Guid Id,
    Guid ConversaId,
    Guid AutorId,
    string AutorNome,
    string? AutorUnidadeNome,
    string? Texto,
    DateTime EnviadaEm,
    AnexoInternoDto? Anexo);

/// <summary>Página de mensagens em ordem CRONOLÓGICA (a mais antiga primeiro). <see cref="TemMais"/> = há anteriores.</summary>
public sealed record PaginaMensagensInternasDto(IReadOnlyList<MensagemInternaDto> Mensagens, bool TemMais);

/// <summary>Unidade para o seletor de destino, com quantas pessoas tem e quantas estão com o painel aberto.</summary>
public sealed record UnidadeInternaDto(Guid Id, string Nome, int Usuarios, int Online, bool Minha);

/// <summary>Pessoa de uma unidade, para o seletor de destino.</summary>
public sealed record UsuarioInternoDto(Guid Id, string Nome, bool Online, bool SouEu);

/// <summary>Resultado da busca: a mensagem, a conversa em que está e um trecho em volta do termo.</summary>
public sealed record ResultadoBuscaInternaDto(
    Guid MensagemId,
    Guid ConversaId,
    string ConversaTitulo,
    string AutorNome,
    string Trecho,
    DateTime EnviadaEm);

public sealed record AbrirConversaUnidadeRequest(Guid UnidadeDestinoId, Guid? UnidadeOrigemId);

public sealed record AbrirConversaDiretaRequest(Guid UsuarioId);

public sealed record SilenciarConversaInternaRequest(bool Silenciada);

/// <summary>
/// Anexo recebido pela Api (multipart) já lido em memória. <see cref="TamanhoDeclarado"/> é o tamanho que o
/// navegador diz ter mandado — se não bater com o recebido, fica no log (diagnóstico do áudio de 23/10/2026).
/// </summary>
public sealed record AnexoEnviadoInterno(
    string Nome, string Mime, byte[] Conteudo, int? DuracaoSegundos, bool EhAudio, long? TamanhoDeclarado = null);

/// <summary>Conteúdo de um anexo para download.</summary>
public sealed record ConteudoAnexoInterno(string Nome, string Mime, byte[] Conteudo);

// ---- Tempo real (payloads do hub) ----

/// <summary>
/// Mensagem nova. Leva os nomes das unidades para o front montar o título sem outra chamada
/// (na direta, o título para quem recebe é o próprio autor).
/// </summary>
public sealed record MensagemInternaEventoDto(
    MensagemInternaDto Mensagem,
    TipoConversaInterna Tipo,
    string? UnidadeANome,
    string? UnidadeBNome);

public sealed record LeituraInternaEventoDto(Guid ConversaId, Guid UsuarioId, DateTime LidaAte);

public sealed record PresencaInternaEventoDto(Guid UsuarioId, bool Online);

public sealed record DigitandoInternoEventoDto(Guid ConversaId, Guid UsuarioId, string Nome);

/// <summary>Um áudio ganhou transcrição — quem está com a conversa aberta passa a ver o texto.</summary>
public sealed record TranscricaoInternaEventoDto(Guid ConversaId, Guid MensagemId, string Transcricao);
