namespace Automais.Zap.Data.Entities;

/// <summary>
/// Um WhatsApp Business Account do tenant. É aqui que o roteamento é configurado: os números
/// do WABA herdam <see cref="UrlDestino"/>, e só um caso raro precisa de exceção por número.
///
/// A Graph API não deixa listar os WABAs de um business sem <c>business_management</c>, que o
/// token do System User não tem — então cada um é cadastrado pelo id e validado contra a Meta.
/// </summary>
public sealed class Waba
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Id do WABA na Meta.</summary>
    public required string WabaId { get; set; }

    /// <summary>Nome como a Meta devolve. Preenchido na consulta, não digitado.</summary>
    public string? Nome { get; set; }

    /// <summary>Webhook da aplicação que recebe os eventos deste WABA.</summary>
    public string? UrlDestino { get; set; }

    /// <summary>Desligado, os números deste WABA param de ser entregues.</summary>
    public bool RoteamentoAtivo { get; set; }

    /// <summary>
    /// Segredo compartilhado com a aplicação de destino, cifrado. Preenchido, o relay assina
    /// o que encaminha com ELE, em X-Automais-Signature.
    ///
    /// Existe porque encaminhar a assinatura original da Meta só funciona enquanto os dois
    /// lados usam o mesmo App Secret — e durante a migração há dois Apps com segredos
    /// diferentes. Além disso, a conversa entre o relay e a instância é entre sistemas
    /// nossos: pedir emprestado o segredo da Meta para isso era acoplamento sem ganho.
    ///
    /// Vazio, mantém o comportamento antigo (repassa a assinatura da Meta).
    /// </summary>
    public string? SegredoEntregaCifrado { get; set; }

    public string? Observacao { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? SincronizadoEm { get; set; }

    public ICollection<Numero> Numeros { get; set; } = [];
}
