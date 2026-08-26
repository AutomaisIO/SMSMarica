using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Conversas.Dtos;

/// <summary>Aba/filtro da lista de conversas.</summary>
public enum AbaConversas
{
    /// <summary>Só as conversas cujo responsável sou eu.</summary>
    Minhas = 1,

    /// <summary>
    /// A FILA: sem responsável, das minhas unidades ou da triagem geral (sem unidade).
    /// Disjunta de <see cref="Minhas"/> — conversa puxada sai daqui.
    /// </summary>
    Unidade = 2,

    /// <summary>Obsoleta — mesmo resultado de <see cref="Unidade"/> (compat com front antigo).</summary>
    NaoAtribuidas = 3,

    /// <summary>Todas as conversas, sem recorte de unidade/posse (ADR-0048: destravada para todo
    /// operador do módulo — só visibilidade de leitura; as ações seguem travadas por posse).</summary>
    Todas = 4,
}

/// <summary>Item da lista de conversas (cabeçalho + estado para a UI).</summary>
public sealed record ConversaListItemDto(
    Guid Id,
    string TelefoneCanonical,
    string? NomeContato,
    Guid? PacienteId,
    AssuntoConversa? Assunto,
    StatusConversa Status,
    Guid? OperadorResponsavelId,
    string? OperadorResponsavelNome,
    Guid? UnidadeId,
    string? UnidadeNome,
    DateTime? UltimaMensagemEm,
    DirecaoMensagem? UltimaMensagemDirecao,
    string? UltimaMensagemPreview,
    int NaoLidas,
    DateTime? JanelaExpiraEm,
    bool PodeTextoLivre,
    // Nome COMPLETO do paciente resolvido do hub FHIR (pelo vínculo ou pelo telefone).
    // NÃO substitui o NomeContato (perfil do WhatsApp): os dois convivem de propósito,
    // para expor divergência (telefone cadastrado na pessoa errada).
    string? PacienteNome = null);

/// <summary>Uma mensagem dentro da thread.</summary>
public sealed record MensagemDto(
    Guid Id,
    Guid? ConversaId,
    DirecaoMensagem Direcao,
    TipoMensagem? TipoMensagem,
    string? Conteudo,
    string? Template,
    Guid? AutorUsuarioId,
    string? AutorNomeExibicao,
    StatusMensagemWhatsApp Status,
    DateTime OcorridoEm);

/// <summary>Inicia uma nova conversa disparando um template HSM aprovado.</summary>
public sealed record IniciarConversaRequest(
    string Telefone,
    Guid? PacienteId,
    string? NomeContato,
    AssuntoConversa? Assunto,
    string Template,
    string Idioma,
    IReadOnlyList<string> Parametros);

/// <summary>Envia uma mensagem de texto livre (dentro da janela de 24h).</summary>
public sealed record EnviarMensagemRequest(string Texto);

/// <summary>Encaminha a conversa para outro atendente (ele vira o responsável).</summary>
public sealed record EncaminharConversaRequest(Guid ParaUsuarioId, string? Observacao);

/// <summary>Transfere a conversa para outra unidade (entra na fila de lá, sem responsável).</summary>
public sealed record TransferirConversaRequest(Guid ParaUnidadeId, string? Observacao);

/// <summary>Atendente que pode receber a conversa (ativo, com o módulo, vinculado à unidade).</summary>
public sealed record AtendenteElegivelDto(Guid UsuarioId, string Nome, bool ResponsavelAtual);

/// <summary>Unidade que pode receber a conversa por transferência.</summary>
public sealed record UnidadeDestinoDto(Guid Id, string Nome);

/// <summary>
/// Contadores de não-lidas para sino/badge sem carregar a lista: <paramref name="MinhasNaoLidas"/>
/// soma as conversas cujo responsável sou eu; <paramref name="FilaNaoLidas"/> soma as SEM
/// responsável visíveis a mim (minhas unidades + triagem geral); <paramref name="TodasNaoLidas"/>
/// soma TODAS as conversas vivas sem recorte de posse/unidade (badge da aba Todas — ADR-0048).
/// </summary>
public sealed record ResumoConversasDto(int MinhasNaoLidas, int FilaNaoLidas, int TodasNaoLidas);

/// <summary>
/// Uma "sessão" de conversa na aba do cadastro do paciente — bloco de mensagens do mesmo
/// telefone separado por 24h+ de silêncio (a janela do WhatsApp). Derivada da linha do tempo
/// de <c>whatsapp_mensagem</c>, então cobre também mensagens de automação e o legado sem
/// <c>conversa_id</c>.
/// </summary>
/// <param name="Operadores">Quem respondeu no bloco (autores das mensagens de saída).</param>
/// <param name="TemAutomaticas">Há saídas sem autor — mensagens do sistema (confirmações, avisos).</param>
/// <param name="PeloTelefone">
/// Nenhuma mensagem do bloco está vinculada a ESTE paciente — a sessão entrou por ser de um
/// telefone do cadastro (celular de família: pode ser diálogo de outra pessoa da casa).
/// </param>
public sealed record SessaoConversaPacienteDto(
    string Telefone,
    DateTime Inicio,
    DateTime Fim,
    int QtdMensagens,
    int QtdRecebidas,
    int QtdEnviadas,
    IReadOnlyList<string> Operadores,
    bool TemAutomaticas,
    bool PeloTelefone);

/// <summary>
/// Um dos cadastros que carregam o telefone da conversa. Celular de família aparece no
/// cadastro da mãe, do filho e do avô — quem atende precisa ver todos, não um escolhido em
/// silêncio.
/// </summary>
/// <param name="Titular">
/// True para o paciente vinculado à conversa (o que o resto do sistema considera "o dono").
/// </param>
public sealed record PacienteDoTelefoneDto(
    Guid PacienteId,
    string Nome,
    string? Cpf,
    DateOnly? DataNascimento,
    bool Titular);

/// <summary>
/// Candidato a contato na abertura de uma conversa — o paciente achado no hub FHIR, já com o
/// telefone que a mensagem usaria.
/// </summary>
/// <param name="Origem">Por onde ele foi achado ("Solicitação SISREG 123456", "Cadastro").</param>
public sealed record ContatoConversaDto(
    Guid PacienteId,
    string Nome,
    string? Telefone,
    string? Cpf,
    DateOnly? DataNascimento,
    string Origem);
