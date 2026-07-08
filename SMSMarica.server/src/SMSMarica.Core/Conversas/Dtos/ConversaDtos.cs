using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Conversas.Dtos;

/// <summary>Aba/filtro da lista de conversas.</summary>
public enum AbaConversas
{
    /// <summary>Só as conversas cujo responsável sou eu.</summary>
    Minhas = 1,

    /// <summary>Fila das minhas unidades (inclui as minhas e a triagem sem unidade).</summary>
    Unidade = 2,

    /// <summary>Da(s) minha(s) unidade(s) ainda sem responsável.</summary>
    NaoAtribuidas = 3,

    /// <summary>Todas (exige permissão de supervisão; senão cai no escopo da Unidade).</summary>
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
